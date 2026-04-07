class_name LocalClient
extends PlayerClient

var camera: GameCamera = null
var _gatherer: LocalInputGatherer = null
var _current_input: InputState = InputState.new()
var _input_timer: float = 0.0
var _sequence: int = 0

const INPUT_DELTA: float = 1.0 / 60.0

func _ready() -> void:
	camera = $Camera3D

func setup(assigned_skater: SkaterController, puck: Puck) -> void:
	super.setup(assigned_skater, puck)
	camera.skater = skater
	camera.puck = puck
	_gatherer = LocalInputGatherer.new(camera)
	add_child(_gatherer)

func get_input() -> InputState:
	return _current_input

func _process(delta: float) -> void:
	if _gatherer == null:
		return
	_input_timer += delta
	if _input_timer >= INPUT_DELTA:
		_input_timer -= INPUT_DELTA
		_sequence += 1
		var state: InputState = _gatherer.gather()
		state.sequence = _sequence
		state.delta = INPUT_DELTA
		_current_input = state
		# TODO: send to server here
