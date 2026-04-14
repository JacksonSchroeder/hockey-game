using System.Numerics;
using HockeyGame.Logic.Rules;
using Xunit;

namespace HockeyGame.Tests.Domain.Rules;

public class PuckCollisionRulesTests
{
    [Fact]
    public void CanPokeCheck_RejectsSameTeam()
    {
        Assert.False(PuckCollisionRules.CanPokeCheck(carrierTeamId: 0, checkerTeamId: 0));
        Assert.False(PuckCollisionRules.CanPokeCheck(carrierTeamId: 1, checkerTeamId: 1));
    }

    [Fact]
    public void CanPokeCheck_AllowsOpponent()
    {
        Assert.True(PuckCollisionRules.CanPokeCheck(carrierTeamId: 0, checkerTeamId: 1));
        Assert.True(PuckCollisionRules.CanPokeCheck(carrierTeamId: 1, checkerTeamId: 0));
    }

    [Fact]
    public void DeflectOffBlade_FullReflection_ReversesDirection()
    {
        // Puck moving +X, blade normal is -X (blade face pointing back at puck)
        var velocity = new Vector3(10, 0, 0);
        var normal = new Vector3(-1, 0, 0);
        var result = PuckCollisionRules.DeflectOffBlade(velocity, normal, deflectBlend: 1.0f, speedRetain: 1.0f);
        // Pure reflection off -X normal flips X sign
        Assert.True(result.X < 0);
        Assert.Equal(10f, result.Length(), 2);
    }

    [Fact]
    public void DeflectOffBlade_SpeedRetainLossesEnergy()
    {
        var velocity = new Vector3(10, 0, 0);
        var normal = new Vector3(-1, 0, 0);
        var result = PuckCollisionRules.DeflectOffBlade(velocity, normal, 1.0f, speedRetain: 0.5f);
        Assert.Equal(5f, result.Length(), 2);
    }

    [Fact]
    public void ApplyDeflectionElevation_AddsYComponent()
    {
        var horiz = new Vector3(1, 0, 0);
        var elevated = PuckCollisionRules.ApplyDeflectionElevation(horiz, 35.0f);
        Assert.True(elevated.Y > 0);
        Assert.Equal(1f, elevated.Length(), 2); // still normalized
    }

    [Fact]
    public void BodyCheckStripVelocity_ScalesDirection()
    {
        var hitDir = new Vector3(1, 0, 0);
        var result = PuckCollisionRules.BodyCheckStripVelocity(hitDir, puckSpeed: 5f);
        Assert.Equal(new Vector3(5, 0, 0), result);
    }

    [Fact]
    public void PokeStripVelocity_BlendsWhenCheckerMoving()
    {
        var checkerVel = new Vector3(2, 0, 0);
        var carrierVel = new Vector3(0, 0, 1);
        var result = PuckCollisionRules.PokeStripVelocity(
            checkerVel, carrierVel,
            carrierPos: Vector3.Zero, checkerPos: Vector3.Zero,
            carrierVelBlend: 0.5f, stripSpeed: 6f,
            fallbackDirection: Vector3.UnitX);
        // Blended direction is (2, 0, 0) + (0, 0, 0.5) → (2, 0, 0.5) normalized
        Assert.Equal(6f, result.Length(), 2);
        Assert.True(result.X > 0);
        Assert.True(result.Z > 0);
    }

    [Fact]
    public void PokeStripVelocity_UsesPositionDelta_WhenCheckerStill()
    {
        // Checker not moving → strip direction = carrier_pos - checker_pos
        var result = PuckCollisionRules.PokeStripVelocity(
            checkerBladeVel: Vector3.Zero,
            carrierBladeVel: Vector3.Zero,
            carrierPos: new Vector3(3, 0, 0),
            checkerPos: new Vector3(0, 0, 0),
            carrierVelBlend: 0.5f, stripSpeed: 6f,
            fallbackDirection: Vector3.UnitX);
        Assert.True(result.X > 0);
        Assert.Equal(6f, result.Length(), 2);
    }

    [Fact]
    public void BodyBlockVelocity_ReflectsAndDampens()
    {
        var velocity = new Vector3(10, 0, 0);
        var normal = new Vector3(-1, 0, 0);
        var result = PuckCollisionRules.BodyBlockVelocity(velocity, normal, dampen: 0.5f);
        // Reflected (flipped X) and speed halved
        Assert.True(result.X < 0);
        Assert.Equal(5f, result.Length(), 2);
    }
}
