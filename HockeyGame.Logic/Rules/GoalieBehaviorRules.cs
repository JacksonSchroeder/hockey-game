using System;
using System.Numerics;
using HockeyGame.Logic.Config;

namespace HockeyGame.Logic.Rules;

// Pure goalie behavior math. Extracted from GoalieController's shot detection,
// defensive zone, and depth zone calculations. Consumes GoalieConfig.
//
// directionSign conventions (from the original code):
//   +1 = goalie defends the +Z goal (Team 0)
//   -1 = goalie defends the -Z goal (Team 1)
public static class GoalieBehaviorRules
{
    // Is a released puck on course to hit the net? If so, return reaction_delay
    // to start a butterfly drop timer.
    // Returns null if this isn't a shot the goalie should react to.
    public static float? DetectShot(
        Vector3 puckPosition,
        Vector3 puckVelocity,
        float goalLineZ,
        float goalCenterX,
        GoalieConfig cfg)
    {
        if (puckVelocity.Length() < cfg.ShotSpeedThreshold) return null;
        if (MathF.Abs(puckVelocity.Z) < 0.001f) return null;
        float tToGoal = (goalLineZ - puckPosition.Z) / puckVelocity.Z;
        if (tToGoal <= 0.0f) return null;
        float projectedX = puckPosition.X + puckVelocity.X * tToGoal;
        if (MathF.Abs(projectedX - goalCenterX) > cfg.NetHalfWidth + cfg.NetMargin) return null;
        return cfg.ReactionDelay;
    }

    // Is the puck in the goalie's defensive zone? Triggers RVH post-hug.
    // Defensive zone = either behind the goal line OR within zone_post_z at a sharp angle.
    public static bool IsPuckInDefensiveZone(
        Vector3 puckPosition,
        float goalLineZ,
        float goalCenterX,
        int directionSign,
        GoalieConfig cfg)
    {
        // "Behind goal" = puck is on the far side of the goal line from the rink center.
        bool behindGoal = (puckPosition.Z - goalLineZ) * directionSign < 0.0f;
        if (behindGoal) return true;
        float puckZDist = MathF.Abs(puckPosition.Z - goalLineZ);
        if (puckZDist > cfg.ZonePostZ) return false;
        float puckAngle = MathF.Atan2(
            MathF.Abs(puckPosition.X - goalCenterX),
            MathF.Max(puckZDist, 0.01f));
        float earlyAngleRad = cfg.RvhEarlyAngle * MathF.PI / 180.0f;
        return puckAngle >= earlyAngleRad;
    }

    // Depth zone math: map puck distance from goal line to target depth using the
    // Buckley chart (aggressive close, conservative as puck retreats, defensive far away).
    public static float TargetDepthForPuckDistance(float puckZDist, GoalieConfig cfg)
    {
        if (puckZDist <= cfg.ZonePostZ)
        {
            float t = puckZDist / cfg.ZonePostZ;
            return Lerp(cfg.DepthDefensive, cfg.DepthAggressive, t);
        }
        if (puckZDist <= cfg.ZoneAggressiveZ)
            return cfg.DepthAggressive;
        if (puckZDist <= cfg.ZoneBaseZ)
        {
            float t = (puckZDist - cfg.ZoneAggressiveZ) / (cfg.ZoneBaseZ - cfg.ZoneAggressiveZ);
            return Lerp(cfg.DepthAggressive, cfg.DepthBase, t);
        }
        if (puckZDist <= cfg.ZoneConservativeZ)
        {
            float t = (puckZDist - cfg.ZoneBaseZ) / (cfg.ZoneConservativeZ - cfg.ZoneBaseZ);
            return Lerp(cfg.DepthBase, cfg.DepthConservative, t);
        }
        {
            float t = Clamp01((puckZDist - cfg.ZoneConservativeZ) / cfg.ZoneConservativeZ);
            return Lerp(cfg.DepthConservative, cfg.DepthDefensive, t);
        }
    }

    // Lateral target X: project puck onto the goalie's current depth along the shot line.
    // Clamped to the net width so the goalie never strays past the posts.
    public static float TargetLateralX(
        Vector3 puckPosition,
        float goalLineZ,
        float goalCenterX,
        float currentDepth,
        GoalieConfig cfg)
    {
        float puckZDist = MathF.Abs(puckPosition.Z - goalLineZ);
        float targetX;
        if (puckZDist > 0.01f)
        {
            targetX = goalCenterX + (puckPosition.X - goalCenterX) * (currentDepth / puckZDist);
        }
        else
        {
            targetX = goalCenterX;
        }
        return Clamp(targetX, goalCenterX - cfg.NetHalfWidth, goalCenterX + cfg.NetHalfWidth);
    }

    private static float Clamp(float v, float min, float max) =>
        v < min ? min : v > max ? max : v;
    private static float Clamp01(float v) => Clamp(v, 0, 1);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
