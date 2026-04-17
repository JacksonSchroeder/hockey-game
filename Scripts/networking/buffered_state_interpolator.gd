class_name BufferedStateInterpolator

# Shared interpolation helper for PuckController / RemoteController /
# GoalieController. All three buffer timestamped network-state snapshots
# and interpolate between bracketing pairs; this class collapses the bracket
# search and stale-trim logic into one place. The per-field lerp stays in
# each controller because each state type has its own field mix.
#
# Buffer element contract (duck-typed): { timestamp: float, state }.

class BracketResult:
	var from_state: Variant = null
	var to_state: Variant = null
	var t: float = 0.0   # clamped to [0, 1]; 1.0 when render_time is past newest

# Returns a BracketResult locating render_time within the buffer, or null if
# the buffer has fewer than 2 entries (caller should do nothing this tick).
# When render_time sits past the newest sample, returns (newest, newest, 1.0)
# so the caller's lerp degenerates to "apply the newest sample" without a
# special-case branch.
static func find_bracket(buffer: Array, render_time: float) -> BracketResult:
	if buffer.size() < 2:
		return null
	for i in range(buffer.size() - 1):
		var a = buffer[i]
		var b = buffer[i + 1]
		if a.timestamp <= render_time and render_time <= b.timestamp:
			return _make(a, b, render_time)
	var newest = buffer[buffer.size() - 1]
	var r := BracketResult.new()
	r.from_state = newest.state
	r.to_state = newest.state
	r.t = 1.0
	return r

# Drops stale buffer entries; keeps at least min_keep at the tail so the next
# tick still has material to bracket against.
static func drop_stale(buffer: Array, render_time: float, min_keep: int = 2) -> void:
	while buffer.size() > min_keep and buffer[1].timestamp < render_time:
		buffer.pop_front()

static func _make(a, b, render_time: float) -> BracketResult:
	var r := BracketResult.new()
	r.from_state = a.state
	r.to_state = b.state
	var span: float = b.timestamp - a.timestamp
	r.t = clampf((render_time - a.timestamp) / span, 0.0, 1.0) if span > 0.0 else 0.0
	return r
