class_name HitTracker
extends RefCounted

# Host-only tracker for hit crediting. Validates that a hit is against an
# opposing player before incrementing the hitter's stat.
#
# Flow:
#   on_hit(hitter_peer_id, victim_team_id) → credits hit if cross-team, emits hit_credited

signal hit_credited

var _registry: PlayerRegistry = null


func setup(registry: PlayerRegistry) -> void:
	_registry = registry


func on_hit(hitter_peer_id: int, victim_team_id: int) -> void:
	var record: PlayerRecord = _registry.get_record(hitter_peer_id)
	if record == null:
		return
	if record.team.team_id == victim_team_id:
		return  # no credit for hitting a teammate
	record.stats.hits += 1
	hit_credited.emit()
