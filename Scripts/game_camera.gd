class_name GameCamera
extends Camera3D

@export var skater: Node3D
@export var puck: Node3D
@export var min_height: float = 15.0
@export var max_height: float = 30.0
@export var padding: float = 5.0
@export var smooth_speed: float = 5.0

func _physics_process(delta: float) -> void:
	var targets: Array[Node3D] = []
	if skater:
		targets.append(skater)
	if puck:
		targets.append(puck)
	
	if targets.is_empty():
		return
	
	# Find center and bounds
	var min_pos = Vector2(targets[0].global_position.x, targets[0].global_position.z)
	var max_pos = min_pos
	var center = Vector3.ZERO
	
	for t in targets:
		center += t.global_position
		min_pos.x = min(min_pos.x, t.global_position.x)
		min_pos.y = min(min_pos.y, t.global_position.z)
		max_pos.x = max(max_pos.x, t.global_position.x)
		max_pos.y = max(max_pos.y, t.global_position.z)
	
	center /= targets.size()
	
	# Zoom based on spread
	var spread = max(max_pos.x - min_pos.x, max_pos.y - min_pos.y) + padding
	var target_height = clampf(spread * 1.5, min_height, max_height)
	
	var target_pos = Vector3(center.x, target_height, center.z)
	global_position = global_position.lerp(target_pos, smooth_speed * delta)
