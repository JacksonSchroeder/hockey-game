using System.Numerics;
using HockeyGame.Logic.NetworkStates;
using Xunit;

namespace HockeyGame.Tests.Domain.NetworkStates;

public class InputStateTests
{
    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = new InputState
        {
            Sequence = 100,
            Delta = 1.0f / 60.0f,
            MoveVector = new Vector2(0.5f, -0.3f),
            MouseWorldPos = new Vector3(1.0f, 0.0f, 2.0f),
            ShootPressed = true,
            ShootHeld = false,
            SlapPressed = false,
            SlapHeld = true,
            FacingHeld = true,
            Brake = true,
            ElevationUp = false,
            ElevationDown = true,
        };

        var restored = InputState.FromArray(original.ToArray());

        Assert.Equal(original.Sequence, restored.Sequence);
        Assert.Equal(original.Delta, restored.Delta);
        Assert.Equal(original.MoveVector, restored.MoveVector);
        Assert.Equal(original.MouseWorldPos, restored.MouseWorldPos);
        Assert.Equal(original.ShootPressed, restored.ShootPressed);
        Assert.Equal(original.ShootHeld, restored.ShootHeld);
        Assert.Equal(original.SlapPressed, restored.SlapPressed);
        Assert.Equal(original.SlapHeld, restored.SlapHeld);
        Assert.Equal(original.FacingHeld, restored.FacingHeld);
        Assert.Equal(original.Brake, restored.Brake);
        Assert.Equal(original.ElevationUp, restored.ElevationUp);
        Assert.Equal(original.ElevationDown, restored.ElevationDown);
    }
}
