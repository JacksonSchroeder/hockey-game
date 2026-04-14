using System;
using System.Numerics;
using HockeyGame.Logic.Interfaces;
using HockeyGame.Logic.NetworkStates;
using HockeyGame.Logic.Rules;

namespace HockeyGame.Logic.Controllers;

// Pure-C# skater state machine + input-processing core. Knows nothing about
// Godot nodes — it operates on ISkater, IPuckActions, IGameStateProvider
// interfaces injected at setup time. Godot-side SkaterController thin-wraps
// this as a Node subclass, calls ProcessInput() from _PhysicsProcess, and
// emits signals.
//
// Benefit: all the movement math, state transitions, blade control, and shot
// logic is testable with NSubstitute mocks.
public class SkaterControllerCore
{
    public enum State
    {
        SkatingWithoutPuck,
        SkatingWithPuck,
        WristerAim,
        SlapperChargeWithPuck,
        SlapperChargeWithoutPuck,
        FollowThrough,
    }

    public record Config(
        SkaterMovementRules.MovementConfig Movement,
        ShotMechanics.WristerConfig Wrister,
        ShotMechanics.SlapperConfig Slapper,
        float RotationSpeed,
        float MoveDeadzone,
        float FacingLagSpeed,
        float FacingDragSpeed,
        float BladeHeight,
        float PlaneReach,
        float ShoulderOffset,
        float BladeForehandLimit,   // degrees
        float BladeBackhandLimit,   // degrees
        float MaxMouseDistance,
        float MinBladeReach,
        float UpperBodyTwistRatio,
        float UpperBodyReturnSpeed,
        float MaxChargeDirectionVariance, // degrees
        float SlapperBladeX,
        float SlapperBladeZ,
        float SlapperAimArc,         // degrees
        float FollowThroughDuration);

    // Shot request — emitted by the controller when a shot is released. The
    // Godot-side thin wrapper subscribes and forwards to the puck.
    public event Action<Vector3, float>? PuckReleaseRequested;

    // ── Runtime ──────────────────────────────────────────────────────────────
    public State CurrentState { get; private set; } = State.SkatingWithoutPuck;
    public Vector2 Facing { get; private set; } = new(0, 1);  // matching GDScript's Vector2.DOWN
    public float UpperBodyAngle { get; private set; }
    public float BladeRelativeAngle { get; private set; }
    public bool IsElevated { get; private set; }
    public int LastProcessedSequence { get; private set; }
    public bool HasPuck { get; private set; }

    private Vector3 _shotDir;
    private float _followThroughTimer;
    private float _chargeDistance;
    private Vector3 _prevBladePos;
    private Vector3 _prevBladeDir;
    private float _slapperChargeTimer;

    private ISkater _skater = null!;
    private IPuckActions _puck = null!;
    private IGameStateProvider _gameState = null!;
    private Config _cfg = null!;

    public void Setup(
        ISkater skater,
        IPuckActions puck,
        IGameStateProvider gameState,
        Config cfg)
    {
        _skater = skater;
        _puck = puck;
        _gameState = gameState;
        _cfg = cfg;
    }

    // ── Input Processing ─────────────────────────────────────────────────────
    public void ProcessInput(InputState input, float delta)
    {
        if (input.ElevationUp) IsElevated = true;
        if (input.ElevationDown) IsElevated = false;
        _skater.IsElevated = IsElevated;

        ApplyMovement(input, delta);
        ApplyFacing(input, delta);
        ApplyState(input, delta);
        ApplyUpperBody(delta);
        _skater.UpdateStickMesh();
    }

    // ── Network state ────────────────────────────────────────────────────────
    public SkaterNetworkState GetNetworkState() => new()
    {
        Position = _skater.Position,
        Rotation = _skater.Rotation,
        Velocity = _skater.Velocity,
        BladePosition = _skater.BladePosition,
        UpperBodyRotationY = _skater.UpperBodyRotationY,
        Facing = _skater.Facing,
        LastProcessedSequence = LastProcessedSequence,
        IsGhost = _skater.IsGhost,
    };

    // ── Puck signal handlers (called from Godot wrapper when Puck signals fire)
    public void OnPuckPickedUpNetwork()
    {
        HasPuck = true;
        CurrentState = State.SkatingWithPuck;
        Vector3 localBlade = _skater.BladePosition - _skater.ShoulderPosition;
        BladeRelativeAngle = MathF.Atan2(localBlade.X, -localBlade.Z);
    }

    public void OnPuckReleasedNetwork()
    {
        if (!HasPuck) return;
        HasPuck = false;
        TransitionToSkating();
    }

    public void TeleportTo(Vector3 pos)
    {
        _skater.Position = pos;
        _skater.Velocity = Vector3.Zero;
    }

    // ── State machine ────────────────────────────────────────────────────────
    private void ApplyState(InputState input, float delta)
    {
        switch (CurrentState)
        {
            case State.SkatingWithoutPuck: StateSkatingWithoutPuck(input, delta); break;
            case State.SkatingWithPuck: StateSkatingWithPuck(input, delta); break;
            case State.WristerAim: StateWristerAim(input, delta); break;
            case State.SlapperChargeWithPuck: StateSlapperChargeWithPuck(input, delta); break;
            case State.SlapperChargeWithoutPuck: StateSlapperChargeWithoutPuck(input, delta); break;
            case State.FollowThrough: StateFollowThrough(delta); break;
        }
    }

    private void StateSkatingWithoutPuck(InputState input, float delta)
    {
        ApplyBladeFromMouse(input, delta);
        if (input.ShootPressed)
        {
            CurrentState = State.WristerAim;
            _shotDir = Vector3.Zero;
        }
        if (input.SlapPressed) EnterSlapperCharge();
    }

    private void StateSkatingWithPuck(InputState input, float delta)
    {
        ApplyBladeFromMouse(input, delta);
        if (input.ShootPressed) EnterWristerAim();
        if (input.SlapPressed) EnterSlapperCharge();
    }

    private void StateWristerAim(InputState input, float delta)
    {
        ApplyBladeFromMouse(input, delta);

        if (HasPuck)
        {
            Vector3 bladeDelta = _skater.BladePosition - _prevBladePos;
            bladeDelta.Y = 0;
            float dist = bladeDelta.Length();
            if (dist > 0.001f)
            {
                Vector3 currentDir = Vector3.Normalize(bladeDelta);
                if (_prevBladeDir != Vector3.Zero)
                {
                    float angleRad = MathF.Acos(Math.Clamp(Vector3.Dot(_prevBladeDir, currentDir), -1f, 1f));
                    float angleDeg = angleRad * 180f / MathF.PI;
                    if (angleDeg > _cfg.MaxChargeDirectionVariance) _chargeDistance = 0;
                }
                _chargeDistance += dist;
                _prevBladeDir = currentDir;
            }
        }

        _prevBladePos = _skater.BladePosition;

        if (!input.ShootHeld) ReleaseWrister(input);
    }

    private void StateSlapperChargeWithPuck(InputState input, float delta)
    {
        _slapperChargeTimer += delta;
        ApplySlapperBladePosition();

        // Slow skater down during windup
        var vel = _skater.Velocity;
        Vector2 horiz = new(vel.X, vel.Z);
        horiz = MoveToward(horiz, Vector2.Zero, _cfg.Movement.Friction * delta);
        _skater.Velocity = new Vector3(horiz.X, vel.Y, horiz.Y);

        // Upper body tracks mouse, clamped to aim arc
        Vector3 mouseWorld = new(input.MouseWorldPos.X, 0, input.MouseWorldPos.Z);
        Vector3 toMouse = mouseWorld - _skater.Position;
        toMouse.Y = 0;
        if (toMouse.Length() > _cfg.MoveDeadzone)
        {
            // Mouse direction in skater-local space — rotation is embedded in skater.Rotation.Y
            float skaterYaw = _skater.Rotation.Y;
            Matrix4x4 basis = Matrix4x4.CreateRotationY(skaterYaw);
            Vector3 local = Vector3.Transform(Vector3.Normalize(toMouse),
                Matrix4x4.Transpose(basis));
            float rawAngle = MathF.Atan2(local.X, -local.Z);
            float clamped = Math.Clamp(rawAngle,
                -_cfg.SlapperAimArc * MathF.PI / 180f,
                _cfg.SlapperAimArc * MathF.PI / 180f);
            UpperBodyAngle = LerpAngle(UpperBodyAngle, -clamped, _cfg.UpperBodyReturnSpeed * delta);
            _skater.SetUpperBodyRotation(UpperBodyAngle);
        }

        if (!input.SlapHeld) ReleaseSlapper(input);
    }

    private void StateSlapperChargeWithoutPuck(InputState input, float delta)
    {
        _slapperChargeTimer += delta;
        ApplySlapperBladePosition();

        Vector3 mouseWorld = new(input.MouseWorldPos.X, 0, input.MouseWorldPos.Z);
        Vector2 toMouse = new(mouseWorld.X - _skater.Position.X, mouseWorld.Z - _skater.Position.Z);
        if (toMouse.Length() > _cfg.MoveDeadzone)
        {
            Facing = Vector2.Lerp(Facing, Vector2.Normalize(toMouse), _cfg.RotationSpeed * delta);
            Facing = Vector2.Normalize(Facing);
            _skater.SetFacing(Facing);
        }

        if (!input.SlapHeld) ReleaseSlapper(input);
    }

    private void StateFollowThrough(float delta)
    {
        ApplyBladeFromRelativeAngle();
        _followThroughTimer -= delta;
        if (_followThroughTimer <= 0) TransitionToSkating();
    }

    // ── State helpers ────────────────────────────────────────────────────────
    private void TransitionToSkating()
    {
        CurrentState = HasPuck ? State.SkatingWithPuck : State.SkatingWithoutPuck;
        _shotDir = Vector3.Zero;
        UpperBodyAngle = 0;
    }

    private void EnterWristerAim()
    {
        CurrentState = State.WristerAim;
        _shotDir = Vector3.Zero;
        _chargeDistance = 0;
        _prevBladePos = _skater.BladePosition;
        _prevBladeDir = Vector3.Zero;
    }

    private void EnterSlapperCharge()
    {
        _slapperChargeTimer = 0;
        _shotDir = Vector3.Zero;
        UpperBodyAngle = 0;
        _skater.SetUpperBodyRotation(0);
        CurrentState = HasPuck ? State.SlapperChargeWithPuck : State.SlapperChargeWithoutPuck;
    }

    private void ReleaseWrister(InputState input)
    {
        if (HasPuck)
        {
            var result = ShotMechanics.ReleaseWrister(
                playerPos: _skater.Position,
                mouseWorldPos: input.MouseWorldPos,
                bladeWorldPos: _skater.UpperBodyToGlobal(_skater.BladePosition),
                bladeLocalPos: _skater.BladePosition,
                shoulderLocalPos: _skater.ShoulderPosition,
                isLeftHanded: _skater.IsLeftHanded,
                isElevated: IsElevated,
                chargeDistance: _chargeDistance,
                cfg: _cfg.Wrister);
            PuckReleaseRequested?.Invoke(result.Direction, result.Power);
        }
        CurrentState = State.FollowThrough;
        _followThroughTimer = _cfg.FollowThroughDuration;
    }

    private void ReleaseSlapper(InputState input)
    {
        if (HasPuck)
        {
            var result = ShotMechanics.ReleaseSlapper(
                bladeWorldPos: _skater.UpperBodyToGlobal(_skater.BladePosition),
                mouseWorldPos: input.MouseWorldPos,
                isElevated: IsElevated,
                chargeTime: _slapperChargeTimer,
                cfg: _cfg.Slapper);
            PuckReleaseRequested?.Invoke(result.Direction, result.Power);
        }
        CurrentState = State.FollowThrough;
        _followThroughTimer = _cfg.FollowThroughDuration;
    }

    private void ApplySlapperBladePosition()
    {
        float handSign = _skater.IsLeftHanded ? -1f : 1f;
        Vector3 pos = _skater.ShoulderPosition + new Vector3(
            handSign * _cfg.SlapperBladeX,
            _cfg.BladeHeight,
            _cfg.SlapperBladeZ);
        _skater.SetBladePosition(pos);
    }

    private bool IsInSlapperState() =>
        CurrentState is State.SlapperChargeWithPuck or State.SlapperChargeWithoutPuck;

    // ── Blade control ────────────────────────────────────────────────────────
    private void ApplyBladeFromMouse(InputState input, float delta)
    {
        Vector3 mouseWorld = new(input.MouseWorldPos.X, 0, input.MouseWorldPos.Z);
        Vector3 shoulderWorld = _skater.UpperBodyToGlobal(_skater.ShoulderPosition);
        shoulderWorld.Y = 0;
        Vector3 toMouse = mouseWorld - shoulderWorld;
        if (toMouse.Length() < 0.01f) return;

        Vector3 localToMouse = _skater.UpperBodyToLocal(shoulderWorld + Vector3.Normalize(toMouse));
        localToMouse.Y = 0;
        Vector3 fromShoulder = localToMouse - _skater.ShoulderPosition;
        float rawAngle = MathF.Atan2(fromShoulder.X, -fromShoulder.Z);

        float handSign = _skater.IsLeftHanded ? -1f : 1f;
        float handedAngle = rawAngle * handSign;
        float foreLimit = _cfg.BladeForehandLimit * MathF.PI / 180f;
        float backLimit = _cfg.BladeBackhandLimit * MathF.PI / 180f;
        float clampedHanded = Math.Clamp(handedAngle, -backLimit, foreLimit);
        float clampedAngle = clampedHanded * handSign;

        if (!IsInSlapperState() && CurrentState != State.WristerAim)
        {
            if (MathF.Abs(handedAngle) > MathF.Abs(clampedHanded))
            {
                float excess = (handedAngle - clampedHanded) * handSign;
                Facing = RotateVec2(Facing, excess * _cfg.FacingDragSpeed * delta);
                Facing = Vector2.Normalize(Facing);
                _skater.SetFacing(Facing);
            }
        }

        Vector3 clampedDir = new(MathF.Sin(clampedAngle), 0, -MathF.Cos(clampedAngle));
        float t = Math.Clamp(toMouse.Length() / _cfg.MaxMouseDistance, 0f, 1f);
        float reach = _cfg.MinBladeReach + (_cfg.PlaneReach - _cfg.MinBladeReach) * t;
        Vector3 clampedTarget = _skater.ShoulderPosition + clampedDir * reach;
        clampedTarget.Y = _cfg.BladeHeight;

        Vector3 intendedPos = clampedTarget;
        clampedTarget = _skater.ClampBladeToWalls(clampedTarget);

        if (HasPuck)
        {
            float squeeze = _skater.GetWallSqueeze(intendedPos, clampedTarget);
            if (ShotMechanics.ShouldReleaseOnWallPin(squeeze, _skater.WallSqueezeThreshold))
            {
                Vector3 wallNormal = _skater.GetBladeWallNormal();
                Vector3 releaseDir = wallNormal.Length() > 0
                    ? Vector3.Normalize(wallNormal)
                    : Vector3.Normalize(-clampedTarget);  // fallback: push away
                PuckReleaseRequested?.Invoke(releaseDir, 3.0f);
            }
        }

        _skater.SetBladePosition(clampedTarget);
        BladeRelativeAngle = clampedAngle;
    }

    private void ApplyBladeFromRelativeAngle()
    {
        Vector3 localDir = new(MathF.Sin(BladeRelativeAngle), 0, -MathF.Cos(BladeRelativeAngle));
        Vector3 localTarget = _skater.ShoulderPosition + localDir * _cfg.PlaneReach;
        localTarget.Y = _cfg.BladeHeight;
        localTarget = _skater.ClampBladeToWalls(localTarget);
        _skater.SetBladePosition(localTarget);
    }

    // ── Upper body, facing, movement ─────────────────────────────────────────
    private void ApplyUpperBody(float delta)
    {
        if (CurrentState == State.SlapperChargeWithPuck) return;

        float targetAngle = 0;
        Vector3 bladePos = _skater.BladePosition - _skater.ShoulderPosition;
        bladePos.Y = 0;
        if (bladePos.Length() > 0.01f)
        {
            float bladeAngle = MathF.Atan2(bladePos.X, -bladePos.Z);
            targetAngle = -bladeAngle * _cfg.UpperBodyTwistRatio;
        }
        UpperBodyAngle = LerpAngle(UpperBodyAngle, targetAngle, _cfg.UpperBodyReturnSpeed * delta);
        _skater.SetUpperBodyRotation(UpperBodyAngle);
    }

    private void ApplyFacing(InputState input, float delta)
    {
        if (CurrentState is State.WristerAim
            or State.SlapperChargeWithPuck
            or State.SlapperChargeWithoutPuck) return;

        if (input.FacingHeld)
        {
            Vector3 mouseWorld = input.MouseWorldPos;
            Vector2 toMouse = new(mouseWorld.X - _skater.Position.X, mouseWorld.Z - _skater.Position.Z);
            if (toMouse.Length() > _cfg.MoveDeadzone)
            {
                Facing = Vector2.Lerp(Facing, Vector2.Normalize(toMouse), _cfg.RotationSpeed * delta);
                Facing = Vector2.Normalize(Facing);
            }
        }
        else if (input.MoveVector.Length() > _cfg.MoveDeadzone)
        {
            Facing = Vector2.Lerp(Facing, Vector2.Normalize(input.MoveVector), _cfg.FacingLagSpeed * delta);
            Facing = Vector2.Normalize(Facing);
        }

        _skater.SetFacing(Facing);
    }

    private void ApplyMovement(InputState input, float delta)
    {
        if (CurrentState == State.SlapperChargeWithPuck) return;

        _skater.Velocity = SkaterMovementRules.ApplyMovement(
            currentVelocity: _skater.Velocity,
            moveInput: input.MoveVector,
            skaterFacingRotationY: _skater.Rotation.Y,
            hasPuck: HasPuck,
            brake: input.Brake,
            delta: delta,
            cfg: _cfg.Movement);
    }

    // ── Math helpers ─────────────────────────────────────────────────────────
    private static float LerpAngle(float a, float b, float t)
    {
        float diff = b - a;
        while (diff > MathF.PI) diff -= 2 * MathF.PI;
        while (diff < -MathF.PI) diff += 2 * MathF.PI;
        return a + diff * Math.Clamp(t, 0f, 1f);
    }

    private static Vector2 RotateVec2(Vector2 v, float angleRad)
    {
        float cos = MathF.Cos(angleRad);
        float sin = MathF.Sin(angleRad);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private static Vector2 MoveToward(Vector2 from, Vector2 to, float delta)
    {
        Vector2 diff = to - from;
        float len = diff.Length();
        if (len <= delta || len == 0) return to;
        return from + diff / len * delta;
    }
}
