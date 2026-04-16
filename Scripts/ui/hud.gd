class_name HUD
extends CanvasLayer

var _period_label: Label
var _clock_label: Label
var _home_score_label: Label
var _away_score_label: Label
var _phase_panel: PanelContainer
var _phase_label: Label
var _elevation_panel: PanelContainer
var _local_skater: Skater = null

const _DARK_BG    := Color(0.07, 0.07, 0.09, 0.92)
const _WHITE      := Color(1.00, 1.00, 1.00, 1.00)
const _DIM        := Color(0.62, 0.62, 0.68, 1.00)
const _GOLD       := Color(1.00, 0.85, 0.20, 1.00)
const _SEP_COLOR  := Color(0.28, 0.28, 0.33, 1.00)

func _ready() -> void:
	_build_scorebug()
	_build_phase_banner()
	_build_elevation_indicator()
	if NetworkManager.is_host:
		_build_reset_button()
	_period_label.text = _period_ordinal(1)
	_clock_label.text = _format_clock(GameRules.PERIOD_DURATION)
	_home_score_label.text = "0"
	_away_score_label.text = "0"
	_phase_panel.visible = false
	GameManager.score_changed.connect(_on_score_changed)
	GameManager.phase_changed.connect(_on_phase_changed)
	GameManager.period_changed.connect(_on_period_changed)
	GameManager.clock_updated.connect(_on_clock_updated)
	GameManager.game_over.connect(_on_game_over)

func _process(_delta: float) -> void:
	if _local_skater == null:
		var record: PlayerRecord = GameManager.get_local_player()
		if record:
			_local_skater = record.skater
	if _local_skater != null:
		_elevation_panel.visible = _local_skater.is_elevated

# ---------------------------------------------------------------------------
# Build helpers
# ---------------------------------------------------------------------------

func _build_scorebug() -> void:
	var panel_style := StyleBoxFlat.new()
	panel_style.bg_color = _DARK_BG
	panel_style.set_corner_radius_all(3)

	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", panel_style)
	panel.position = Vector2(8, 8)
	add_child(panel)

	var hbox := HBoxContainer.new()
	hbox.add_theme_constant_override("separation", 0)
	panel.add_child(hbox)

	# Left column: period ordinal stacked above clock
	var left_cell := _cell(6, 8)
	hbox.add_child(left_cell)
	var left_vbox := VBoxContainer.new()
	left_vbox.add_theme_constant_override("separation", 2)
	left_vbox.alignment = BoxContainer.ALIGNMENT_CENTER
	left_cell.add_child(left_vbox)

	_period_label = _lbl("1ST", 12, _DIM)
	_period_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	left_vbox.add_child(_period_label)

	_clock_label = _lbl("4:00", 24, _WHITE)
	_clock_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_clock_label.custom_minimum_size = Vector2(62, 0)
	left_vbox.add_child(_clock_label)

	hbox.add_child(_vsep())

	# Right column: GridContainer so badge column and score column share alignment.
	var right_cell := _cell(4, 6)
	hbox.add_child(right_cell)
	var grid := GridContainer.new()
	grid.columns = 2
	grid.add_theme_constant_override("h_separation", 10)
	grid.add_theme_constant_override("v_separation", 4)
	right_cell.add_child(grid)

	grid.add_child(_team_badge("HOME", PlayerRules.generate_primary_color(0)))
	_home_score_label = _lbl("0", 20, _WHITE)
	_home_score_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_home_score_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_home_score_label.custom_minimum_size = Vector2(20, 0)
	_home_score_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	grid.add_child(_home_score_label)

	grid.add_child(_team_badge("AWAY", PlayerRules.generate_primary_color(1)))
	_away_score_label = _lbl("0", 20, _WHITE)
	_away_score_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_away_score_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_away_score_label.custom_minimum_size = Vector2(20, 0)
	_away_score_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	grid.add_child(_away_score_label)

func _build_phase_banner() -> void:
	# Centered below the scorebug
	var root := Control.new()
	root.anchor_right = 1.0
	root.offset_top = 62.0
	root.offset_bottom = 112.0
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)

	var centering := HBoxContainer.new()
	centering.alignment = BoxContainer.ALIGNMENT_CENTER
	centering.anchor_right = 1.0
	centering.offset_bottom = 50.0
	centering.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(centering)

	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.07, 0.07, 0.09, 0.88)
	style.set_corner_radius_all(3)
	style.set_content_margin(SIDE_LEFT, 20)
	style.set_content_margin(SIDE_RIGHT, 20)
	style.set_content_margin(SIDE_TOP, 8)
	style.set_content_margin(SIDE_BOTTOM, 8)

	_phase_panel = PanelContainer.new()
	_phase_panel.add_theme_stylebox_override("panel", style)
	centering.add_child(_phase_panel)

	_phase_label = _lbl("", 18, _GOLD)
	_phase_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_phase_panel.add_child(_phase_label)

func _build_elevation_indicator() -> void:
	var style := StyleBoxFlat.new()
	style.bg_color = _DARK_BG
	style.set_corner_radius_all(6)
	style.set_content_margin_all(8)

	_elevation_panel = PanelContainer.new()
	_elevation_panel.add_theme_stylebox_override("panel", style)
	_elevation_panel.anchor_left = 0.5
	_elevation_panel.anchor_right = 0.5
	_elevation_panel.anchor_top = 1.0
	_elevation_panel.anchor_bottom = 1.0
	_elevation_panel.offset_left = -60.0
	_elevation_panel.offset_right = 60.0
	_elevation_panel.offset_top = -48.0
	_elevation_panel.offset_bottom = -16.0
	_elevation_panel.visible = false
	add_child(_elevation_panel)

	var label := _lbl("\u2191 ELEVATED", 16, Color(0.4, 0.8, 1.0, 1.0))
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_elevation_panel.add_child(label)

func _build_reset_button() -> void:
	var btn := Button.new()
	btn.text = "Reset"
	btn.add_theme_font_size_override("font_size", 14)
	btn.anchor_left = 1.0
	btn.anchor_right = 1.0
	btn.anchor_top = 0.0
	btn.anchor_bottom = 0.0
	btn.offset_left = -88.0
	btn.offset_right = -16.0
	btn.offset_top = 16.0
	btn.offset_bottom = 48.0
	btn.pressed.connect(GameManager.reset_game)
	add_child(btn)

# ---------------------------------------------------------------------------
# Signal handlers
# ---------------------------------------------------------------------------

func _on_score_changed(score_0: int, score_1: int) -> void:
	_home_score_label.text = str(score_0)
	_away_score_label.text = str(score_1)

func _on_phase_changed(new_phase: int) -> void:
	match new_phase:
		GamePhase.Phase.PLAYING:
			_phase_panel.visible = false
		GamePhase.Phase.GOAL_SCORED:
			_phase_label.text = "GOAL!"
			_phase_panel.visible = true
		GamePhase.Phase.END_OF_PERIOD:
			_phase_label.text = "END OF PERIOD"
			_phase_panel.visible = true
		GamePhase.Phase.GAME_OVER:
			_phase_label.text = "GAME OVER"
			_phase_panel.visible = true
		_:
			_phase_label.text = "FACEOFF"
			_phase_panel.visible = true

func _on_period_changed(new_period: int) -> void:
	_period_label.text = _period_ordinal(new_period)

func _on_clock_updated(t: float) -> void:
	_clock_label.text = _format_clock(t)

func _on_game_over() -> void:
	_phase_label.text = "GAME OVER"
	_phase_panel.visible = true

# ---------------------------------------------------------------------------
# Utility
# ---------------------------------------------------------------------------

func _cell(h_margin: int, v_margin: int) -> MarginContainer:
	var c := MarginContainer.new()
	c.add_theme_constant_override("margin_left", h_margin)
	c.add_theme_constant_override("margin_right", h_margin)
	c.add_theme_constant_override("margin_top", v_margin)
	c.add_theme_constant_override("margin_bottom", v_margin)
	return c

func _team_badge(text: String, bg_color: Color) -> PanelContainer:
	var style := StyleBoxFlat.new()
	style.bg_color = bg_color
	style.set_corner_radius_all(3)
	style.set_content_margin(SIDE_LEFT, 6)
	style.set_content_margin(SIDE_RIGHT, 6)
	style.set_content_margin(SIDE_TOP, 3)
	style.set_content_margin(SIDE_BOTTOM, 3)
	var badge := PanelContainer.new()
	badge.add_theme_stylebox_override("panel", style)
	badge.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	# Pick text color by background luminance: dark text on light fills, white on dark.
	var lum: float = 0.299 * bg_color.r + 0.587 * bg_color.g + 0.114 * bg_color.b
	var text_color: Color = Color(0.06, 0.06, 0.06) if lum > 0.4 else _WHITE
	badge.add_child(_lbl(text, 11, text_color))
	return badge

func _vsep() -> VSeparator:
	var sep := VSeparator.new()
	var style := StyleBoxFlat.new()
	style.bg_color = _SEP_COLOR
	style.set_content_margin_all(0)
	sep.add_theme_stylebox_override("separator", style)
	sep.custom_minimum_size = Vector2(1, 0)
	return sep

func _lbl(text: String, size: int, color: Color) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", color)
	return l

func _period_ordinal(p: int) -> String:
	match p:
		1: return "1ST"
		2: return "2ND"
		3: return "3RD"
		_: return "OT"

func _format_clock(t: float) -> String:
	var secs: int = int(ceil(t))
	return "%d:%02d" % [int(secs / 60.0), secs % 60]
