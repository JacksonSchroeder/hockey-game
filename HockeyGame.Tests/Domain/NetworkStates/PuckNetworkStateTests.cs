using System.Numerics;
using HockeyGame.Logic.NetworkStates;
using Xunit;

namespace HockeyGame.Tests.Domain.NetworkStates;

public class PuckNetworkStateTests
{
    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = new PuckNetworkState
        {
            Position = new Vector3(5.0f, 0.05f, -10.0f),
            Velocity = new Vector3(20.0f, 0.0f, -15.0f),
            CarrierPeerId = 12345,
        };

        var restored = PuckNetworkState.FromArray(original.ToArray());

        Assert.Equal(original.Position, restored.Position);
        Assert.Equal(original.Velocity, restored.Velocity);
        Assert.Equal(original.CarrierPeerId, restored.CarrierPeerId);
    }

    [Fact]
    public void DefaultCarrier_IsMinusOne()
    {
        var state = new PuckNetworkState();
        Assert.Equal(-1, state.CarrierPeerId);
    }
}
