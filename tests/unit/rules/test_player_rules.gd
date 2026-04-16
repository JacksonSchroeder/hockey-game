extends GutTest

# PlayerRules — team balancing, color generation, faceoff position lookup.

# ── assign_team ──────────────────────────────────────────────────────────────

func test_first_player_assigned_to_team_0() -> void:
	assert_eq(PlayerRules.assign_team(0, 0), 0, "tie goes to team 0")

func test_smaller_team_gets_next_player() -> void:
	assert_eq(PlayerRules.assign_team(1, 0), 1)
	assert_eq(PlayerRules.assign_team(0, 1), 0)

func test_balanced_counts_prefer_team_0() -> void:
	assert_eq(PlayerRules.assign_team(2, 2), 0)

func test_lopsided_filled_to_smaller() -> void:
	assert_eq(PlayerRules.assign_team(3, 1), 1)

# ── generate_primary_color ───────────────────────────────────────────────────

func test_team_0_primary_is_gold() -> void:
	var c: Color = PlayerRules.generate_primary_color(0)
	assert_true(c.r > 0.8 and c.g > 0.5 and c.b < 0.3, "home primary should be Penguins gold, got r=%f g=%f b=%f" % [c.r, c.g, c.b])

func test_team_1_primary_is_blue() -> void:
	var c: Color = PlayerRules.generate_primary_color(1)
	assert_true(c.b > c.r and c.b > c.g, "away primary should be Leafs blue-dominant, got r=%f g=%f b=%f" % [c.r, c.g, c.b])

func test_team_0_primary_is_deterministic() -> void:
	assert_eq(PlayerRules.generate_primary_color(0), PlayerRules.generate_primary_color(0))

func test_team_1_primary_is_deterministic() -> void:
	assert_eq(PlayerRules.generate_primary_color(1), PlayerRules.generate_primary_color(1))

# ── generate_secondary_color ─────────────────────────────────────────────────

func test_team_0_secondary_is_near_black() -> void:
	var c: Color = PlayerRules.generate_secondary_color(0)
	assert_true(c.r < 0.1 and c.g < 0.1 and c.b < 0.1, "home secondary should be Penguins black, got r=%f g=%f b=%f" % [c.r, c.g, c.b])

func test_team_1_secondary_is_white() -> void:
	var c: Color = PlayerRules.generate_secondary_color(1)
	assert_true(c.r > 0.9 and c.g > 0.9 and c.b > 0.9, "away secondary should be Leafs white, got r=%f g=%f b=%f" % [c.r, c.g, c.b])

func test_team_0_secondary_is_deterministic() -> void:
	assert_eq(PlayerRules.generate_secondary_color(0), PlayerRules.generate_secondary_color(0))

func test_team_1_secondary_is_deterministic() -> void:
	assert_eq(PlayerRules.generate_secondary_color(1), PlayerRules.generate_secondary_color(1))

# ── faceoff_position_for_slot ────────────────────────────────────────────────

func test_faceoff_positions_even_slots_are_team_0_side() -> void:
	assert_gt(PlayerRules.faceoff_position_for_slot(0).z, 0.0)
	assert_gt(PlayerRules.faceoff_position_for_slot(2).z, 0.0)
	assert_gt(PlayerRules.faceoff_position_for_slot(4).z, 0.0)

func test_faceoff_positions_odd_slots_are_team_1_side() -> void:
	assert_lt(PlayerRules.faceoff_position_for_slot(1).z, 0.0)
	assert_lt(PlayerRules.faceoff_position_for_slot(3).z, 0.0)
	assert_lt(PlayerRules.faceoff_position_for_slot(5).z, 0.0)
