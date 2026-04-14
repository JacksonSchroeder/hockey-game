using System.Numerics;
using HockeyGame.Logic.NetworkStates;
using Xunit;

namespace HockeyGame.Tests.Domain.NetworkStates;

public class SkaterNetworkStateTests
{
    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = new SkaterNetworkState
        {
            Position = new Vector3(1.0f, 2.0f, 3.0f),
            Rotation = new Vector3(0.1f, 0.2f, 0.3f),
            Velocity = new Vector3(4.0f, 0.0f, 5.0f),
            BladePosition = new Vector3(0.5f, -0.95f, -1.0f),
            UpperBodyRotationY = 0.15f,
            Facing = new Vector2(0.0f, -1.0f),
            LastProcessedSequence = 42,
            IsGhost = true,
        };

        var restored = SkaterNetworkState.FromArray(original.ToArray());

        Assert.Equal(original.Position, restored.Position);
        Assert.Equal(original.Rotation, restored.Rotation);
        Assert.Equal(original.Velocity, restored.Velocity);
        Assert.Equal(original.BladePosition, restored.BladePosition);
        Assert.Equal(original.UpperBodyRotationY, restored.UpperBodyRotationY);
        Assert.Equal(original.Facing, restored.Facing);
        Assert.Equal(original.LastProcessedSequence, restored.LastProcessedSequence);
        Assert.Equal(original.IsGhost, restored.IsGhost);
    }

    [Fact]
    public void Defaults_AreZero()
    {
        var state = new SkaterNetworkState();
        Assert.Equal(Vector3.Zero, state.Position);
        Assert.Equal(Vector3.Zero, state.Velocity);
        Assert.Equal(0, state.LastProcessedSequence);
        Assert.False(state.IsGhost);
    }
}
