using System.Numerics;
using HockeyGame.Logic.Config;
using HockeyGame.Logic.Rules;
using Xunit;

namespace HockeyGame.Tests.Domain.Rules;

public class GoalieBehaviorRulesTests
{
    private static readonly GoalieConfig Cfg = GoalieConfig.Default;

    // Shot detection

    [Fact]
    public void SlowPuck_NotAShot()
    {
        var result = GoalieBehaviorRules.DetectShot(
            puckPosition: new Vector3(0, 0, 10),
            puckVelocity: new Vector3(0, 0, -1), // below threshold
            goalLineZ: 26.6f, goalCenterX: 0f, cfg: Cfg);
        Assert.Null(result);
    }

    [Fact]
    public void FastPuckOnTarget_IsShot()
    {
        var result = GoalieBehaviorRules.DetectShot(
            puckPosition: new Vector3(0, 0, 10),
            puckVelocity: new Vector3(0, 0, 20), // heading toward +Z goal
            goalLineZ: 26.6f, goalCenterX: 0f, cfg: Cfg);
        Assert.Equal(Cfg.ReactionDelay, result);
    }

    [Fact]
    public void FastPuckAwayFromGoal_NotAShot()
    {
        var result = GoalieBehaviorRules.DetectShot(
            puckPosition: new Vector3(0, 0, 10),
            puckVelocity: new Vector3(0, 0, -20), // away from +Z goal
            goalLineZ: 26.6f, goalCenterX: 0f, cfg: Cfg);
        Assert.Null(result);
    }

    [Fact]
    public void FastPuckWideOfPost_NotAShot()
    {
        // Projected X far outside net
        var result = GoalieBehaviorRules.DetectShot(
            puckPosition: new Vector3(10, 0, 10),
            puckVelocity: new Vector3(10, 0, 5), // drifting more wide as it travels
            goalLineZ: 26.6f, goalCenterX: 0f, cfg: Cfg);
        Assert.Null(result);
    }

    // Defensive zone

    [Fact]
    public void PuckBehindGoal_InDefensiveZone()
    {
        // Team 0 goalie (direction_sign=+1) at z=26.6. Puck behind means z > 26.6
        Assert.True(GoalieBehaviorRules.IsPuckInDefensiveZone(
            puckPosition: new Vector3(0, 0, 28),
            goalLineZ: 26.6f, goalCenterX: 0, directionSign: 1, cfg: Cfg));
    }

    [Fact]
    public void PuckFarFromGoal_NotInDefensiveZone()
    {
        Assert.False(GoalieBehaviorRules.IsPuckInDefensiveZone(
            puckPosition: new Vector3(0, 0, 10),
            goalLineZ: 26.6f, goalCenterX: 0, directionSign: 1, cfg: Cfg));
    }

    [Fact]
    public void PuckNearPostAtSharpAngle_InDefensiveZone()
    {
        // Within zone_post_z (2.0) and at a sharp angle — simulates corner play
        Assert.True(GoalieBehaviorRules.IsPuckInDefensiveZone(
            puckPosition: new Vector3(3, 0, 25.5f), // close in z, offset in x → sharp angle
            goalLineZ: 26.6f, goalCenterX: 0, directionSign: 1, cfg: Cfg));
    }

    // Depth zones

    [Fact]
    public void DepthAtPost_LerpToAggressive()
    {
        // At puck_z_dist = zone_post_z, should be exactly at depth_aggressive
        float d = GoalieBehaviorRules.TargetDepthForPuckDistance(Cfg.ZonePostZ, Cfg);
        Assert.Equal(Cfg.DepthAggressive, d, 3);
    }

    [Fact]
    public void DepthInAggressiveZone_IsAggressive()
    {
        float d = GoalieBehaviorRules.TargetDepthForPuckDistance(
            (Cfg.ZonePostZ + Cfg.ZoneAggressiveZ) / 2, Cfg);
        Assert.Equal(Cfg.DepthAggressive, d, 3);
    }

    [Fact]
    public void DepthFarAway_IsDefensive()
    {
        float d = GoalieBehaviorRules.TargetDepthForPuckDistance(100f, Cfg);
        Assert.Equal(Cfg.DepthDefensive, d, 3);
    }

    // Lateral positioning

    [Fact]
    public void TargetLateralX_ClampsToNetWidth()
    {
        // Puck at extreme X angle → target should clamp
        float target = GoalieBehaviorRules.TargetLateralX(
            puckPosition: new Vector3(100, 0, 10),
            goalLineZ: 26.6f, goalCenterX: 0f, currentDepth: 0.5f, cfg: Cfg);
        Assert.True(target <= Cfg.NetHalfWidth + 0.001f);
    }

    [Fact]
    public void TargetLateralX_TracksAlongShotLine()
    {
        // Puck at x=1, z=goal_line - 10 (10 units out), depth=1.0 → target should be x=0.1
        float target = GoalieBehaviorRules.TargetLateralX(
            puckPosition: new Vector3(1, 0, 16.6f), // 10 units from goal line
            goalLineZ: 26.6f, goalCenterX: 0f, currentDepth: 1.0f, cfg: Cfg);
        Assert.Equal(0.1f, target, 2);
    }
}
