class_name SkaterController
extends CharacterBody3D

# ── State Machine ─────────────────────────────────────────────────────────────
enum State {
	SKATING_WITHOUT_PUCK,
	SKATING_WITH_PUCK,
	SHOOT_IDLE,
	WRISTER_AIM,
	SLAPPER_CHARGE,
	FOLLOW_THROUGH,
}

# ── Movement Tuning ──────────────────────────────────────────────────────────
@export var thrust: float = 20.0
@export var friction: float = 5.0
@export var max_speed: float = 10.0
@export var rotation_speed: float = 6.0
@export var move_deadzone: float = 0.1
@export var dash_force: float = 10.0
@export var dash_cooldown: float = 1.0
@export var dash_duration: float = 0.2
@export var brake_multiplier: float = 5.0
@export var shoot_mode_thrust_multiplier: float = 0.2

# ── Blade Tuning ─────────────────────────────────────────────────────────────
@export var blade_height: float = 0.0
@export var blade_move_speed: float = 12.0
@export var blade_return_speed: float = 8.0
@export var blade_deadzone: float = 0.1
@export var plane_reach: float = 1.5
@export var close_reach: float = 0.4
@export var shoulder_offset: float = 0.35
@export var backhand_behind_scale: float = 0.3
@export var forehand_behind_reach: float = 1.0
@export var wall_squeeze_threshold: float = 0.3
@export var world_forehand_limit: float = 135.0   # degrees from forward on forehand side
@export var world_backhand_limit: float = 90.0     # degrees from forward on backhand side

# ── Shooting Tuning ──────────────────────────────────────────────────────────
@export var min_shot_power: float = 15.0
@export var max_shot_power: float = 30.0
@export var max_windup_time: float = 1.0
@export var aim_arrow_length: float = 3.0
@export var aim_arrow_thickness: float = 0.05
@export var shot_deadzone: float = 0.1
@export var wrister_elevate_force: float = 0.3
@export var slapper_elevate_force: float = 0.1
@export var follow_through_duration: float = 0.15
@export var upper_body_aim_speed: float = 10.0
@export var forehand_wrister_power: float = 15.0
@export var backhand_wrister_power: float = 10.0
@export var quick_shot_power: float = 12

# ── Character ─────────────────────────────────────────────────────────────────
@export var is_left_handed: bool = true
@export var puck: Puck
@export var invert_plane: bool = false

# ── Node References ───────────────────────────────────────────────────────────
@onready var lower_body: Node3D = $LowerBody
@onready var upper_body: Node3D = $UpperBody
@onready var blade: Marker3D = $UpperBody/Blade
@onready var shoulder: Marker3D = $UpperBody/Shoulder
@onready var stick_raycast: RayCast3D = $StickRaycast
@onready var stick_mesh: MeshInstance3D = $UpperBody/StickMesh

# ── State ─────────────────────────────────────────────────────────────────────
var _input: InputState
var _gatherer: LocalInputGatherer
var _state: State = State.SKATING_WITHOUT_PUCK
var _facing: Vector2 = Vector2.DOWN
var _is_backward: bool = false
var _dash_timer: float = 0.0
var _dash_active_timer: float = 0.0
var _upper_body_angle: float = 0.0
var _aim_arrow: MeshInstance3D = null

# ── Shooting State ────────────────────────────────────────────────────────────
var _windup_timer: float = 0.0
var _last_shot_dir: Vector3 = Vector3.ZERO
var _follow_through_timer: float = 0.0
var _shot_is_forehand: bool = true

# ── Blade State ───────────────────────────────────────────────────────────────
var _blade_plane_pos: Vector2 = Vector2.ZERO
var _prev_blade_plane_pos: Vector2 = Vector2.ZERO
var _world_blade_target: Vector3 = Vector3.ZERO

func _ready() -> void:
	_gatherer = LocalInputGatherer.new()
	add_child(_gatherer)
	
	# Position shoulder based on handedness
	var hand_sign = -1.0 if is_left_handed else 1.0
	shoulder.position = Vector3(hand_sign * shoulder_offset, 0, 0)
	
	# Initialize world blade target to default forward position
	_world_blade_target = Vector3(shoulder.position.x, blade_height, -plane_reach * 0.7)
	
	# Aim arrow setup
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
	
	# Connect puck signals
	puck.puck_picked_up.connect(_on_puck_picked_up)
	puck.puck_released.connect(_on_puck_released)

func _physics_process(delta: float) -> void:
	_input = _gatherer.gather()
	
	if _input.reset:
		puck.reset()
	
	if _input.self_pass and puck.carrier == null:
		var dir = (global_position - puck.global_position).normalized()
		dir.y = 0.0
		puck.linear_velocity = dir * quick_shot_power
		
	if _input.self_shot and puck.carrier == null:
		var dir = (global_position - puck.global_position).normalized()
		dir.y = 0.0
		puck.linear_velocity = dir * max_shot_power
		
	if _input.ability:
		invert_plane = !invert_plane
	
	_apply_movement(delta)
	_apply_facing(delta)
	_apply_dash(delta)
	_apply_state(delta)
	_apply_upper_body(delta)
	_update_stick_mesh()
	move_and_slide()

# ── Puck Signals ──────────────────────────────────────────────────────────────
func _on_puck_picked_up(carrier: SkaterController) -> void:
	if carrier == self:
		_state = State.SKATING_WITH_PUCK
		# Map current blade position back to plane coordinates
		# so there's no visual jump
		var local_blade = blade.position - shoulder.position
		_blade_plane_pos.x = local_blade.x / plane_reach
		_blade_plane_pos.y = 0.0  # rough — could be smarter

func _on_puck_released() -> void:
	if _state == State.FOLLOW_THROUGH:
		return  # let follow through finish naturally
	if _state in [State.SKATING_WITH_PUCK, State.SHOOT_IDLE, State.WRISTER_AIM, State.SLAPPER_CHARGE]:
		_state = State.SKATING_WITHOUT_PUCK
		# Seed world blade target from current blade position
		_world_blade_target = blade.position

# ── State Machine ─────────────────────────────────────────────────────────────
func _apply_state(delta: float) -> void:
	match _state:
		State.SKATING_WITHOUT_PUCK:
			_state_skating_without_puck(delta)
		State.SKATING_WITH_PUCK:
			_state_skating_with_puck(delta)
		State.SHOOT_IDLE:
			_state_shoot_idle(delta)
		State.WRISTER_AIM:
			_state_wrister_aim(delta)
		State.SLAPPER_CHARGE:
			_state_slapper_charge(delta)
		State.FOLLOW_THROUGH:
			_state_follow_through(delta)

# ── Skating Without Puck ─────────────────────────────────────────────────────
func _state_skating_without_puck(delta: float) -> void:
	_aim_arrow.visible = false
	_apply_blade_world_relative(delta)

# ── Skating With Puck ────────────────────────────────────────────────────────
func _state_skating_with_puck(delta: float) -> void:
	_aim_arrow.visible = false
	_apply_blade_player_relative(delta)
	
	# Enter shoot mode
	if _input.shoot_pressed:
		_state = State.SHOOT_IDLE
		_windup_timer = 0.0
		_last_shot_dir = Vector3.ZERO
		var hand_sign = -1.0 if is_left_handed else 1.0
		_shot_is_forehand = sign(_blade_plane_pos.x) == sign(hand_sign) or _blade_plane_pos.x == 0.0

# ── Shoot Idle ────────────────────────────────────────────────────────────────
func _state_shoot_idle(delta: float) -> void:
	if _input.shot_cancel:
		_transition_to_skating()
		return
	
	if not _input.shoot_held:
		_transition_to_skating()
		return
	
	var stick = _input.blade_vector
	if stick.length() > 0.2:
		var screen_dir = Vector3(stick.x, 0, stick.y)
		var local_dir = global_transform.basis.inverse() * screen_dir
		
		if local_dir.z > shot_deadzone:
			_state = State.SLAPPER_CHARGE
			_shot_is_forehand = true
			_last_shot_dir = (-screen_dir).normalized()
		elif local_dir.z < -shot_deadzone:
			_state = State.WRISTER_AIM
			_last_shot_dir = screen_dir.normalized()
	
	_aim_arrow.visible = false

# ── Wrister Aim ───────────────────────────────────────────────────────────────
func _state_wrister_aim(delta: float) -> void:
	if _input.shot_cancel:
		_transition_to_skating()
		return
	
	if not _input.shoot_held:
		_transition_to_skating()
		return
	
	var stick = _input.blade_vector
	
	if stick.length() > 0.2:
		var screen_dir = Vector3(stick.x, 0, stick.y)
		var local_dir = global_transform.basis.inverse() * screen_dir
		if local_dir.z < 0.0:
			_last_shot_dir = screen_dir.normalized()
	
	if stick.length() <= 0.2 and _last_shot_dir != Vector3.ZERO:
		var power = forehand_wrister_power if _shot_is_forehand else backhand_wrister_power
		_release_shot(power, wrister_elevate_force)
		return
	
	_update_aim_arrow()

# ── Slapper Charge ────────────────────────────────────────────────────────────
func _state_slapper_charge(delta: float) -> void:
	if _input.shot_cancel:
		_transition_to_skating()
		return
	
	if not _input.shoot_held:
		_transition_to_skating()
		return
	
	_windup_timer += delta
	
	var stick = _input.blade_vector
	
	if stick.length() > 0.2:
		var screen_dir = Vector3(stick.x, 0, stick.y)
		var local_dir = global_transform.basis.inverse() * screen_dir
		if local_dir.z > 0.0:
			_last_shot_dir = (-screen_dir).normalized()
	
	if stick.length() <= 0.2 and _last_shot_dir != Vector3.ZERO:
		var t = clampf(_windup_timer / max_windup_time, 0.0, 1.0)
		var power = lerpf(min_shot_power, max_shot_power, t)
		_release_shot(power, slapper_elevate_force)
		return
	
	_update_aim_arrow()

# ── Follow Through ────────────────────────────────────────────────────────────
func _state_follow_through(delta: float) -> void:
	_follow_through_timer -= delta
	_aim_arrow.visible = false
	if _follow_through_timer <= 0.0:
		_transition_to_skating()

# ── State Helpers ─────────────────────────────────────────────────────────────
func _transition_to_skating() -> void:
	if puck.carrier == self:
		_state = State.SKATING_WITH_PUCK
	else:
		_state = State.SKATING_WITHOUT_PUCK
		_world_blade_target = blade.position
	_windup_timer = 0.0
	_last_shot_dir = Vector3.ZERO
	_aim_arrow.visible = false

func _release_shot(power: float, elevate_force: float) -> void:
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

# ── Blade: Player-Relative (With Puck) ───────────────────────────────────────
func _apply_blade_player_relative(delta: float) -> void:
	var stick = _input.blade_vector
	
	# Clamp stick to unit circle
	if stick.length() > 1.0:
		stick = stick.normalized()
	
	if stick.length() > blade_deadzone:
		_blade_plane_pos = _blade_plane_pos.lerp(stick, blade_move_speed * delta)
	else:
		_blade_plane_pos = _blade_plane_pos.lerp(Vector2.ZERO, blade_return_speed * delta)
	
	var local_pos = _remap_stick_to_blade(_blade_plane_pos)
	local_pos = _apply_wall_clamping(local_pos)
	
	blade.position = local_pos
	blade.look_at(upper_body.global_position, Vector3.UP)
	
	# Quick shot
	if _input.quick_shot and puck.carrier == self:
		var plane_delta = _blade_plane_pos - _prev_blade_plane_pos
		if plane_delta.length() > 0.001:
			var local_dir = Vector3(plane_delta.x, 0, plane_delta.y).normalized()
			var world_dir = global_transform.basis * local_dir
			world_dir.y = 0.0
			puck.release(world_dir.normalized(), quick_shot_power)
	
	_prev_blade_plane_pos = _blade_plane_pos

# ── Blade: World-Relative (Without Puck) ─────────────────────────────────────
func _apply_blade_world_relative(delta: float) -> void:
	var stick = _input.blade_vector
	
	# Clamp stick to unit circle
	if stick.length() > 1.0:
		stick = stick.normalized()
	
	if stick.length() > blade_deadzone:
		# Screen-space direction → local-space position
		var world_dir = Vector3(stick.x, 0, stick.y).normalized()
		var local_dir = global_transform.basis.inverse() * world_dir
		local_dir.y = 0.0
		local_dir = local_dir.normalized()
		
		# Clamp to reachable arc
		var forward = Vector3(0, 0, -1)
		var angle = forward.signed_angle_to(local_dir, Vector3.UP)
		var hand_sign = -1.0 if is_left_handed else 1.0
		
		var max_angle: float
		if sign(angle) == sign(hand_sign):
			max_angle = deg_to_rad(world_backhand_limit)
		else:
			max_angle = deg_to_rad(world_forehand_limit)
		
		if abs(angle) > max_angle:
			angle = sign(angle) * max_angle
			local_dir = forward.rotated(Vector3.UP, angle)
		
		var reach = stick.length() * plane_reach
		var target_pos = local_dir * reach
		target_pos += shoulder.position
		target_pos.y = blade_height
		_world_blade_target = _world_blade_target.lerp(target_pos, blade_move_speed * delta)
	else:
		# Return to default forward position relative to shoulder
		var default_pos = Vector3(shoulder.position.x, blade_height, -plane_reach * 0.7)
		_world_blade_target = _world_blade_target.lerp(default_pos, blade_return_speed * delta)
	
	var local_pos = _apply_wall_clamping(_world_blade_target)
	
	blade.position = local_pos
	blade.look_at(upper_body.global_position, Vector3.UP)

# ── Blade: Remap & Wall Clamping ─────────────────────────────────────────────
func _remap_stick_to_blade(stick_pos: Vector2) -> Vector3:
	var sx = stick_pos.x
	var sy = stick_pos.y
	
	if invert_plane:
		sx = -sx
		sy = -sy
	
	var hand_sign = -1.0 if is_left_handed else 1.0
	var is_backhand_side = sign(sx) != sign(hand_sign) and sx != 0.0
	var is_behind = sy > 0.0
	
	# Compress the backhand-behind quadrant
	if is_backhand_side and is_behind:
		var backhand_amount = abs(sx)
		var behind_amount = sy
		var blend = backhand_amount * behind_amount
		var blend_scale = lerpf(1.0, backhand_behind_scale, blend)
		sx *= blend_scale
		sy *= blend_scale
	
	# Lateral position from shoulder
	var lateral = sx * plane_reach + shoulder.position.x
	
	# Depth: forward/back
	var neutral_depth = plane_reach * 0.7
	var forward_extra = plane_reach * 0.3
	
	var depth: float
	if sy <= 0.0:
		depth = neutral_depth + (-sy * forward_extra)
	else:
		# Blend between feet (center/backhand) and behind (forehand)
		var forehand_amount = 0.0
		if sign(sx) == sign(hand_sign) and sx != 0.0:
			forehand_amount = abs(sx)
		
		var feet_depth = lerpf(neutral_depth, close_reach, sy)
		var behind_depth = lerpf(neutral_depth, -forehand_behind_reach, sy)
		depth = lerpf(feet_depth, behind_depth, forehand_amount)
	
	return Vector3(lateral, blade_height, -depth)

func _apply_wall_clamping(local_pos: Vector3) -> Vector3:
	var intended_pos = local_pos
	var to_blade = local_pos
	to_blade.y = 0.0
	stick_raycast.target_position = to_blade
	stick_raycast.force_raycast_update()
	
	if stick_raycast.is_colliding():
		var hit_dist = global_position.distance_to(stick_raycast.get_collision_point())
		var blade_dist = to_blade.length()
		if hit_dist < blade_dist:
			var clamped_dist = max(hit_dist - 0.05, 0.1)
			local_pos = to_blade.normalized() * clamped_dist
			local_pos.y = blade_height
	
	# Release puck if wall is squeezing the blade significantly
	if puck.carrier == self:
		var squeeze = intended_pos.length() - local_pos.length()
		if squeeze > wall_squeeze_threshold:
			var wall_normal = stick_raycast.get_collision_normal()
			if wall_normal.length() > 0.0:
				puck.release(wall_normal.normalized(), 3.0)
			else:
				var nudge = global_transform.basis * (-to_blade.normalized())
				puck.release(nudge.normalized(), 3.0)
	
	return local_pos

# ── Upper Body ────────────────────────────────────────────────────────────────
func _apply_upper_body(delta: float) -> void:
	var target_offset = 0.0
	
	if _is_in_shoot_state() and _last_shot_dir != Vector3.ZERO:
		var world_aim_angle = atan2(-_last_shot_dir.x, -_last_shot_dir.z)
		target_offset = angle_difference(rotation.y, world_aim_angle)
	
	_upper_body_angle = lerp_angle(_upper_body_angle, target_offset, upper_body_aim_speed * delta)
	upper_body.rotation.y = _upper_body_angle

# ── Facing ────────────────────────────────────────────────────────────────────
func _apply_facing(delta: float) -> void:
	if _is_in_shoot_state():
		return
	if _dash_active_timer > 0.0:
		return
	var move = _input.move_vector
	_is_backward = _input.orientation
	
	var target_facing = _facing
	
	if move.length() > move_deadzone:
		target_facing = move.normalized()
		if _is_backward:
			target_facing = -target_facing
	elif _is_backward:
		target_facing = -_facing
	
	if target_facing != _facing:
		_facing = _facing.lerp(target_facing, rotation_speed * delta).normalized()
	
	var facing_angle = atan2(-_facing.x, -_facing.y)
	rotation.y = facing_angle
	lower_body.rotation.y = 0.0
	_upper_body_angle = 0.0

# ── Movement ──────────────────────────────────────────────────────────────────
func _apply_movement(delta: float) -> void:
	var move = _input.move_vector
	
	if move.length() > move_deadzone:
		var thrust_dir = Vector3(move.x, 0, move.y)
		var current_thrust = thrust * shoot_mode_thrust_multiplier if _is_in_shoot_state() else thrust
		velocity += thrust_dir * current_thrust * delta
		var speed = Vector2(velocity.x, velocity.z).length()
		if speed > max_speed:
			var pre_thrust_speed = Vector2(velocity.x - thrust_dir.x * current_thrust * delta, velocity.z - thrust_dir.z * current_thrust * delta).length()
			var target_speed = maxf(pre_thrust_speed, max_speed)
			if speed > target_speed:
				var limited = Vector2(velocity.x, velocity.z).normalized() * target_speed
				velocity.x = limited.x
				velocity.z = limited.y
	
	var horizontal_vel = Vector2(velocity.x, velocity.z)
	var current_friction = friction * brake_multiplier if _input.brake else friction
	horizontal_vel = horizontal_vel.move_toward(Vector2.ZERO, current_friction * delta)
	velocity.x = horizontal_vel.x
	velocity.z = horizontal_vel.y

# ── Dash ──────────────────────────────────────────────────────────────────────
func _apply_dash(delta: float) -> void:
	_dash_timer -= delta
	_dash_active_timer -= delta
	
	if _input.dash and _dash_timer <= 0.0:
		var move = _input.move_vector
		if move.length() > move_deadzone:
			var dash_dir = Vector3(move.x, 0, move.y).normalized()
			velocity += dash_dir * dash_force
			_dash_timer = dash_cooldown
			_dash_active_timer = dash_duration

# ── Stick Mesh ────────────────────────────────────────────────────────────────
func _update_stick_mesh() -> void:
	var stick_origin = shoulder.position
	var to_blade = blade.position - stick_origin
	stick_mesh.position = stick_origin + to_blade / 2.0
	stick_mesh.scale.z = to_blade.length()
	stick_mesh.look_at(upper_body.to_global(blade.position), Vector3.UP)
