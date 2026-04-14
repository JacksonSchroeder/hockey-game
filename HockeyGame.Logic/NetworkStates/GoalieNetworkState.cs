namespace HockeyGame.Logic.NetworkStates;

public class GoalieNetworkState
{
    public float PositionX { get; set; }
    public float PositionZ { get; set; }
    public float RotationY { get; set; }
    public int StateEnum { get; set; }
    public float FiveHoleOpenness { get; set; }

    public object[] ToArray() => new object[]
    {
        PositionX,
        PositionZ,
        RotationY,
        StateEnum,
        FiveHoleOpenness,
    };

    public static GoalieNetworkState FromArray(object[] data) => new()
    {
        PositionX = (float)data[0],
        PositionZ = (float)data[1],
        RotationY = (float)data[2],
        StateEnum = (int)data[3],
        FiveHoleOpenness = (float)data[4],
    };
}
