class_name PuckController
extends Node

var puck: Puck = null
var is_server: bool = false

# ── State ─────────────────────────────────────────────────────────────────────
var _carrier_peer_id: int = -1

# ── Setup ─────────────────────────────────────────────────────────────────────
func setup(assigned_puck: Puck, assigned_is_server: bool) -> void:
	puck = assigned_puck
	is_server = assigned_is_server
	puck.set_server_mode(is_server)
	if is_server:
		puck.puck_picked_up.connect(_on_puck_picked_up)
		puck.puck_released.connect(_on_puck_released)

# ── Server Signals ────────────────────────────────────────────────────────────
func _on_puck_picked_up(carrier: Skater) -> void:
	for peer_id in GameManager.players:
		var record: PlayerRecord = GameManager.players[peer_id]
		if record.skater == carrier:
			_carrier_peer_id = peer_id
			record.controller.on_puck_picked_up_network()
			return

func _on_puck_released() -> void:
	if _carrier_peer_id != -1 and GameManager.players.has(_carrier_peer_id):
		GameManager.players[_carrier_peer_id].controller.on_puck_released_network()
	_carrier_peer_id = -1

# ── State Serialization ───────────────────────────────────────────────────────
func get_state() -> Array:
	var state := PuckNetworkState.new()
	state.position = puck.get_puck_position()
	state.velocity = puck.get_puck_velocity()
	state.carrier_peer_id = _carrier_peer_id
	return state.to_array()

func apply_state(data: Array) -> void:
	if is_server:
		return
	var state := PuckNetworkState.from_array(data)
	_carrier_peer_id = state.carrier_peer_id
	puck.set_puck_position(state.position)
	puck.set_puck_velocity(state.velocity)
