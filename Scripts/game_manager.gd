extends Node

# ── Scenes ────────────────────────────────────────────────────────────────────
const PUCK_SCENE: PackedScene = preload("res://Scenes/Puck.tscn")
const SKATER_SCENE: PackedScene = preload("res://Scenes/Skater.tscn")
const GOALIE_SCENE: PackedScene = preload("res://Scenes/Goalie.tscn")
const LOCAL_CLIENT_SCENE: PackedScene = preload("res://Scenes/LocalClient.tscn")

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
	puck.position = Constants.PUCK_START_POS

func _spawn_goalies() -> void:
	var top: = GOALIE_SCENE.instantiate()
	var bottom: = GOALIE_SCENE.instantiate()
	top.puck = puck
	bottom.puck = puck
	top.position = Constants.TOP_GOALIE_POS
	bottom.position = Constants.BOTTOM_GOALIE_POS
	bottom.rotation.y = PI
	add_child(top)
	add_child(bottom)
	goalies.append(top)
	goalies.append(bottom)

func _spawn_local_player() -> void:
	var skater: SkaterController = SKATER_SCENE.instantiate()
	skater.position = Constants.SKATER_START_POS
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
