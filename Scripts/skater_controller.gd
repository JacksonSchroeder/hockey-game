class_name SkaterController
extends CharacterBody3D

# Tuning
@export var thrust: float = 20.0
@export var friction: float = 5.0
@export var max_speed: float = 10.0
@export var rotation_speed: float = 8.0
@export var dash_force: float = 15.0
@export var dash_cooldown: float = 1.0
@export var dash_duration: float = 0.2
@export var stick_reach: float = 1.5
@export var blade: Marker3D
@export var blade_speed: float = 10.0
@export var blade_height: float = 0.5
@export var puck: Puck
@export var min_shot_power: float = 10.0
@export var max_shot_power: float = 30.0
@export var max_windup_time: float = 1.0
@export var shot_buffer_time: float = 0.2
@export var aim_arrow_length: float = 5.0
@export var aim_arrow_thickness: float = 0.05
@export var shoot_mode_thrust_multiplier: float = 0.2
@export var shot_deadzone: float = 0.1
@export var wrister_elevate_force: float = 0.3
@export var slapper_elevate_force: float = 0.1
@export var brake_multiplier: float = 5.0

# State
var _input: InputState
var _gatherer: LocalInputGatherer
var _facing: Vector2 = Vector2.DOWN
var _is_backward: bool = false
var _dash_timer: float = 0.0
var _dash_active_timer: float = 0.0
var _current_blade_dir: Vector3 = Vector3(0, 0, -1)
var _shoot_mode: bool = false
var _wound_up: bool = false
var _windup_timer: float = 0.0
var _shot_buffering: bool = false
var _shot_buffer_timer: float = 0.0
var _last_shot_dir: Vector3 = Vector3.ZERO
var _aim_arrow: MeshInstance3D = null

func _ready() -> void:
	_gatherer = LocalInputGatherer.new()
	add_child(_gatherer)
	_aim_arrow = MeshInstance3D.new()
	var box = BoxMesh.new()
	box.size = Vector3(aim_arrow_thickness, 0.01, aim_arrow_length)
	_aim_arrow.mesh = box
	var mat = StandardMaterial3D.new()
	mat.albedo_color = Color(1, 1, 0, 0.7)
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_aim_arrow.material_override = mat
	_aim_arrow.visible = false
	add_child(_aim_arrow)

func _physics_process(delta: float) -> void:
	_input = _gatherer.gather()
	
	if _input.reset:
		puck.reset()
		
	if _input.self_pass and puck.carrier == null:
		var dir = (global_position - puck.global_position).normalized()
		dir.y = 0.0
		puck.linear_velocity = dir * min_shot_power
	
	_apply_movement(delta)
	_apply_facing(delta)
	_apply_dash(delta)
	_apply_shoot(delta)
	_apply_blade(delta)
	move_and_slide()
	
func _apply_blade(delta: float) -> void:
	if _shoot_mode:
		return
		
	var stick = _input.blade_vector
	
	var target_dir: Vector3
	if stick.length() < 0.1:
		target_dir = _current_blade_dir  # Hold player-relative position
	else:
		# Screen-relative: convert world input to local space
		var screen_dir = Vector3(stick.x, 0, stick.y)
		var local_dir = global_transform.basis.inverse() * screen_dir
		local_dir.y = 0
		target_dir = local_dir.normalized()
		
		# Clamp to 180° arc: 90° each side of forward (-Z)
		var forward = Vector3(0, 0, -1)
		var angle = forward.signed_angle_to(target_dir, Vector3.UP)
		if abs(angle) > PI / 2.0:
			if angle > 0:
				target_dir = Vector3(-1, 0, 0)
			else:
				target_dir = Vector3(1, 0, 0)
	
	_current_blade_dir = _current_blade_dir.lerp(target_dir, blade_speed * delta).normalized()
	
	blade.position = _current_blade_dir * stick_reach
	blade.position.y = blade_height
	blade.look_at(global_position, Vector3.UP)

func _apply_dash(delta: float) -> void:
	_dash_timer -= delta
	_dash_active_timer -= delta
	
	if _input.dash and _dash_timer <= 0.0:
		var move = _input.move_vector
		if move.length() > 0.1:
			var dash_dir = Vector3(move.x, 0, move.y).normalized()
			velocity += dash_dir * dash_force
			_dash_timer = dash_cooldown
			_dash_active_timer = dash_duration

func _apply_facing(delta: float) -> void:
	if _shoot_mode:
		return
	if _dash_active_timer > 0.0:
		return
	var move = _input.move_vector
	_is_backward = _input.orientation
	
	if move.length() > 0.1:
		var target_facing = move.normalized()
		if _is_backward:
			target_facing = -target_facing
		_facing = _facing.lerp(target_facing, rotation_speed * delta).normalized()
	
	rotation.y = atan2(-_facing.x, -_facing.y)

func _apply_movement(delta: float) -> void:
	var move = _input.move_vector
	
	if move.length() > 0.0:
		var thrust_dir = Vector3(move.x, 0, move.y)
		var current_thrust = thrust * shoot_mode_thrust_multiplier if _shoot_mode else thrust
		velocity += thrust_dir * current_thrust * delta
	
	# Friction
	var horizontal_vel = Vector2(velocity.x, velocity.z)
	var current_friction = friction * brake_multiplier if _input.brake else friction
	horizontal_vel = horizontal_vel.move_toward(Vector2.ZERO, current_friction * delta)
	velocity.x = horizontal_vel.x
	velocity.z = horizontal_vel.y
	
	# Speed cap
	var speed = Vector2(velocity.x, velocity.z).length()
	if speed > max_speed:
		var capped = Vector2(velocity.x, velocity.z).normalized() * max_speed
		velocity.x = capped.x
		velocity.z = capped.y
		
func _apply_shoot(delta: float) -> void:
	if _input.shot_cancel and _shoot_mode:
		_shoot_mode = false
		_wound_up = false
		_windup_timer = 0.0
		_shot_buffering = false
		_shot_buffer_timer = 0.0
		_aim_arrow.visible = false
		return
	
	if _input.shoot_pressed:
		_shoot_mode = true
	
	if not _shoot_mode:
		return
	
	if not _input.shoot_held:
		_shoot_mode = false
		_wound_up = false
		_windup_timer = 0.0
		_shot_buffering = false
		_shot_buffer_timer = 0.0
		_aim_arrow.visible = false
		return
	
	var stick = _input.blade_vector
	
	if stick.length() > 0.2:
		var screen_dir = Vector3(stick.x, 0, stick.y)
		var local_dir = global_transform.basis.inverse() * screen_dir
		
		if not _wound_up and not _shot_buffering:
			if local_dir.z > shot_deadzone:
				_wound_up = true
			elif local_dir.z < -shot_deadzone:
				_shot_buffering = true
		
		if _wound_up:
			if local_dir.z > 0.0:
				_last_shot_dir = (-screen_dir).normalized()
			_windup_timer += delta
		elif _shot_buffering:
			if local_dir.z < 0.0:
				_last_shot_dir = screen_dir.normalized()
		
		if _wound_up or _shot_buffering:
			_aim_arrow.visible = true
			var blade_pos = blade.global_position
			_aim_arrow.global_position = blade_pos + _last_shot_dir * (aim_arrow_length / 2.0)
			_aim_arrow.global_position.y = 0.1
			_aim_arrow.global_rotation.y = atan2(_last_shot_dir.x, _last_shot_dir.z)
		else:
			_aim_arrow.visible = false
	
	elif (_wound_up or _shot_buffering) and _last_shot_dir != Vector3.ZERO:
		if puck.carrier == self:
			var power: float
			var elevate: float
			if _wound_up:
				var t = clampf(_windup_timer / max_windup_time, 0.0, 1.0)
				power = lerpf(min_shot_power, max_shot_power, t)
				elevate = slapper_elevate_force
			else:
				power = min_shot_power
				elevate = wrister_elevate_force
			var shot_dir = _last_shot_dir
			if _input.elevate:
				shot_dir.y = elevate
			puck.release(shot_dir, power)
		_wound_up = false
		_shot_buffering = false
		_windup_timer = 0.0
		_aim_arrow.visible = false
