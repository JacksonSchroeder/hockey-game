class_name PlayerRules

# Pure rules about players — team balance, visual-distinct color generation,
# and faceoff position lookup. No engine or GameManager access; callers do
# the data gathering (counting team members, etc.) and pass the numbers in.

const MAX_PER_TEAM: int = 3

# Returns team_id (0 or 1). Balances by count; ties go to team 0.
static func assign_team(team0_count: int, team1_count: int) -> int:
	return 0 if team0_count <= team1_count else 1

# Primary color: jersey, arms, blade. Fixed per team — all teammates match.
#   Team 0 (home) = Pittsburgh Penguins Vegas Gold (#FFB81C)
#   Team 1 (away) = Toronto Maple Leafs Blue (#003E7E)
static func generate_primary_color(team_id: int) -> Color:
	if team_id == 0:
		return Color(1.000, 0.722, 0.110)  # Penguins Vegas Gold #FFB81C
	return Color(0.000, 0.243, 0.494)      # Leafs Blue #003E7E

# Secondary color: legs and helmet (DirectionIndicator). Fixed per team.
#   Team 0 (home) = Penguins Black
#   Team 1 (away) = Leafs White
static func generate_secondary_color(team_id: int) -> Color:
	if team_id == 0:
		return Color(0.06, 0.06, 0.06)  # Penguins Black
	return Color(1.00, 1.00, 1.00)      # Leafs White

# Looks up the faceoff start position for a slot index.
static func faceoff_position_for_slot(slot: int) -> Vector3:
	return GameRules.CENTER_FACEOFF_POSITIONS[slot]
