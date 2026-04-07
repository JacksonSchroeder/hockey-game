# Hockey Game — Design Document v7.0

A 3v3 arcade hockey game built in **Godot 4.4.1** (3D, GDScript). Online multiplayer — one player per machine, each with their own camera.

**Design philosophy:** Depth over breadth — few inputs with rich emergent behavior rather than many explicit mechanics.

**Key inspirations:** Omega Strikers / Rocket League (structure), Breakpoint (twin-stick melee blade feel), Mario Superstar Baseball (stylized characters, exaggerated arcadey tuning, unique abilities, pre-match draft). Slapshot: Rebound is a cautionary reference — its pure physics shooting feels unintuitive; the blade proximity pickup system is explicitly designed to solve that accessibility gap.

---

## 1. Vision & Direction

The game targets a stylized arcade experience with four character categories: **Power, Balanced, Technique, Speed**. Positional assignment (C/W/D) during drafting determines faceoff lineups and default defensive assignments.

The Rocket League freeplay ceiling is a guiding star — the stickhandling-to-shot pipeline should reward practice and feel satisfying to master. Players should want to spend time in free play practicing moves and scoring on the goalie.

---

## 2. Architecture

### 2.1 Scene Structure

- **Skater:** CharacterBody3D with UpperBody/LowerBody split (Node3D). Shoulder (Marker3D) under UpperBody, positioned by code based on handedness. Blade (Marker3D) and StickMesh under UpperBody. Reusable scene — one scene per skater, driven by CharacterStats resource.
- **Puck:** RigidBody3D with cylinder collision (radius 0.1m, height 0.05m). PickupZone (Area3D, SphereShape3D radius 0.5m) for blade proximity detection. Emits `puck_picked_up` and `puck_released` signals.
- **Rink:** StaticBody3D with procedurally generated walls, corners, and ice surface via @tool script. 60×26m, Z axis is the long axis.
- **Goals:** StaticBody3D with procedurally generated posts, crossbar, and back wall via @tool script.
- **Goalie:** StaticBody3D with butterfly-stance collision shapes (two leg pads + body block with five hole gap).
- **Camera:** Camera3D per player. Weighted anchor system — player, puck, mouse, attacking goal. Zoom computed after position clamping to prevent fighting between position and zoom.

### 2.2 Collision Layers

| Layer | Purpose |
|-------|---------|
| 1 | General physics (boards, goals, goalies, skaters) |
| 2 | Blades (BladeArea on each skater) |
| 3 | Puck pickup zone (PickupZone on puck) |
| 4 | Ice surface |

The puck has **no layer** (mask = 1). It bounces off everything on layer 1 but doesn't push skaters.

### 2.3 Input Architecture

All input flows through an **InputState** data object populated by a **LocalInputGatherer**. This abstraction layer supports future swap to network input or AI input without touching game logic.

The gatherer requires a `Camera3D` reference to compute mouse world position via ray-plane intersection at y=0 (ice surface).

InputState fields: `move_vector`, `mouse_world_pos`, `shoot_pressed`, `shoot_held`, `slap_pressed`, `slap_held`, `facing_held`, `brake`, `self_pass`, `self_shot`, `elevation_up`, `elevation_down`, `reset`.

### 2.4 Physics

240 FPS physics tick rate to prevent tunneling. CCD enabled on puck. Puck mass 0.17kg, radius 0.1m.

---

## 3. Controls

### 3.1 Mouse & Keyboard Layout

| Input | Action |
|-------|--------|
| **WASD** | Movement (screen-relative) |
| **Mouse position** | Blade position (always active) |
| **Left click (tap)** | Quick shot / pass |
| **Left click (hold + move)** | Wrister — charge by sweeping blade, release to fire |
| **Right click (hold)** | Slapshot charge — release to fire |
| **Shift (hold)** | Backward skating — facing locks to mouse direction |
| **Scroll up** | Set elevated shot mode |
| **Scroll down** | Set flat shot mode |
| **Space** | Brake (increased friction) |
| **Q** | Self-pass (feed puck toward player — practice tool) |
| **E** | Self-shot (fire puck toward player — practice tool) |

### 3.2 Control Philosophy

The blade is **always mouse-controlled** — there is no mode toggle. The mouse drives the blade at all times. Facing automatically follows movement direction with lag, giving the skater natural body weight.

Shift is a deliberate stance change — like a hockey player planting and turning to skate backward while watching the play develop in front of them.

---

## 4. Blade Control

The blade follows the mouse cursor at all times, originating from the stick-hand shoulder. Mouse distance from the player is mapped to blade reach — at `max_mouse_distance` (4m) the blade is fully extended, closer in it retracts proportionally. This gives finer control resolution for close-quarters stickhandling.

### 4.1 Arc Limits

The blade is clamped to a reachable arc around the player, measured from the player's forward direction in upper-body local space:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `blade_forehand_limit` | 90° | Max arc on forehand side |
| `blade_backhand_limit` | 80° | Max arc on backhand side |

When the mouse pushes past the arc limit, the player's **facing rotates smoothly** to follow at `facing_drag_speed`. This means extended mouse movement in one direction naturally rotates the whole player.

### 4.2 Player-Relative Storage

When the blade is not actively following the mouse (e.g. during follow through), the blade's **player-relative angle** is stored and replayed. If the skater rotates, the blade stays in the same relative position to the body.

### 4.3 Upper Body Twist

The UpperBody node rotates independently of the CharacterBody3D to express the angle between facing and blade direction. The upper body rotates to `blade_angle * upper_body_twist_ratio` (default 0.5). Always active during normal skating and wrister aim.

### 4.4 Handedness

Each character has an `is_left_handed` flag. This determines which side is forehand, where the shoulder pivot sits, and how the arc limits are oriented. Backhand shots are less powerful than forehand shots (`backhand_power_coefficient`, default 0.75).

### 4.5 Wall Clamping

The blade's StickRaycast detects nearby walls and shortens blade reach accordingly. If the wall squeezes the blade significantly beyond `wall_squeeze_threshold`, the puck is released along the wall normal.

---

## 5. Facing & Skating

### 5.1 Default Facing

Facing automatically follows the movement direction with lag (`facing_lag_speed`). This gives the skater natural body weight — the body gradually catches up to the direction of travel, like a real skater committing to a line.

### 5.2 Backward Skating

**Hold shift.** Facing locks to the mouse direction. The skater can move freely in any direction while watching where the mouse points — like a defenseman skating backward while watching the play.

Movement thrust is scaled by the relationship between facing and input direction:

| Skating direction relative to facing | Thrust |
|--------------------------------------|--------|
| Forward (aligned) | Full (`thrust`) |
| Sideways (crossover) | `crossover_thrust_multiplier` (0.85) |
| Backward (opposing) | `backward_thrust_multiplier` (0.7) |

### 5.3 Facing During Shots

Facing is locked during `WRISTER_AIM`, `SLAPPER_CHARGE_WITH_PUCK`, and `SLAPPER_CHARGE_WITHOUT_PUCK`. Shift has no effect in these states.

### 5.4 Blade Drag Rotation

Pushing the mouse past the arc limit rotates facing smoothly at `facing_drag_speed` regardless of shift state (except during shot states).

---

## 6. Shooting

### 6.1 Quick Shot / Pass

**Tap left click.** If the wrister charge distance is below `quick_shot_threshold`, the puck fires in the blade's current direction at `quick_shot_power`. No aiming required — what you see is what you get. Low skill floor: new players click to pass, experienced players wind up for precision shots.

### 6.2 Wrister

**Hold left click and sweep the blade, release to fire.** Power determined by blade travel distance, mapped from `min_wrister_power` to `max_wrister_power` over `max_wrister_charge_distance`.

**Direction variance check:** If the blade changes direction by more than `max_charge_direction_variance` (45°) in a single frame, charge resets to zero. Prevents charge farming by wiggling the mouse.

**Backhand penalty:** Blade on backhand side at release → power multiplied by `backhand_power_coefficient` (0.75).

**Without puck:** Entering wrister aim without possession is allowed. Puck arriving on blade while left click is held fires on release — enables one-touch finishes and tip-ins.

### 6.3 Slapshot — With Puck

**Hold right click while carrying the puck.** Blade fixes to forehand position relative to shoulder. Skater glides on existing momentum (no thrust, natural friction). Upper body rotates toward mouse within `slapper_aim_arc` (45° either side). Release to fire.

Power scales from `min_slapper_power` to `max_slapper_power` over `max_slapper_charge_time`.

### 6.4 Slapshot — Without Puck (One-Timer)

**Hold right click without the puck.** Full movement and facing rotation active — skate into position while charging. Blade fixes to forehand position. Puck arriving on blade while charging automatically transitions to `SLAPPER_CHARGE_WITH_PUCK` with charge time carried over. Release fires if puck is on blade, otherwise cancels.

### 6.5 Elevation

Scroll up sets elevated shot mode. Scroll down returns to flat. State persists until changed.

| Shot type | Elevation |
|-----------|-----------|
| Wrister / quick shot | `wrister_elevation` (0.3) |
| Slapshot | `slapper_elevation` (0.15) |

---

## 7. Puck

### 7.1 Pickup

Automatic on blade proximity via Area3D overlap. Puck freezes and follows blade each frame. Single authority managed by the puck node.

### 7.2 Pickup vs Deflection

| Puck Speed | Result |
|-----------|--------|
| Below `pickup_max_speed` (8.0) | Clean pickup |
| Between thresholds | Middle zone — currently picks up |
| Above `deflect_min_speed` (20.0) | Deflection off blade |

`deflect_blend` (0.5) controls redirection vs continuation. `deflect_speed_retain` (0.7) controls speed retention. `deflect_cooldown` (0.3s) prevents immediate re-attachment.

### 7.3 Puck Signals

- `puck_picked_up(carrier)` — skater gains possession
- `puck_released()` — puck released for any reason

### 7.4 Puck Physics

No collision layer (mask = 1). Reattach cooldown 0.5s after any release.

---

## 8. Skating & Movement

### 8.1 Movement

Screen-relative WASD. Thrust scales with facing/input alignment (see Section 5.2). Friction brings speed back down naturally.

| Parameter | Default |
|-----------|---------|
| `thrust` | 20.0 |
| `friction` | 5.0 |
| `max_speed` | 10.0 |
| `brake_multiplier` | 5.0 |
| `facing_lag_speed` | 6.0 |
| `backward_thrust_multiplier` | 0.7 |
| `crossover_thrust_multiplier` | 0.85 |

### 8.2 Wall Squeeze

Boards clamping the blade beyond `wall_squeeze_threshold` releases the puck along the wall normal.

---

## 9. Camera

One camera per player. Weighted anchor system:

| Anchor | Weight | Notes |
|--------|--------|-------|
| Player | 1.0 (base) | Always included, non-negotiable |
| Puck | `puck_weight` (1.0) | Pulls camera toward puck |
| Mouse world pos | `mouse_weight` (0.5) | Leads camera toward aim direction |
| Attacking goal | `goal_weight` (0.3) | Biases toward offensive end — requires game manager |

**Player-first guarantee:** The weighted target is clamped so the player never exceeds `player_margin` (0.8) from the frame edge.

**Zoom:** Computed after position clamping, based on distance from clamped camera position to puck. This prevents zoom from fighting the position clamp.

**Soft rink clamp:** Applied last, after player guarantee. Never shows outside the rink if possible, but never overrides player visibility.

---

## 10. Skater State Machine

| State | Blade | Movement | Facing |
|-------|-------|----------|--------|
| `SKATING_WITHOUT_PUCK` | Follows mouse | Full | Follows movement (shift = backward) |
| `SKATING_WITH_PUCK` | Follows mouse | Full | Follows movement (shift = backward) |
| `WRISTER_AIM` | Follows mouse | Full | Locked |
| `SLAPPER_CHARGE_WITH_PUCK` | Fixed forehand | Glide only | Locked (upper body aims within arc) |
| `SLAPPER_CHARGE_WITHOUT_PUCK` | Fixed forehand | Full | Continuous toward mouse |
| `FOLLOW_THROUGH` | Stored relative angle | Full | Follows movement (shift = backward) |

---

## 11. Characters & Abilities

Four character categories: **Power, Balanced, Technique, Speed**. Each character has individually tuned parameters (no rigid tier system) and a unique ability.

Positional assignment (C/W/D) during drafting determines faceoff lineups and default defensive assignments. Position doesn't change character stats.

### 11.1 Ability Design Principles

- Abilities modify physics or movement — never "puck goes in net"
- Simple to execute, but when and where is the skill expression
- Balance is compositional

### 11.2 CharacterStats Resource

Per-character tuning will live in a `CharacterStats` resource. Universal game constants (arc limits, shot thresholds, etc.) stay on the skater. Per-character values (thrust, max_speed, power ranges, etc.) move to CharacterStats. Separation TBD after playtesting reveals which knobs actually matter.

---

## 12. Game Flow & Rules

### 12.1 No Stoppages

Never stops except for goals and faceoffs. All rule enforcement uses soft mechanical deterrents.

### 12.2 Faceoffs

Puck dropped between two players, battled with existing stick mechanics. No minigame.

### 12.3 Soft Offsides

Speed decays past the blue line without the puck.

### 12.4 Soft Icing

Iced puck placed behind net, defensive team only.

### 12.5 Defensive Assignment Indicator

Optional man-to-man visual indicator. Togglable per player. Dynamic reassignment with brief delay to prevent flickering.

### 12.6 Penalties

Self-regulating in 3v3. Mechanical solutions preferred over formal penalty system.

---

## 13. AI Goalie

Distinct entity, not a retuned skater. Detailed behavior deferred until core systems are playable.

Current implementation: StaticBody3D with butterfly-stance collision shapes, angle-tracking with reaction lag.

---

## 14. Rink

60×26m (may reduce to 2/3 scale), corner radius 8.5m. Z axis is the long axis. Procedurally generated via @tool script. Board bounce 0.4. Goals at both ends, 3.4m from boards.

---

## 15. Build Order

| Stage | Description | Status |
|-------|-------------|--------|
| 1 | Skating feel | ✅ Complete |
| 2 | Stick/puck interaction | ✅ Complete |
| 3 | Basic goalie | ✅ Complete |
| 4 | Second skater + collisions | Next |
| 5 | Networking test (early validation) | Planned |
| 6 | Characters + abilities | Planned |

**Architecture targets:** Skater as reusable scene, CharacterStats resource per character, game manager for authority, input abstraction for multiplayer.

---

## 16. Open Questions

*Parked for playtesting to reveal real gaps:*

- Slapshot pre/post release buffer window for one-timer timing
- Middle-zone puck reception: blade readiness check
- Elevation further refinement (additional angles/states)
- Backward skating feel tuning (multiplier values)
- Crossover feel — whether automatic boost through turns is worth adding
- Game manager for attacking goal direction and camera wiring
- Camera goal anchor flip speed on turnovers
- Aim assist
- Goalie body parts and detailed save mechanics
- Goal detection
- Stick checks / poke checks
- Rink size tuning
- IK for arm/stick animation
- Procedural skating animations
- CharacterStats resource separation (universal vs per-character exports)
- Slapshot charge direction variance check (prevent charge farming while stationary)
