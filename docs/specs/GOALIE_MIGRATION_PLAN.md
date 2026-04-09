# Goalie Body Part Migration Plan

Migrate goalie body part sizes and per-state positions from the initial implementation values to the revised values agreed on in design mode. **No changes to the state machine, transitions, shot detection, or networking.** This is purely a size/position/rotation update plus adding a head part.

---

## Summary of Changes

1. **Add a Head body part** — new 7th collision shape
2. **Update all fixed sizes** — pads much taller, body taller and narrower, depths more realistic
3. **Update all per-state position/rotation configs** — positions reworked for proper proportions
4. **Add Head to the scene tree** — same pattern as other parts (StaticBody3D with CollisionShape3D + MeshInstance3D)
5. **Add Head to body config dictionaries** — needs entries in all four state configs
6. **Fix RVH config/post mapping bug** — RVH_LEFT and RVH_RIGHT configs are swapped (design drawings were from shooter's perspective, implementation is in goalie's local space)

---

## New Fixed Sizes

Replace the current fixed sizes with these. The big changes: pads go from 0.50h to 0.84h (33" realistic pads), body goes from 0.48w/0.55h to 0.40w/0.60h (narrower, taller), and depths are more realistic.

| Part | Width (X) | Height (Y) | Depth (Z) | Change from current |
|------|-----------|------------|-----------|-------------------|
| Pad (x2) | 0.28 | **0.84** | **0.15** | h: 0.50→0.84, d: 0.08→0.15 |
| Body | **0.40** | **0.60** | **0.25** | w: 0.48→0.40, h: 0.55→0.60, d: 0.20→0.25 |
| Head | **0.22** | **0.22** | **0.20** | **NEW PART** |
| Glove | 0.25 | 0.25 | **0.15** | d: 0.10→0.15 |
| Blocker | 0.20 | 0.30 | **0.10** | d: unchanged |
| Stick | 0.50 | 0.04 | 0.04 | unchanged |

---

## New Per-State Configs

All positions are local to goalie root. Y=0 is ice level. Catches left (blocker screen-left, glove screen-right from shooter view). Rotations on Z axis (front view).

### STANDING

| Part | Position (x, y, z) | Rotation Z | Notes |
|------|---------------------|-----------|-------|
| Left pad | (-0.22, 0.0, 0.0) | 12° | Same x, same rot — pad height change is the big difference |
| Right pad | (0.22, 0.0, 0.0) | -12° | Mirror |
| Body | (0.0, **0.72**, 0.0) | 0° | Was 0.50 — raised to account for taller pads, bottom at 0.42 overlaps pad tops |
| **Head** | **(0.0, 1.25, 0.0)** | **0°** | **NEW** — neck gap above body, pokes above crossbar |
| Blocker | (-0.35, **0.75**, 0.0) | 0° | Was 0.43 — raised to match new body height |
| Glove | (0.38, **0.80**, 0.0) | 0° | Was 0.48 — raised to match |
| Stick | (0.0, 0.02, **-0.25**) | 0° | Unchanged (already corrected to negative Z) |

### BUTTERFLY

| Part | Position (x, y, z) | Rotation Z | Notes |
|------|---------------------|-----------|-------|
| Left pad | (**-0.42**, 0.0, 0.0) | 90° | Was -0.33 — moved out so inner edges barely touch at center |
| Right pad | (**0.42**, 0.0, 0.0) | -90° | Mirror |
| Body | (0.0, **0.32**, 0.0) | 0° | Was 0.28 — slightly higher, bottom at 0.02 |
| **Head** | **(0.0, 0.85, 0.0)** | **0°** | **NEW** — dropped proportionally |
| Blocker | (-0.42, 0.30, 0.0) | 0° | Unchanged |
| Glove | (0.46, 0.35, 0.0) | 0° | Was 0.45 — minor tweak |
| Stick | (0.0, 0.02, -0.30) | 0° | Unchanged |

Key change: pads at x=±0.42 means inner edges just touch at center (pad width 0.28 rotated 90° gives 0.84m displayed width, center of pad at 0.42 means inner edge at 0.42 - 0.84/2 = 0.0). Five-hole is sealed when set but opens immediately on any lateral butterfly slide.

### RVH_RIGHT (blocker side)

| Part | Position (x, y, z) | Rotation Z | Notes |
|------|---------------------|-----------|-------|
| Right pad (post) | (0.46, 0.0, 0.0) | -90° | Unchanged |
| Left pad (back leg) | (**0.05**, 0.0, 0.0) | **60°** | Was (0.18, 5°) — moved away from post, much steeper angle for realistic RVH anchor |
| Body | (**0.52**, **0.52**, 0.0) | 0° | Was (0.30, 0.42) — pushed closer to post and lowered, bottom at 0.22 seals short side gap |
| **Head** | **(0.52, 1.05, 0.0)** | **0°** | **NEW** |
| Blocker | (**0.62**, **0.55**, 0.0) | 0° | Was (0.48, 0.45) — pushed to post to seal high |
| Glove | (**0.10**, **0.50**, 0.0) | 0° | Was (0.0, 0.40) — slight adjustment |
| Stick | (0.30, 0.02, -0.20) | 0° | Was (0.25, ...) — minor tweak |

### RVH_LEFT (glove side)

| Part | Position (x, y, z) | Rotation Z | Notes |
|------|---------------------|-----------|-------|
| Left pad (post) | (-0.46, 0.0, 0.0) | 90° | Unchanged |
| Right pad (back leg) | (**-0.05**, 0.0, 0.0) | **-60°** | Mirror of right |
| Body | (**-0.52**, **0.52**, 0.0) | 0° | Mirror |
| **Head** | **(-0.52, 1.05, 0.0)** | **0°** | **NEW** |
| Glove | (**-0.62**, **0.55**, 0.0) | 0° | Pushed to post |
| Blocker | (**-0.10**, **0.50**, 0.0) | 0° | Mirror |
| Stick | (-0.30, 0.02, -0.20) | 0° | Mirror |

---

## Top-Down Z Positions (depth layering)

These Z positions define where parts sit front-to-back. Negative Z is forward (toward shooter) in Godot local space. Apply to all stances unless the stance-specific config overrides.

**Standing:**
| Part | Z position | Notes |
|------|-----------|-------|
| Stick | -0.30 | Furthest forward, covers five-hole |
| Pads | -0.20 | Front line blocking surface |
| Glove | -0.18 | Slightly behind pads |
| Blocker | -0.18 | Slightly behind pads |
| Body | 0.0 | On goal line |
| Head | 0.08 | Behind body |

**Butterfly:** Same layering, stick pushed to -0.35.

**RVH:** Stick angled, Z positions adjusted per the stance-specific configs. Back leg at -0.15, post pad at -0.10, body at 0.05 (slightly behind goal line).

> Note: The current implementation may have all Z positions at 0.0. If so, update them to these values. If the current code already handles Z positions in the body configs, update the values.

---

## Bug Fix: RVH Config/Post Mapping

The original design spec's body part positions were drawn from the **shooter's perspective** (looking into the net). But the goalie's local space in Godot has -Z as forward, which means the goalie's local left/right are flipped relative to the drawings.

The result: the goalie correctly moves to the right post when the puck is on the right, but applies the RVH_LEFT body config (and vice versa). The pose looks mirrored — blocker sealing the post where the glove should be, body shifted the wrong way.

**Fix:** Swap which config is used for which post. The config position values themselves are correct — they just need to be applied to the opposite post. Either:
- Swap the config names (rename what's currently RVH_RIGHT to RVH_LEFT and vice versa), OR
- Swap the state transition logic (when puck is on the left, use RVH_RIGHT config and vice versa)

Pick whichever is cleaner in the actual code. The end result should be: when the puck is behind the left post, the **glove** seals that post (for a catches-left goalie). When behind the right post, the **blocker** seals it.

---

## Implementation Steps

1. **Add Head to the goalie scene.** Create a new `StaticBody3D` child (same pattern as Body, Glove, etc.) with a `CollisionShape3D` (BoxShape3D 0.22 x 0.22 x 0.20) and `MeshInstance3D` (BoxMesh same size, give it a distinct debug color — purple suggested). Add `@onready var head: Node3D` reference in `goalie.gd`.

2. **Update fixed sizes in the body config code.** Find where `BoxShape3D` sizes are set (likely in `goalie.gd` or wherever the body configs are defined). Update to the new values from the table above. Pad height 0.50→0.84 is the biggest change.

3. **Update per-state position/rotation configs.** Find the dictionaries or match statements that define body part positions per state. Update all four states (STANDING, BUTTERFLY, RVH_LEFT, RVH_RIGHT) to the new values. Add head entries to each.

4. **Fix RVH config/post mapping.** Swap which config is applied to which post so that for a catches-left goalie: left post = glove seals, right post = blocker seals. See the "Bug Fix" section above.

5. **Update Z positions.** Add the depth layering Z values. The pads should be in front of the body, the stick in front of the pads.

6. **Update the body part lerp code to include head.** Wherever the code iterates over body parts to lerp positions/rotations, make sure head is included.

7. **Test all four stances visually.** Switch between states and verify:
   - Standing: pads on ice angled inward, body overlaps pad tops, head above crossbar with neck gap
   - Butterfly: pads flat touching at center, body dropped, head mid-net
   - RVH right: back leg at 60° spread away from post, body tight to RIGHT post, **blocker at post** (catches-left goalie)
   - RVH left: mirror of right, body tight to LEFT post, **glove at post** (catches-left goalie)
   - Verify from both the top-down camera AND the shooter's perspective that the correct hand is at the post

8. **Test five-hole behavior.** Verify that butterfly pads touching at center means five-hole opens on lateral butterfly slides.

9. **Test shot blocking.** Fire pucks at the goalie in each stance and verify coverage matches expectations — top corners open in standing, top third open in butterfly, high corner over post-side shoulder open in RVH.

---

## What Does NOT Change

- State machine logic (STANDING, BUTTERFLY, RVH_LEFT, RVH_RIGHT)
- Transition conditions and timers
- Shot detection (puck_released signal, velocity/direction/projection checks)
- Buckley depth system (zone boundaries, depth values, lerping)
- Lateral movement (shuffle vs T-push, lateral_threshold)
- Five-hole mechanic (proportional to lateral speed)
- Facing logic (rotation toward puck, clamping, freeze during shot)
- Networking approach
- Goal assignment / direction_sign
- Any @export tuning parameters (except adding head-related ones if needed)
