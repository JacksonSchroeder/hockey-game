extends Node

# ── Scenes ────────────────────────────────────────────────────────────────────
const PUCK_SCENE: PackedScene = preload("res://Scenes/Puck.tscn")
const SKATER_SCENE: PackedScene = preload("res://Scenes/Skater.tscn")
const GOALIE_SCENE: PackedScene = preload("res://Scenes/Goalie.tscn")
const LOCAL_CLIENT_SCENE: PackedScene = preload("res://Scenes/LocalClient.tscn")

# ── Tuning ────────────────────────────────────────────────────────────────────
const TOP_GOALIE_POS: Vector3 = Vector3(0, 1, -24)
const BOTTOM_GOALIE_POS: Vector3 = Vector3(0, 1, 24)
const PUCK_START_POS: Vector3 = Vector3(0, 0.05, -0)
const SKATER_START_POS: Vector3 = Vector3(3, 1, -10)

# ── Game State ────────────────────────────────────────────────────────────────
var puck: Puck = null
var skaters: Array[SkaterController] = []
var goalies: Array = []
var local_client = null

func _ready() -> void:
	_spawn_puck()
	_spawn_goalies()
	_spawn_local_player()

# ── Spawning ──────────────────────────────────────────────────────────────────
func _spawn_puck() -> void:
	puck = PUCK_SCENE.instantiate()
	add_child(puck)
	puck.global_position = PUCK_START_POS

func _spawn_goalies() -> void:
	var top: = GOALIE_SCENE.instantiate()
	var bottom: = GOALIE_SCENE.instantiate()
	top.global_position = TOP_GOALIE_POS
	bottom.global_position = BOTTOM_GOALIE_POS
	bottom.rotation.y = PI
	top.puck = puck
	bottom.puck = puck
	add_child(top)
	add_child(bottom)
	goalies.append(top)
	goalies.append(bottom)

func _spawn_local_player() -> void:
	var skater: SkaterController = SKATER_SCENE.instantiate()
	skater.global_position = SKATER_START_POS
	skater.puck = puck
	add_child(skater)
	skaters.append(skater)

	local_client = LOCAL_CLIENT_SCENE.instantiate()
	add_child(local_client)
	local_client.setup(skater, puck)

# ── Accessors ─────────────────────────────────────────────────────────────────
func get_puck() -> Puck:
	return puck

func get_local_skater() -> SkaterController:
	if skaters.size() > 0:
		return skaters[0]
	return null
