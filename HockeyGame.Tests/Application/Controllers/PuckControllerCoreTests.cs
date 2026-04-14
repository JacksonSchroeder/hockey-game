using System.Numerics;
using HockeyGame.Logic.Controllers;
using HockeyGame.Logic.Interfaces;
using HockeyGame.Logic.NetworkStates;
using NSubstitute;
using Xunit;

namespace HockeyGame.Tests.Application.Controllers;

public class PuckControllerCoreTests
{
    private static readonly PuckControllerCore.Config Cfg = new(
        InterpolationDelay: 0.1f,
        PredictionReconcileThreshold: 3.0f,
        PositionCorrectionBlend: 0.3f,
        VelocityCorrectionBlend: 0.5f,
        StateBufferCap: 10);

    [Fact]
    public void Server_DoesNotBufferState()
    {
        var puck = Substitute.For<IPuckActions>();
        var bridge = new PuckControllerCore.Bridge();
        var controller = new PuckControllerCore(
            puck, isServer: true, localPeerId: 1, Cfg, bridge);

        // Applying state on server should be a no-op
        var state = new PuckNetworkState
        {
            Position = new Vector3(5, 0, 5),
            Velocity = new Vector3(1, 0, 0),
        };
        controller.ApplyState(state);

        // Server's puck physics drives itself; server doesn't reconcile from incoming state.
        puck.DidNotReceive().SetPosition(Arg.Any<Vector3>());
    }

    [Fact]
    public void Client_LargeDivergence_SnapsToServerState()
    {
        var puck = Substitute.For<IPuckActions>();
        puck.Position.Returns(Vector3.Zero);
        puck.Velocity.Returns(Vector3.Zero);

        var bridge = new PuckControllerCore.Bridge();
        var controller = new PuckControllerCore(
            puck, isServer: false, localPeerId: 2, Cfg, bridge);

        // Simulate trajectory prediction in progress
        controller.NotifyLocalRelease(new Vector3(1, 0, 0), 10.0f);

        // Server says puck is way over there — should snap
        var serverState = new PuckNetworkState
        {
            Position = new Vector3(100, 0, 100),
            Velocity = Vector3.Zero,
        };
        controller.ApplyState(serverState);

        puck.Received().SetPosition(new Vector3(100, 0, 100));
    }

    [Fact]
    public void Client_PickupFromServer_EndsTrajectoryPrediction()
    {
        var puck = Substitute.For<IPuckActions>();
        puck.Position.Returns(Vector3.Zero);

        var controller = new PuckControllerCore(
            puck, isServer: false, localPeerId: 2, Cfg, new PuckControllerCore.Bridge());

        controller.NotifyLocalRelease(new Vector3(1, 0, 0), 10.0f);

        // Server reports puck is now carried by someone — end prediction
        var carriedState = new PuckNetworkState
        {
            Position = Vector3.Zero,
            Velocity = Vector3.Zero,
            CarrierPeerId = 99,
        };
        controller.ApplyState(carriedState);

        puck.Received().SetClientPredictionMode(false);
    }

    [Fact]
    public void ServerPickup_CallsBridgeAndStoresCarrier()
    {
        var puck = Substitute.For<IPuckActions>();
        var carrier = Substitute.For<ISkater>();
        int? capturedSkaterArg = null;

        var bridge = new PuckControllerCore.Bridge
        {
            OnServerPickup = s =>
            {
                capturedSkaterArg = s == carrier ? 42 : -1;
                return 42;
            },
        };
        var controller = new PuckControllerCore(
            puck, isServer: true, localPeerId: 1, Cfg, bridge);

        controller.OnServerPuckPickedUp(carrier);

        Assert.Equal(42, controller.CarrierPeerId);
        Assert.Equal(42, capturedSkaterArg);
    }

    [Fact]
    public void ServerRelease_NotifiesBridgeAndClearsCarrier()
    {
        var puck = Substitute.For<IPuckActions>();
        int? released = null;

        var bridge = new PuckControllerCore.Bridge
        {
            OnServerPickup = _ => 42,
            OnServerReleased = id => released = id,
        };
        var controller = new PuckControllerCore(
            puck, isServer: true, localPeerId: 1, Cfg, bridge);

        controller.OnServerPuckPickedUp(Substitute.For<ISkater>());
        controller.OnServerPuckReleased();

        Assert.Equal(42, released);
        Assert.Equal(-1, controller.CarrierPeerId);
    }
}
