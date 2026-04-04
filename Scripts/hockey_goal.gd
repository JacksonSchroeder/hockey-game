@tool
class_name HockeyGoal
extends StaticBody3D

@export var goal_width: float = 1.83:
	set(v):
		goal_width = v
		_rebuild()
@export var goal_height: float = 1.22:
	set(v):
		goal_height = v
		_rebuild()
@export var goal_depth: float = 1.0:
	set(v):
		goal_depth = v
		_rebuild()
@export var post_thickness: float = 0.05:
	set(v):
		post_thickness = v
		_rebuild()
@export var distance_from_end: float = 3.4:
	set(v):
		distance_from_end = v
		_rebuild()
@export var rink_length: float = 60.0:
	set(v):
		rink_length = v
		_rebuild()
@export var goal_color: Color = Color(0.8, 0.1, 0.1):
	set(v):
		goal_color = v
		_rebuild()
@export var rebuild: bool = false:
	set(v):
		_rebuild()

func _ready() -> void:
	_rebuild()

func _rebuild() -> void:
	for child in get_children():
		child.queue_free()
	
	var half_l = rink_length / 2.0
	var half_w = goal_width / 2.0
	var t = post_thickness
	
	var goal_positions = [
		half_l - distance_from_end,
		-(half_l - distance_from_end),
	]
	
	for goal_z in goal_positions:
		var facing = sign(goal_z)
		
		# Left post
		_add_post(
			Vector3(-half_w, goal_height / 2.0, goal_z),
			Vector3(t, goal_height, t)
		)
		# Right post
		_add_post(
			Vector3(half_w, goal_height / 2.0, goal_z),
			Vector3(t, goal_height, t)
		)
		# Crossbar
		_add_post(
			Vector3(0, goal_height, goal_z),
			Vector3(goal_width, t, t)
		)
		# Back bar bottom
		_add_post(
			Vector3(0, t / 2.0, goal_z + facing * goal_depth),
			Vector3(goal_width, t, t)
		)
		# Left side bar
		_add_post(
			Vector3(-half_w, t / 2.0, goal_z + facing * goal_depth / 2.0),
			Vector3(t, t, goal_depth)
		)
		# Right side bar
		_add_post(
			Vector3(half_w, t / 2.0, goal_z + facing * goal_depth / 2.0),
			Vector3(t, t, goal_depth)
		)
		# Back wall
		_add_post(
			Vector3(0, goal_height / 2.0, goal_z + facing * goal_depth),
			Vector3(goal_width, goal_height, t)
		)

func _add_post(pos: Vector3, size: Vector3) -> void:
	var mesh_instance = MeshInstance3D.new()
	var box = BoxMesh.new()
	box.size = size
	mesh_instance.mesh = box
	mesh_instance.position = pos
	var mat = StandardMaterial3D.new()
	mat.albedo_color = goal_color
	mesh_instance.material_override = mat
	add_child(mesh_instance)
	
	var col = CollisionShape3D.new()
	var shape = BoxShape3D.new()
	shape.size = size
	col.shape = shape
	col.position = pos
	add_child(col)
