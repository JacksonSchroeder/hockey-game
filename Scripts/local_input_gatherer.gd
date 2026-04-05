class_name LocalInputGatherer
extends Node

func gather() -> InputState:
	var state = InputState.new()
	
	state.move_vector = Input.get_vector("move_left", "move_right", "move_up", "move_down")
	state.blade_vector = Input.get_vector("blade_left", "blade_right", "blade_up", "blade_down")
	state.shoot_held = Input.is_action_pressed("shoot")
	state.shoot_pressed = Input.is_action_just_pressed("shoot")
	state.orientation = Input.is_action_pressed("orientation")
	state.dash = Input.is_action_just_pressed("dash")
	state.ability = Input.is_action_just_pressed("ability")
	state.reset = Input.is_action_just_pressed("reset")
	state.self_pass = Input.is_action_just_pressed("self_pass")
	state.shot_cancel = Input.is_action_pressed("shot_cancel")
	state.elevate = Input.is_action_pressed("elevate")
	state.brake = Input.is_action_pressed("brake")
	state.quick_shot = Input.is_action_pressed("quick_shot")
	return state
