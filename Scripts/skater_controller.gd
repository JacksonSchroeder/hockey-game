class_name SkaterController
extends CharacterBody3D

# ── State Machine ─────────────────────────────────────────────────────────────
enum State {
	SKATING,
	SHOOT_IDLE,
	WRISTER_AIM,
	SLAPPER_CHARGE,
	FOLLOW_THROUGH,
}

# ── Tuning ────────────────────────────────────────────────────────────────────
@export var thrust: float = 20.0
@export var friction: float = 5.0
@export var max_speed: float = 10.0
@export var rotation_speed: float = 6.0
@export var dash_force: float = 15.0
@export var dash_cooldown: float = 1.0
@export var dash_duration: float = 0.2
@export var stick_reach: float = 1.5
@export var blade_speed: float = 8.0
@export var blade_height: float = 0.0
@export var puck: Puck
@export var min_shot_power: float = 15.0
@export var max_shot_power: float = 25.0
@export var max_windup_time: float = 1.0
@export var aim_arrow_length: float = 3.0
@export var aim_arrow_thickness: float = 0.05
@export var shoot_mode_thrust_multiplier: float = 0.2
@export var shot_deadzone: float = 0.1
@export var wrister_elevate_force: float = 0.3
@export var slapper_elevate_force: float = 0.1
@export var brake_multiplier: float = 5.0
@export var min_reach: float = 0.65
@export var follow_through_duration: float = 0.15
@export var upper_body_aim_speed: float = 10.0
@export var is_left_handed: bool = true
@export var forehand_wrister_angle: float = -40.0  # degrees left of forward for lefty
@export var backhand_wrister_angle: float = 30.0   # degrees right of forward for lefty
@export var slapper_windup_angle: float = -80.0     # further back on forehand side
@export var forehand_wrister_power: float = 12.0
@export var backhand_wrister_power: float = 7.0
@export var quick_shot_power: float = 12

# ── Node References ───────────────────────────────────────────────────────────
@onready var lower_body: Node3D = $LowerBody
@onready var upper_body: Node3D = $UpperBody
@onready var blade: Marker3D = $UpperBody/Blade
@onready var stick_raycast: RayCast3D = $StickRaycast
@onready var stick_mesh: MeshInstance3D = $UpperBody/StickMesh

# ── State ─────────────────────────────────────────────────────────────────────
var _input: InputState
var _gatherer: LocalInputGatherer
var _state: State = State.SKATING
var _facing: Vector2 = Vector2.DOWN
var _is_backward: bool = false
var _dash_timer: float = 0.0
var _dash_active_timer: float = 0.0
var _current_blade_dir: Vector3 = Vector3(0, 0, -1)
var _windup_timer: float = 0.0
var _last_shot_dir: Vector3 = Vector3.ZERO
var _follow_through_timer: float = 0.0
var _upper_body_angle: float = 0.0
var _aim_arrow: MeshInstance3D = null
var _shot_is_forehand: bool = true
var _prev_blade_world_pos: Vector3 = Vector3.ZERO

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
	_apply_state(delta)
	_apply_blade(delta)
	_apply_upper_body(delta)
	_update_stick_mesh()
	move_and_slide()

# ── State Machine ─────────────────────────────────────────────────────────────
func _apply_state(delta: float) -> void:
	match _state:
		State.SKATING:
			_state_skating()
		State.SHOOT_IDLE:
			_state_shoot_idle(delta)
		State.WRISTER_AIM:
			_state_wrister_aim(delta)
		State.SLAPPER_CHARGE:
			_state_slapper_charge(delta)
		State.FOLLOW_THROUGH:
			_state_follow_through(delta)

func _state_skating() -> void:
	_aim_arrow.visible = false
	if _input.shoot_pressed:
		_state = State.SHOOT_IDLE
		_windup_timer = 0.0
		_last_shot_dir = Vector3.ZERO
		_shot_is_forehand = _is_forehand()

func _state_shoot_idle(delta: float) -> void:
	# Cancel
	if _input.shot_cancel:
		_transition_to_skating()
		return
	
	# Release trigger
	if not _input.shoot_held:
		_transition_to_skating()
		return
	
	var stick = _input.blade_vector
	if stick.length() > 0.2:
		var screen_dir = Vector3(stick.x, 0, stick.y)
		var local_dir = global_transform.basis.inverse() * screen_dir
		
		if local_dir.z > shot_deadzone:
			_state = State.SLAPPER_CHARGE
			_shot_is_forehand = true  # Slappers are always forehand
			_last_shot_dir = (-screen_dir).normalized()
		elif local_dir.z < -shot_deadzone:
			_state = State.WRISTER_AIM
			# _shot_is_forehand already set from shoot mode entry
			_last_shot_dir = screen_dir.normalized()
	
	_aim_arrow.visible = false

func _state_wrister_aim(delta: float) -> void:
	# Cancel
	if _input.shot_cancel:
		_transition_to_skating()
		return
	
	# Release trigger
	if not _input.shoot_held:
		_transition_to_skating()
		return
	
	var stick = _input.blade_vector
	
	# Update aim direction (only in forward hemisphere)
	if stick.length() > 0.2:
		var screen_dir = Vector3(stick.x, 0, stick.y)
		var local_dir = global_transform.basis.inverse() * screen_dir
		if local_dir.z < 0.0:
			_last_shot_dir = screen_dir.normalized()
	
	# Release shot on stick returning to deadzone
	if stick.length() <= 0.2 and _last_shot_dir != Vector3.ZERO:
		var power = forehand_wrister_power if _shot_is_forehand else backhand_wrister_power
		_release_shot(power, wrister_elevate_force)
		return
	
	# Show aim arrow
	_update_aim_arrow()

func _state_slapper_charge(delta: float) -> void:
	# Cancel
	if _input.shot_cancel:
		_transition_to_skating()
		return
	
	# Release trigger
	if not _input.shoot_held:
		_transition_to_skating()
		return
	
	_windup_timer += delta
	
	var stick = _input.blade_vector
	
	# Update aim direction (only in backward hemisphere, inverted)
	if stick.length() > 0.2:
		var screen_dir = Vector3(stick.x, 0, stick.y)
		var local_dir = global_transform.basis.inverse() * screen_dir
		if local_dir.z > 0.0:
			_last_shot_dir = (-screen_dir).normalized()
	
	# Release shot on stick returning to deadzone
	if stick.length() <= 0.2 and _last_shot_dir != Vector3.ZERO:
		var t = clampf(_windup_timer / max_windup_time, 0.0, 1.0)
		var power = lerpf(min_shot_power, max_shot_power, t)
		_release_shot(power, slapper_elevate_force)
		return
	
	# Show aim arrow
	_update_aim_arrow()

func _state_follow_through(delta: float) -> void:
	_follow_through_timer -= delta
	_aim_arrow.visible = false
	if _follow_through_timer <= 0.0:
		_transition_to_skating()

# ── State Helpers ─────────────────────────────────────────────────────────────
func _transition_to_skating() -> void:
	_state = State.SKATING
	_windup_timer = 0.0
	_last_shot_dir = Vector3.ZERO
	_aim_arrow.visible = false

func _release_shot(power: float, elevate_force: float) -> void:
	print("release! dir: ", _last_shot_dir, " power: ", power, " carrier: ", puck.carrier == self)
	if puck.carrier == self:
		var shot_dir = _last_shot_dir
		if _input.elevate:
			shot_dir.y = elevate_force
		puck.release(shot_dir, power)
	_state = State.FOLLOW_THROUGH
	_follow_through_timer = follow_through_duration

func _is_in_shoot_state() -> bool:
	return _state in [State.SHOOT_IDLE, State.WRISTER_AIM, State.SLAPPER_CHARGE, State.FOLLOW_THROUGH]

func _update_aim_arrow() -> void:
	if _last_shot_dir == Vector3.ZERO:
		_aim_arrow.visible = false
		return
	_aim_arrow.visible = true
	var blade_pos = blade.global_position
	_aim_arrow.global_position = blade_pos + _last_shot_dir * (aim_arrow_length / 2.0)
	_aim_arrow.global_position.y = 0.1
	_aim_arrow.global_rotation.y = atan2(_last_shot_dir.x, _last_shot_dir.z)

# ── Blade ─────────────────────────────────────────────────────────────────────
func _apply_blade(delta: float) -> void:
	if _is_in_shoot_state():
		_apply_shoot_blade_animation(delta)
		_prev_blade_world_pos = blade.global_position
		return
	
	var stick = _input.blade_vector
	
	var target_dir: Vector3
	if stick.length() < 0.1:
		target_dir = _current_blade_dir
	else:
		var screen_dir = Vector3(stick.x, 0, stick.y)
		var local_dir = upper_body.global_transform.basis.inverse() * screen_dir
		local_dir.y = 0
		target_dir = local_dir.normalized()
		
		var forward = Vector3(0, 0, -1)
		var angle = forward.signed_angle_to(target_dir, Vector3.UP)
		if abs(angle) > PI / 2.0:
			if angle > 0:
				target_dir = Vector3(-1, 0, 0)
			else:
				target_dir = Vector3(1, 0, 0)
	
	_current_blade_dir = _current_blade_dir.lerp(target_dir, blade_speed * delta).normalized()
	
	stick_raycast.target_position = _current_blade_dir * stick_reach
	stick_raycast.force_raycast_update()
	
	var reach = stick_reach
	if stick_raycast.is_colliding():
		var hit_dist = global_position.distance_to(stick_raycast.get_collision_point())
		if hit_dist < stick_reach:
			reach = max(hit_dist - 0.05, 0.1)
	
	if reach < min_reach and puck.carrier == self:
		var nudge = global_transform.basis * (-_current_blade_dir)
		puck.release(nudge.normalized(), 3.0)
	
	blade.position = _current_blade_dir * reach
	blade.position.y = blade_height
	blade.look_at(upper_body.global_position, Vector3.UP)
	
	# Quick shot - after blade position is updated
	if _input.quick_shot and puck.carrier == self:
		var blade_world_velocity = blade.global_position - _prev_blade_world_pos
		blade_world_velocity.y = 0.0
		if blade_world_velocity.length() > 0.001:
			puck.release(blade_world_velocity.normalized(), quick_shot_power)
	
	_prev_blade_world_pos = blade.global_position

# ── Upper Body ────────────────────────────────────────────────────────────────
func _apply_upper_body(delta: float) -> void:
	var target_offset = 0.0
	
	if _is_in_shoot_state() and _last_shot_dir != Vector3.ZERO:
		var world_aim_angle = atan2(-_last_shot_dir.x, -_last_shot_dir.z)
		target_offset = angle_difference(rotation.y, world_aim_angle)
	
	_upper_body_angle = lerp_angle(_upper_body_angle, target_offset, upper_body_aim_speed * delta)
	upper_body.rotation.y = _upper_body_angle
	#
	#if _is_in_shoot_state() and _last_shot_dir != Vector3.ZERO:
		#var world_aim_angle = atan2(_last_shot_dir.x, _last_shot_dir.z)
		#target_offset = angle_difference(rotation.y, world_aim_angle)
	#
	#_upper_body_angle = lerp_angle(_upper_body_angle, target_offset, upper_body_aim_speed * delta)
	#upper_body.rotation.y = _upper_body_angle

# ── Facing ────────────────────────────────────────────────────────────────────
func _apply_facing(delta: float) -> void:
	if _is_in_shoot_state():
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
	
	var facing_angle = atan2(-_facing.x, -_facing.y)
	rotation.y = facing_angle
	lower_body.rotation.y = 0.0
	_upper_body_angle = 0.0

# ── Movement ──────────────────────────────────────────────────────────────────
func _apply_movement(delta: float) -> void:
	var move = _input.move_vector
	
	if move.length() > 0.0:
		var thrust_dir = Vector3(move.x, 0, move.y)
		var current_thrust = thrust * shoot_mode_thrust_multiplier if _is_in_shoot_state() else thrust
		velocity += thrust_dir * current_thrust * delta
	
	var horizontal_vel = Vector2(velocity.x, velocity.z)
	var current_friction = friction * brake_multiplier if _input.brake else friction
	horizontal_vel = horizontal_vel.move_toward(Vector2.ZERO, current_friction * delta)
	velocity.x = horizontal_vel.x
	velocity.z = horizontal_vel.y
	
	var speed = Vector2(velocity.x, velocity.z).length()
	if speed > max_speed:
		var capped = Vector2(velocity.x, velocity.z).normalized() * max_speed
		velocity.x = capped.x
		velocity.z = capped.y

# ── Dash ──────────────────────────────────────────────────────────────────────
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

# ── Stick ──────────────────────────────────────────────────────────────────────
func _update_stick_mesh() -> void:
	var to_blade = blade.position
	stick_mesh.position = to_blade / 2.0
	stick_mesh.scale.z = to_blade.length()
	stick_mesh.look_at(upper_body.to_global(to_blade), Vector3.UP)
	
	
func _apply_shoot_blade_animation(delta: float) -> void:
	var target_angle_deg: float = 0.0
	
	match _state:
		State.SHOOT_IDLE:
			return
		State.WRISTER_AIM:
			if _shot_is_forehand:
				target_angle_deg = forehand_wrister_angle
			else:
				target_angle_deg = backhand_wrister_angle
		State.SLAPPER_CHARGE:
			target_angle_deg = slapper_windup_angle
		State.FOLLOW_THROUGH:
			return
	
	if not is_left_handed:
		target_angle_deg = -target_angle_deg
	
	var angle_rad = deg_to_rad(target_angle_deg)
	var target_dir = Vector3(sin(angle_rad), 0, -cos(angle_rad))
	
	_current_blade_dir = _current_blade_dir.lerp(target_dir, blade_speed * delta).normalized()
	blade.position = _current_blade_dir * stick_reach
	blade.position.y = blade_height


func _is_forehand() -> bool:
	var hand_sign = -1.0 if is_left_handed else 1.0
	return sign(_current_blade_dir.x) == sign(hand_sign)
