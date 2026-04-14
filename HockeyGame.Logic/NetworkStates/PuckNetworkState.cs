using System.Numerics;

namespace HockeyGame.Logic.NetworkStates;

public class PuckNetworkState
{
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public int CarrierPeerId { get; set; } = -1;

    public object[] ToArray() => new object[]
    {
        Position,
        Velocity,
        CarrierPeerId,
    };

    public static PuckNetworkState FromArray(object[] data) => new()
    {
        Position = (Vector3)data[0],
        Velocity = (Vector3)data[1],
        CarrierPeerId = (int)data[2],
    };
}
