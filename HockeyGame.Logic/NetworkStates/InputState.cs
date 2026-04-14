using System.Numerics;

namespace HockeyGame.Logic.NetworkStates;

public class InputState
{
    public int Sequence { get; set; }
    public float Delta { get; set; } = 1.0f / 60.0f;
    public Vector2 MoveVector { get; set; }
    public Vector3 MouseWorldPos { get; set; }
    public bool ShootPressed { get; set; }
    public bool ShootHeld { get; set; }
    public bool SlapPressed { get; set; }
    public bool SlapHeld { get; set; }
    public bool FacingHeld { get; set; }
    public bool Brake { get; set; }
    public bool ElevationUp { get; set; }
    public bool ElevationDown { get; set; }

    public object[] ToArray() => new object[]
    {
        Sequence,
        Delta,
        MoveVector.X,
        MoveVector.Y,
        MouseWorldPos.X,
        MouseWorldPos.Y,
        MouseWorldPos.Z,
        ShootPressed,
        ShootHeld,
        SlapPressed,
        SlapHeld,
        FacingHeld,
        Brake,
        ElevationUp,
        ElevationDown,
    };

    public static InputState FromArray(object[] data) => new()
    {
        Sequence = (int)data[0],
        Delta = (float)data[1],
        MoveVector = new Vector2((float)data[2], (float)data[3]),
        MouseWorldPos = new Vector3((float)data[4], (float)data[5], (float)data[6]),
        ShootPressed = (bool)data[7],
        ShootHeld = (bool)data[8],
        SlapPressed = (bool)data[9],
        SlapHeld = (bool)data[10],
        FacingHeld = (bool)data[11],
        Brake = (bool)data[12],
        ElevationUp = (bool)data[13],
        ElevationDown = (bool)data[14],
    };
}
