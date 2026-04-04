class_name Goalie
extends StaticBody3D

@export var puck: Puck
@export var reaction_speed: float = 3.0
@export var max_slide_range: float = 0.7
@export var net_depth: float = 1.0

var _home_position: Vector3
var _target_x: float = 0.0

func _ready() -> void:
	_home_position = global_position

func _physics_process(delta: float) -> void:
	var facing = sign(_home_position.z)
	var net_center = _home_position + Vector3(0, 0, facing * net_depth)
	var puck_pos = puck.global_position
	
	var dir = (net_center - puck_pos).normalized()
	if abs(dir.z) > 0.001:
		var t = (_home_position.z - puck_pos.z) / dir.z
		_target_x = puck_pos.x + dir.x * t
	
	_target_x = clampf(_target_x, _home_position.x - max_slide_range, _home_position.x + max_slide_range)
	
	var current_x = global_position.x
	var new_x = lerpf(current_x, _target_x, reaction_speed * delta)
	
	global_position.x = new_x
	global_position.y = _home_position.y
	global_position.z = _home_position.z
