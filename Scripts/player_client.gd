class_name PlayerClient
extends Node

var skater: SkaterController = null

func get_input() -> InputState:
	return InputState.new()

func setup(assigned_skater: SkaterController, puck: Puck) -> void:
	skater = assigned_skater
	skater.client = self
	skater.initialize(puck)
