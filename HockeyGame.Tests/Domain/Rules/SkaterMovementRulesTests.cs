using System.Numerics;
using HockeyGame.Logic.Rules;
using Xunit;

namespace HockeyGame.Tests.Domain.Rules;

public class SkaterMovementRulesTests
{
    private static readonly SkaterMovementRules.MovementConfig DefaultCfg = new(
        Thrust: 20f,
        Friction: 5f,
        MaxSpeed: 10f,
        MoveDeadzone: 0.1f,
        BrakeMultiplier: 5f,
        PuckCarrySpeedMultiplier: 0.88f,
        BackwardThrustMultiplier: 0.7f,
        CrossoverThrustMultiplier: 0.85f);

    [Fact]
    public void NoInput_AppliesFriction()
    {
        var result = SkaterMovementRules.ApplyMovement(
            currentVelocity: new Vector3(5, 0, 0),
            moveInput: Vector2.Zero,
            skaterFacingRotationY: 0f,
            hasPuck: false,
            brake: false,
            delta: 0.1f,
            cfg: DefaultCfg);
        // Friction decelerates — new speed less than 5
        Assert.True(new Vector2(result.X, result.Z).Length() < 5f);
    }

    [Fact]
    public void Input_AppliesThrust()
    {
        var result = SkaterMovementRules.ApplyMovement(
            currentVelocity: Vector3.Zero,
            moveInput: new Vector2(1, 0),
            skaterFacingRotationY: 0f,
            hasPuck: false,
            brake: false,
            delta: 0.1f,
            cfg: DefaultCfg);
        Assert.True(result.X > 0);
    }

    [Fact]
    public void Brake_AppliesStrongerFriction()
    {
        var noBrake = SkaterMovementRules.ApplyMovement(
            new Vector3(5, 0, 0), Vector2.Zero, 0f, false, brake: false, 0.1f, DefaultCfg);
        var withBrake = SkaterMovementRules.ApplyMovement(
            new Vector3(5, 0, 0), Vector2.Zero, 0f, false, brake: true, 0.1f, DefaultCfg);
        Assert.True(withBrake.Length() < noBrake.Length());
    }

    [Fact]
    public void HasPuck_ReducesMaxSpeed()
    {
        // Accelerate for a long time so we hit the speed cap.
        var v = Vector3.Zero;
        for (int i = 0; i < 1000; i++)
        {
            v = SkaterMovementRules.ApplyMovement(
                v, new Vector2(1, 0), 0f, hasPuck: false, false, 0.01f, DefaultCfg);
        }
        float noPuckSpeed = new Vector2(v.X, v.Z).Length();

        v = Vector3.Zero;
        for (int i = 0; i < 1000; i++)
        {
            v = SkaterMovementRules.ApplyMovement(
                v, new Vector2(1, 0), 0f, hasPuck: true, false, 0.01f, DefaultCfg);
        }
        float puckSpeed = new Vector2(v.X, v.Z).Length();

        // With puck, max speed is 0.88× → should settle lower
        Assert.True(puckSpeed < noPuckSpeed);
        Assert.True(puckSpeed < DefaultCfg.MaxSpeed);
    }

    [Fact]
    public void DeadzoneInput_TreatedAsNoInput()
    {
        // Tiny input magnitude below deadzone
        var result = SkaterMovementRules.ApplyMovement(
            currentVelocity: new Vector3(5, 0, 0),
            moveInput: new Vector2(0.01f, 0),
            skaterFacingRotationY: 0f,
            hasPuck: false,
            brake: false,
            delta: 0.1f,
            cfg: DefaultCfg);
        // Friction applied, no thrust — so speed decreased
        Assert.True(new Vector2(result.X, result.Z).Length() < 5f);
    }
}
