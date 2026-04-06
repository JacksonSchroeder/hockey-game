# Hockey Game — Design Document v5.0

A 3v3 arcade hockey game built in **Godot 4.4.1** (3D, GDScript). Couch co-op primary experience with a shared dynamic camera (Smash Bros-style), even online.

**Design philosophy:** Depth over breadth — few inputs with rich emergent behavior rather than many explicit mechanics.

**Key inspirations:** Omega Strikers / Rocket League (structure), Breakpoint (twin-stick melee blade feel), Mario Superstar Baseball (stylized characters, exaggerated arcadey tuning, unique abilities, pre-match draft). Slapshot: Rebound is a cautionary reference — its pure physics shooting feels unintuitive.

---

## 1. Vision & Direction

The game targets a stylized arcade experience with four character categories: **Power, Balanced, Technique, and Speed**. Positional assignment (C/W/D) during drafting determines faceoff lineups and default defensive assignments.

The Rocket League freeplay ceiling is a guiding star — the stickhandling-to-shot pipeline should reward practice and feel satisfying to master. Players should want to spend time in free play practicing moves and scoring on the goalie.

---

## 2. Architecture

### 2.1 Scene Structure

- **Skater:** CharacterBody3D with UpperBody/LowerBody split (Node3D). Shoulder (Marker3D) under UpperBody, positioned by code based on handedness. Blade (Marker3D) and StickMesh under UpperBody. Reusable scene — one scene per skater, driven by CharacterStats resource.
- **Puck:** RigidBody3D with cylinder collision (radius 0.1m, height 0.05m). PickupZone (Area3D, SphereShape3D radius 0.5m) for blade proximity detection. Emits `puck_picked_up` and `puck_released` signals.
- **Rink:** StaticBody3D with procedurally generated walls, corners, and ice surface via @tool script.
- **Goals:** StaticBody3D with procedurally generated posts, crossbar, and back wall via @tool script.
- **Goalie:** StaticBody3D with butterfly-stance collision shapes (two leg pads + body block with five hole gap).

### 2.2 Collision Layers

| Layer | Purpose |
|-------|---------|
| 1 | General physics (boards, goals, goalies, skaters) |
| 2 | Blades (BladeArea on each skater) |
| 3 | Puck pickup zone (PickupZone on puck) |
| 4 | Ice surface |

The puck has **no layer** (mask = 1). It bounces off everything on layer 1 but doesn't push skaters. This prevents the puck from dragging players around on contact.

### 2.3 Input Architecture

All input flows through an **InputState** data object populated by a **LocalInputGatherer**. This abstraction layer supports future swap to network input or AI input without touching game logic.

InputState fields: `move_vector`, `blade_vector`, `shoot_pressed`, `shoot_held`, `shot_cancel`, `elevate`, `brake`, `orientation`, `dash`, `ability`, `reset`, `self_pass`, `quick_shot`.

### 2.4 Physics

240 FPS physics tick rate to prevent tunneling. CCD enabled on puck. Puck mass 0.17kg, radius 0.1m.

---

## 3. Controls

### 3.1 Controller Layout

**Left Hand (Body Control)**
- Left Stick: Movement (screen-relative, analog)
- Left Trigger: Orientation toggle (skate backward)
- Left Bumper: Dash (instant velocity boost, can exceed max speed)
- Left Stick Press: Brake (increased friction for quick stops)

**Right Hand (Stick & Puck)**
- Right Stick: Blade positioning (context-sensitive — see Blade Control)
- Right Trigger Press: Enter shoot mode
- Right Stick in shoot mode: Wrister (flick forward) or Slapshot (pull back to charge)
- Right Stick release in shoot mode: Fire shot
- Right Bumper: Quick shot (tap) / Elevate shot (in shoot mode)
- Right Stick Press: Cancel shot

**Other**
- Y/Triangle: Reset puck to center ice (dev tool)
- Self-pass button: Feeds puck toward player (practice tool)

---

## 4. Blade Control

The blade system is **context-sensitive based on puck possession**, addressing fundamentally different needs for offense and defense.

### 4.1 With Puck: Player-Relative Plane

The right stick maps to a **positional plane** anchored to the skater's stick-hand shoulder. This is not a rotational arc — the stick moves laterally and forward/back on a 2D surface in front of the player.

- **Stick neutral:** Blade returns to center of the plane (in front of the skater)
- **Stick lateral:** Blade shifts left/right relative to the skater's facing
- **Stick forward (up):** Blade extends further in front
- **Stick back (down):** Blade pulls toward the skater's feet

**Asymmetric shape:** The plane is not symmetric. The forehand side (stick-hand side) has extended range, including the ability to drag the puck behind the skater. The backhand-behind quadrant is compressed, reflecting the physical limitation of reaching across the body and behind. The shoulder offset shifts the plane's center toward the stick hand.

**`invert_plane`:** For the team attacking the bottom net, the entire stick input is negated so that the feel is consistent regardless of which direction you're attacking. Both teams have identical screen-relative feel.

**Why player-relative:** Stickhandling with the puck requires fast, twitchy micro-movements — dekes, protections, shot setups. Your brain is in "my hands" mode. Player-relative means left always means your left, which stays consistent through turns.

### 4.2 Without Puck: World-Relative

The right stick points the blade in **screen-space direction**. Point the stick top-right, the blade goes to the top-right of the screen regardless of which way the skater is facing.

**Arc limits:** The blade is clamped to a reachable arc — 135° on the forehand side and 90° on the backhand side, measured from the skater's forward direction. This prevents the blade from going through the skater's body.

**Why world-relative:** Without the puck, you're positioning your stick on the ice — cutting passing lanes, angling for interceptions, preparing to receive. Your brain is in "that spot on the ice" mode. World-relative means you just point at where you want your stick, no mental translation needed. This is especially important when skating backward on defense.

### 4.3 Tuning Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `blade_move_speed` | Lerp speed when stick is active | 12.0 |
| `blade_return_speed` | Lerp speed returning to center | 8.0 |
| `plane_reach` | Max blade distance from shoulder | 1.5 |
| `close_reach` | Blade distance at stick-down (near feet) | 0.4 |
| `shoulder_offset` | Lateral offset toward stick hand | 0.35 |
| `backhand_behind_scale` | Compression of backhand-behind quadrant (0-1) | 0.3 |
| `forehand_behind_reach` | How far behind on forehand side | 1.0 |
| `world_forehand_limit` | Arc limit on forehand side (degrees) | 135 |
| `world_backhand_limit` | Arc limit on backhand side (degrees) | 90 |

---

## 5. Shooting

### 5.1 Shoot Mode

Press RT to enter shoot mode. The blade freezes at its current position on the plane. Forehand/backhand is determined by the blade's lateral position at the moment of entry.

- **Wrister:** Flick the right stick forward. Fixed power (forehand: 15.0, backhand: 10.0). Fires when stick returns to deadzone.
- **Slapshot:** Pull the right stick back to charge. Power scales with charge time (15.0 to 25.0 over 1 second). Always forehand. Fires when stick returns to deadzone.
- **Elevation:** Hold RB during a shot to add vertical component.

Shot aiming is **screen-relative** during shoot mode — point the stick where you want the puck to go on the ice, regardless of skater facing.

### 5.2 Quick Shot

Tap RB while skating with the puck. The shot direction comes from the **blade's plane movement delta** — the direction you're moving the blade on the plane at that moment. This is player-relative, converted to world-space for the puck release.

Quick shots enable dekes-to-shots: sweep the blade forehand to backhand and tap RB, the puck fires in the sweep direction. The lerp on `_blade_plane_pos` provides natural smoothing so frame-to-frame jitter doesn't produce erratic shots.

### 5.3 Handedness

Each character has an `is_left_handed` flag. This determines which side is forehand, where the shoulder pivot sits, and how the plane asymmetry is oriented. Slapshots always force forehand. Forehand wristers are more powerful than backhand.

---

## 6. Puck

### 6.1 Pickup

Automatic on blade proximity via Area3D overlap. The puck freezes and follows the blade. Single authority managed by the puck node — the puck tracks its own carrier.

### 6.2 Pickup vs Deflection

When the puck contacts a blade, **speed determines the outcome**:

| Puck Speed | Result |
|-----------|--------|
| Below `pickup_max_speed` (8.0) | Clean pickup — puck attaches |
| Between thresholds | Middle zone — currently picks up (readiness check planned) |
| Above `deflect_min_speed` (20.0) | Deflection — puck redirects off blade |

**Deflection direction:** The puck's velocity is blended toward the blade's outward-facing direction. `deflect_blend` (0.5) controls how much the blade redirects vs the puck continuing its path. `deflect_speed_retain` (0.7) controls how much speed the puck keeps.

A `deflect_cooldown` (0.3s) prevents the puck from immediately re-attaching after a tip.

### 6.3 Puck Signals

The puck emits signals that drive game state:

- `puck_picked_up(carrier)` — emitted when a skater gains possession. Transitions the skater to `SKATING_WITH_PUCK` state.
- `puck_released()` — emitted on shots, releases, and resets. Transitions the skater to `SKATING_WITHOUT_PUCK` state.

These signals will also be consumed by the game manager, UI, camera, and other systems.

### 6.4 Puck Physics

The puck has **no collision layer** (mask = 1). It bounces off everything on layer 1 (boards, goals, goalies, skater bodies) but skaters' `move_and_slide` doesn't detect it, so the puck can't push players around.

Reattach cooldown of 0.5s after any release prevents immediate re-pickup.

---

## 7. Skating & Movement

### 7.1 Movement

Screen-relative analog movement. Thrust applies up to `max_speed` — if the skater is above max speed (from a dash), thrust still applies for steering but cannot increase speed further. Friction naturally brings speed back down.

### 7.2 Dash

Instant velocity boost in the move direction. Can push the skater above `max_speed`. Full steering is available while above max speed. Friction brings speed back to normal.

| Parameter | Default |
|-----------|---------|
| `thrust` | 20.0 |
| `friction` | 5.0 |
| `max_speed` | 10.0 |
| `dash_force` | 10.0 |
| `dash_cooldown` | 1.0s |
| `dash_duration` | 0.2s |
| `brake_multiplier` | 5.0 |

### 7.3 Facing & Orientation

The skater faces the movement direction by default, lerped at `rotation_speed`. The orientation button (left trigger) flips facing to skate backward. Facing is locked during shoot states.

Movement in shoot states is reduced to 20% thrust (`shoot_mode_thrust_multiplier`).

### 7.4 Wall Squeeze

The blade's StickRaycast detects nearby walls. If the wall clamps the blade significantly from its intended position (exceeding `wall_squeeze_threshold`), the puck is released along the wall normal. This prevents carrying the puck through walls.

---

## 8. Skater State Machine

| State | Blade | Movement | Facing |
|-------|-------|----------|--------|
| `SKATING_WITHOUT_PUCK` | World-relative | Full | Normal |
| `SKATING_WITH_PUCK` | Player-relative plane | Full | Normal |
| `SHOOT_IDLE` | Frozen | Reduced (20%) | Locked |
| `WRISTER_AIM` | Frozen | Reduced (20%) | Locked (upper body aims) |
| `SLAPPER_CHARGE` | Frozen | Reduced (20%) | Locked (upper body aims) |
| `FOLLOW_THROUGH` | Frozen | Reduced (20%) | Locked |

Transitions between `SKATING_WITH_PUCK` and `SKATING_WITHOUT_PUCK` are driven by puck signals, not polled.

---

## 9. Characters & Abilities

Four character categories: **Power, Balanced, Technique, Speed**. Each character has individually tuned parameters (no rigid tier system) and a unique ability.

Positional assignment (C/W/D) during drafting determines faceoff lineups and default defensive assignments. Position doesn't change character stats — it's purely organizational.

### 9.1 Ability Design Principles

- Abilities modify physics or movement in interesting ways — never "puck goes in net"
- Simple to execute (one button press), but when and where you use it is the skill expression
- Balance is compositional: a character can be strong in isolation if they have a clear weakness that team composition can exploit

### 9.2 Dash as Universal + Tunable

Every character has a dash, but dash parameters (distance, cooldown, startup, recovery) are tuned per character. This alone creates meaningful differentiation before abilities enter the picture.

### 9.3 Draft Format

Teams take turns selecting characters from the full roster. Draft order and format TBD (snake draft, simultaneous pick, ban phase, etc.).

*Character roster design is far-horizon work. The core systems must feel right first.*

---

## 10. Game Flow & Rules

### 10.1 No Stoppages

The game never stops except for goals and faceoffs. All rule enforcement uses soft mechanical deterrents rather than whistles.

### 10.2 Faceoffs

Faceoffs occur after goals. The puck is dropped between two players and they battle for it using the existing stick control mechanics. No minigame — just a contested puck drop.

### 10.3 Soft Offsides

Player speed decays the further they are past the blue line without the puck. Prevents cherry-picking without stopping play.

### 10.4 Soft Icing

An iced puck is placed behind the net where only the defensive team can pick it up. Punishes clearing without breaking flow.

### 10.5 Defensive Assignment Indicator

Optional visual indicator showing each player which opponent to cover (man-to-man). Purely visual, togglable per player.

- Assignments initialize from faceoff positions
- Dynamic reassignment when a significant gap develops
- Brief delay (~0.5s) before confirming reassignment to prevent flickering
- Learning aid for newer players

### 10.6 Penalties

Most penalties self-regulate in 3v3 or don't need implementation. If interference becomes a problem, lean toward mechanical solutions (weakened off-puck collisions) rather than a formal penalty system.

---

## 11. AI Goalie

The goalie is a distinct entity, not a retuned skater. Detailed behavior is deferred until core systems are playable.

**Minimal goalie contract:**
- Goalie occupies the crease
- Goalie blocks shots (method TBD)
- Puck stays live off the goalie (no freezing — keeps flow)
- Puck cannot leave the rink

Current implementation: StaticBody3D with butterfly-stance collision shapes, angle-tracking with reaction lag.

---

## 12. Rink

60×26m (may reduce to 2/3 scale), corner radius 8.5m. Procedurally generated via @tool script. Ice texture with NHL lines. Board bounce 0.4. Goals at both ends, 3.4m from boards.

---

## 13. Build Order

| Stage | Description | Status |
|-------|-------------|--------|
| 1 | Skating feel | Complete |
| 2 | Stick/puck interaction | Complete |
| 3 | Basic goalie | Complete |
| 4 | Second skater + collisions | Next |
| 5 | Networking test (early validation) | Planned |
| 6 | Characters + abilities | Planned |

**Architecture targets:** Skater as reusable scene, CharacterStats resource per character, game manager for authority, input abstraction for multiplayer.

---

## 14. Open Questions

*Parked for playtesting to reveal real gaps:*

- Orientation rework: face the puck (defense) or face the net (offense) when holding orientation, vs current backward skating
- Transition blend between world-relative and player-relative blade on possession change
- Active tipping system (using quick shot button without puck to redirect shots/passes)
- Middle-zone puck reception: blade readiness check for speeds between pickup and deflect thresholds
- Elevation input redesign
- Aim assist
- Camera improvements
- Goalie body parts and detailed save mechanics
- Goal detection
- Stick checks / poke checks
- Rink size tuning
- Procedural shooting animations
- Mouse/keyboard as alternative competitive input mode
- Asymmetric blade arc further tuning
- Global stick direction at bottom net (partially addressed by `invert_plane`)
