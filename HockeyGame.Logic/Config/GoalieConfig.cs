namespace HockeyGame.Logic.Config;

// Tunable goalie behavior parameters. Pure data — consumed by GoalieBehaviorRules
// and GoalieController. Defaults mirror the current GDScript @export values.
public class GoalieConfig
{
    public bool CatchesLeft { get; init; } = true;

    // Depth zones (how far from goal line the goalie plays based on puck distance)
    public float DepthAggressive { get; init; } = 1.2f;
    public float DepthBase { get; init; } = 0.6f;
    public float DepthConservative { get; init; } = 0.3f;
    public float DepthDefensive { get; init; } = 0.1f;
    public float ZonePostZ { get; init; } = 2.0f;
    public float ZoneAggressiveZ { get; init; } = 8.0f;
    public float ZoneBaseZ { get; init; } = 12.0f;
    public float ZoneConservativeZ { get; init; } = 20.0f;
    public float DepthSpeed { get; init; } = 2.0f;

    // Lateral movement
    public float ShuffleSpeed { get; init; } = 1.3f;
    public float TPushSpeed { get; init; } = 3.0f;
    public float LateralThreshold { get; init; } = 0.3f;
    public float MaxFacingAngle { get; init; } = 70.0f;
    public float RotationSpeed { get; init; } = 8.0f;
    public float RvhTransitionSpeed { get; init; } = 6.0f;

    // Reaction timing
    public float ReactionDelay { get; init; } = 0.10f;
    public float ButterflyRecoveryTime { get; init; } = 0.4f;

    // Shot detection
    public float ShotSpeedThreshold { get; init; } = 5.0f;
    public float NetHalfWidth { get; init; } = 0.915f;
    public float NetMargin { get; init; } = 1.0f;

    // RVH (reverse vertical horizontal — post-hug)
    public float RvhDepth { get; init; } = 0.1f;
    public float RvhEarlyAngle { get; init; } = 60.0f;
    public float RvhPostPadAngle { get; init; } = 15.0f;

    // Five-hole openness (how far legs spread during movement)
    public float FiveHoleBase { get; init; } = 0.02f;
    public float FiveHoleShuffleMax { get; init; } = 0.06f;
    public float FiveHoleTPushMax { get; init; } = 0.15f;

    // Tracking/smoothing
    public float TrackingSpeed { get; init; } = 6.0f;
    public float PartLerpSpeed { get; init; } = 6.0f;
    public float InterpolationDelay { get; init; } = 0.1f;

    public static GoalieConfig Default => new();
}
