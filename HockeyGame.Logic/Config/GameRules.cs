using System.Numerics;

namespace HockeyGame.Logic.Config;

// Pure game rules — no engine concerns here. Collision layers and physics tick rate
// live in EngineConstants under the Godot project.
public static class GameRules
{
    // Game flow timings
    public const float GoalPauseDuration = 2.0f;
    public const float FaceoffPrepDuration = 0.5f;
    public const float FaceoffTimeout = 10.0f;

    // Rink geometry
    public const float GoalLineZ = 26.6f;  // rink_length/2 - distance_from_end (30 - 3.4)
    public const float BlueLineZ = 7.62f;  // 25ft from center ice (NHL standard)

    // Puck
    public static readonly Vector3 PuckStartPos = new(0, 0.05f, 0);

    // Infractions
    public const float IcingGhostDuration = 3.0f;

    // Networking
    public const int MaxPlayers = 6;
    public const int InputRate = 60;
    public const int StateRate = 20;

    // Center ice faceoff positions, indexed by slot.
    // Even slots (0, 2, 4) are Team 0 — positive-Z side.
    // Odd slots (1, 3, 5) are Team 1 — negative-Z side.
    public static readonly Vector3[] CenterFaceoffPositions =
    {
        new( 0.0f, 1.0f,  1.5f),  // slot 0 — Team 0 center
        new( 0.0f, 1.0f, -1.5f),  // slot 1 — Team 1 center
        new(-5.0f, 1.0f,  3.0f),  // slot 2 — Team 0 left wing
        new(-5.0f, 1.0f, -3.0f),  // slot 3 — Team 1 left wing
        new( 5.0f, 1.0f,  3.0f),  // slot 4 — Team 0 right wing
        new( 5.0f, 1.0f, -3.0f),  // slot 5 — Team 1 right wing
    };
}
