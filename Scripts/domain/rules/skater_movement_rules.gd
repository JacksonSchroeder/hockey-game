class_name SkaterMovementRules

# Pure movement math extracted from SkaterController._apply_movement.
# Takes current state + input + tuning config, returns the new velocity.
# The caller (SkaterController) still owns the state machine guard (slapper
# charge windup, etc.); this function just does the physics.
#
# Config dict keys (all floats):
#   thrust                       — forward thrust magnitude
#   friction                     — base friction applied each tick
#   max_speed                    — maximum horizontal speed
#   move_deadzone                — stick deadzone
#   brake_multiplier             — friction multiplier when braking
#   puck_carry_speed_multiplier  — max speed reduction while carrying
#   backward_thrust_multiplier   — thrust scale when moving against facing
#   crossover_thrust_multiplier  — thrust scale when moving perpendicular to facing
#   dash_impulse_magnitude       — speed added per pulse dash
static func apply_movement(
		current_velocity: Vector3,
		move_input: Vector2,
		facing_rotation_y: float,
		has_puck: bool,
		brake: bool,
		delta: float,
		cfg: Dictionary) -> Vector3:
	var velocity: Vector3 = current_velocity

	if move_input.length() > cfg.move_deadzone:
		var thrust_dir := Vector3(move_input.x, 0.0, move_input.y)
		var facing_dir := Vector2(-sin(facing_rotation_y), -cos(facing_rotation_y))
		var move_dot: float = facing_dir.dot(move_input.normalized())

		var thrust_scale: float
		if move_dot >= 0.0:
			thrust_scale = lerpf(cfg.crossover_thrust_multiplier, 1.0, move_dot)
		else:
			thrust_scale = lerpf(cfg.backward_thrust_multiplier, cfg.crossover_thrust_multiplier, move_dot + 1.0)

		velocity += thrust_dir * cfg.thrust * thrust_scale * delta

		# Speed cap — but preserve over-max speed from external sources (body
		# check boost, etc.) so we don't instantly clamp a legitimate momentum gain.
		var effective_max: float = cfg.max_speed * cfg.puck_carry_speed_multiplier if has_puck else cfg.max_speed
		var horiz := Vector2(velocity.x, velocity.z)
		var speed: float = horiz.length()
		if speed > effective_max:
			var pre_thrust_speed: float = Vector2(
				velocity.x - thrust_dir.x * cfg.thrust * thrust_scale * delta,
				velocity.z - thrust_dir.z * cfg.thrust * thrust_scale * delta
			).length()
			var target_speed: float = maxf(pre_thrust_speed, effective_max)
			if speed > target_speed:
				var limited: Vector2 = horiz.normalized() * target_speed
				velocity.x = limited.x
				velocity.z = limited.y

	# Friction (or braking)
	var current_friction: float = cfg.friction * cfg.brake_multiplier if brake else cfg.friction
	var horiz_vel := Vector2(velocity.x, velocity.z)
	horiz_vel = horiz_vel.move_toward(Vector2.ZERO, current_friction * delta)
	velocity.x = horiz_vel.x
	velocity.z = horiz_vel.y
	return velocity

# Pulse dash — additive impulse in dash_dir, capped at effective_max.
# Same cap philosophy as apply_movement's thrust block: preserves pre-existing
# over-max speed (body check, etc.) but prevents free acceleration when already
# at max (same-direction spamming has no effect).
#
# Config keys used:
#   dash_impulse_magnitude       — speed added per dash (m/s)
#   max_speed                    — horizontal speed ceiling
#   puck_carry_speed_multiplier  — applied to ceiling when carrying
static func apply_dash_impulse(
		current_velocity: Vector3,
		dash_dir: Vector3,
		has_puck: bool,
		cfg: Dictionary) -> Vector3:
	var velocity: Vector3 = current_velocity
	var impulse: Vector3 = dash_dir.normalized() * cfg.dash_impulse_magnitude
	velocity += impulse

	var effective_max: float = cfg.max_speed * cfg.puck_carry_speed_multiplier if has_puck else cfg.max_speed
	var horiz := Vector2(velocity.x, velocity.z)
	var speed: float = horiz.length()
	if speed > effective_max:
		var pre_impulse_speed: float = Vector2(
			velocity.x - impulse.x, velocity.z - impulse.z).length()
		var target_speed: float = maxf(pre_impulse_speed, effective_max)
		if speed > target_speed:
			var limited: Vector2 = horiz.normalized() * target_speed
			velocity.x = limited.x
			velocity.z = limited.y
	return velocity
