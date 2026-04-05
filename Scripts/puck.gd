class_name Puck
extends RigidBody3D

@export var max_speed: float = 30.0
@export var reattach_cooldown: float = 0.5
@export var ice_height: float = 0.05

var carrier: Node3D = null
var _cooldown_timer: float = 0.0

func _ready() -> void:
	$PickupZone.area_entered.connect(_on_blade_entered)

func _on_blade_entered(area: Area3D) -> void:
	if carrier != null:
		return
	if _cooldown_timer > 0.0:
		return
	var node = area
	while node and not node is SkaterController:
		node = node.get_parent()
	if node:
		carrier = node

func release(direction: Vector3, power: float) -> void:
	carrier = null
	freeze = false
	if direction.y > 0:
		position.y = ice_height + 0.1
	linear_velocity = direction * power
	_cooldown_timer = reattach_cooldown

func reset() -> void:
	carrier = null
	freeze = false
	linear_velocity = Vector3.ZERO
	angular_velocity = Vector3.ZERO
	global_position = Vector3(0, ice_height, 0)
	_cooldown_timer = 0.0

func _is_airborne() -> bool:
	return position.y > ice_height + 0.05

func _physics_process(delta: float) -> void:
	if carrier != null:
		freeze = true
		var blade_node = carrier.get_node("UpperBody/Blade")
		global_position = blade_node.global_position
		global_position.y = ice_height
	else:
		freeze = false
		if _cooldown_timer > 0.0:
			_cooldown_timer -= delta
		if linear_velocity.length() > max_speed:
			linear_velocity = linear_velocity.normalized() * max_speed
		
		if _is_airborne():
			# Let physics handle it, just cap speed
			pass
		else:
			# On ice: clamp to surface
			linear_velocity.y = 0.0
			position.y = ice_height
