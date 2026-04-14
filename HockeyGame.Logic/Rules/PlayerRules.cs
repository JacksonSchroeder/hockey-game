using System;
using System.Numerics;
using HockeyGame.Logic.Config;

namespace HockeyGame.Logic.Rules;

// Rules about players: team balance, color generation, faceoff positions.
// No engine dependencies. Color uses System.Drawing.Color (HSV → RGB math is pure).
public static class PlayerRules
{
    // Returns 0 or 1. Balances teams by count; ties → team 0.
    public static int AssignTeam(int team0Count, int team1Count) =>
        team0Count <= team1Count ? 0 : 1;

    // Team 0 = warm reds (hue 340-380°), Team 1 = cool blues (hue 200-260°).
    // Slot-based hue allocation ensures same-team players get visually distinct shades.
    // `jitter` is injected so callers can pass a deterministic value in tests;
    // at runtime the game passes a uniform random float in [-1, 1].
    public const int MaxPerTeam = 3;

    public static PlayerColor GeneratePlayerColor(int teamId, int existingCount, float jitter)
    {
        float hueMinDeg = teamId == 0 ? 340.0f : 200.0f;
        float hueMaxDeg = teamId == 0 ? 380.0f : 260.0f;
        float slotSize = (hueMaxDeg - hueMinDeg) / MaxPerTeam;
        float slotCenter = hueMinDeg + (existingCount + 0.5f) * slotSize;
        float jitterMag = slotSize * 0.25f;
        float hueDeg = slotCenter + jitter * jitterMag;
        float hue = (hueDeg % 360.0f) / 360.0f;
        if (hue < 0) hue += 1.0f;
        return HsvToRgb(hue, 0.8f, 0.9f);
    }

    // Faceoff positions by slot index.
    public static Vector3 FaceoffPositionForSlot(int slot) =>
        GameRules.CenterFaceoffPositions[slot];

    // HSV → RGB conversion (pure math). Godot's Color.from_hsv does the same.
    private static PlayerColor HsvToRgb(float h, float s, float v)
    {
        float c = v * s;
        float hp = h * 6.0f;
        float x = c * (1 - MathF.Abs(hp % 2 - 1));
        (float r, float g, float b) = hp switch
        {
            < 1 => (c, x, 0f),
            < 2 => (x, c, 0f),
            < 3 => (0f, c, x),
            < 4 => (0f, x, c),
            < 5 => (x, 0f, c),
            _   => (c, 0f, x),
        };
        float m = v - c;
        return new PlayerColor(r + m, g + m, b + m, 1.0f);
    }
}

// Small color record so the domain doesn't depend on Godot.Color or System.Drawing.
// Infrastructure converts at the boundary.
public readonly record struct PlayerColor(float R, float G, float B, float A);
