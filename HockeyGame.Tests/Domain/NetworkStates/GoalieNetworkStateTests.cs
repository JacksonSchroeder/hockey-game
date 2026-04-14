using HockeyGame.Logic.NetworkStates;
using Xunit;

namespace HockeyGame.Tests.Domain.NetworkStates;

public class GoalieNetworkStateTests
{
    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = new GoalieNetworkState
        {
            PositionX = 2.5f,
            PositionZ = 26.0f,
            RotationY = 3.14f,
            StateEnum = 1,
            FiveHoleOpenness = 0.06f,
        };

        var restored = GoalieNetworkState.FromArray(original.ToArray());

        Assert.Equal(original.PositionX, restored.PositionX);
        Assert.Equal(original.PositionZ, restored.PositionZ);
        Assert.Equal(original.RotationY, restored.RotationY);
        Assert.Equal(original.StateEnum, restored.StateEnum);
        Assert.Equal(original.FiveHoleOpenness, restored.FiveHoleOpenness);
    }
}
