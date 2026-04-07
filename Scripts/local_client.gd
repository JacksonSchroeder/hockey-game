extends Node

var skater: SkaterController = null
var camera: GameCamera = null

func _ready() -> void:
	camera = $Camera3D

func setup(assigned_skater: SkaterController, puck: Puck) -> void:
	skater = assigned_skater
	camera.skater = skater
	camera.puck = puck
	var gatherer = LocalInputGatherer.new(camera)
	skater.add_child(gatherer)
	skater._gatherer = gatherer
