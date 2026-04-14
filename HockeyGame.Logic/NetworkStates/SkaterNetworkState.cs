using System.Numerics;

namespace HockeyGame.Logic.NetworkStates;

// Serializable snapshot of a skater for network transmission.
// ToArray/FromArray round-trip preserves all fields.
public class SkaterNetworkState
{
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public Vector3 Velocity { get; set; }
    public Vector3 BladePosition { get; set; }
    public float UpperBodyRotationY { get; set; }
    public Vector2 Facing { get; set; }
    public int LastProcessedSequence { get; set; }
    public bool IsGhost { get; set; }

    public object[] ToArray() => new object[]
    {
        Position,
        Rotation,
        Velocity,
        BladePosition,
        UpperBodyRotationY,
        Facing,
        LastProcessedSequence,
        IsGhost,
    };

    public static SkaterNetworkState FromArray(object[] data) => new()
    {
        Position = (Vector3)data[0],
        Rotation = (Vector3)data[1],
        Velocity = (Vector3)data[2],
        BladePosition = (Vector3)data[3],
        UpperBodyRotationY = (float)data[4],
        Facing = (Vector2)data[5],
        LastProcessedSequence = (int)data[6],
        IsGhost = (bool)data[7],
    };
}
