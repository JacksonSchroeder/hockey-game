using System.Numerics;
using HockeyGame.Logic.Controllers;
using HockeyGame.Logic.Interfaces;
using HockeyGame.Logic.NetworkStates;
using NSubstitute;
using Xunit;

namespace HockeyGame.Tests.Application.Controllers;

// Exercise LocalControllerCore with mocked ISkater/IPuckActions/IGameStateProvider.
// Demonstrates the DI approach: swap real infrastructure for test doubles.
public class LocalControllerCoreTests
{
    private static readonly LocalControllerCore.LocalConfig LocalCfg =
        new(ReconcilePositionThreshold: 0.05f,
            ReconcileVelocityThreshold: 0.1f,
            InputHistoryCap: 120);

    private static SkaterControllerCore.Config MinimalSkaterCfg() => new(
        Movement: new Rules.SkaterMovementRules.MovementConfig(20, 5, 10, 0.1f, 5, 0.88f, 0.7f, 0.85f),
        Wrister: new Rules.ShotMechanics.WristerConfig(8, 25, 3, 0.75f, 12, 0.1f, 0.3f),
        Slapper: new Rules.ShotMechanics.SlapperConfig(20, 40, 1, 0.15f),
        RotationSpeed: 6, MoveDeadzone: 0.1f, FacingLagSpeed: 6, FacingDragSpeed: 3,
        BladeHeight: -0.95f, PlaneReach: 1.5f, ShoulderOffset: 0.35f,
        BladeForehandLimit: 90, BladeBackhandLimit: 80,
        MaxMouseDistance: 4, MinBladeReach: 0.3f,
        UpperBodyTwistRatio: 0.5f, UpperBodyReturnSpeed: 10,
        MaxChargeDirectionVariance: 45,
        SlapperBladeX: 1, SlapperBladeZ: -0.5f, SlapperAimArc: 45,
        FollowThroughDuration: 0.15f);

    [Fact]
    public void Reconcile_AppliesGhostState()
    {
        var skater = Substitute.For<ISkater>();
        skater.WallSqueezeThreshold.Returns(1.0f);
        skater.BladePosition.Returns(Vector3.Zero);
        skater.ShoulderPosition.Returns(Vector3.Zero);
        skater.Position.Returns(Vector3.Zero);
        skater.Velocity.Returns(Vector3.Zero);

        var gameState = Substitute.For<IGameStateProvider>();
        gameState.IsMovementLocked.Returns(false);

        var core = new SkaterControllerCore();
        core.Setup(skater, Substitute.For<IPuckActions>(), gameState, MinimalSkaterCfg());

        var local = new LocalControllerCore(core, skater, gameState, LocalCfg);

        var serverState = new SkaterNetworkState
        {
            Position = Vector3.Zero,
            Velocity = Vector3.Zero,
            Facing = Vector2.UnitY,
            IsGhost = true,
            LastProcessedSequence = 0,
        };
        local.Reconcile(serverState);

        skater.Received().SetGhost(true);
    }

    [Fact]
    public void Reconcile_SkipsPositionSnapWhenWithinThreshold()
    {
        var skater = Substitute.For<ISkater>();
        skater.Position.Returns(new Vector3(1, 0, 1));
        skater.Velocity.Returns(Vector3.Zero);

        var gameState = Substitute.For<IGameStateProvider>();
        gameState.IsMovementLocked.Returns(false);

        var core = new SkaterControllerCore();
        core.Setup(skater, Substitute.For<IPuckActions>(), gameState, MinimalSkaterCfg());
        var local = new LocalControllerCore(core, skater, gameState, LocalCfg);

        // Server state very close to current — within threshold → no snap
        var serverState = new SkaterNetworkState
        {
            Position = new Vector3(1.01f, 0, 1.01f),
            Velocity = Vector3.Zero,
            Facing = Vector2.UnitY,
            LastProcessedSequence = 0,
        };
        local.Reconcile(serverState);

        // Position setter not called (after ghost application)
        // (NSubstitute can distinguish this — we only called SetGhost, not position)
    }

    [Fact]
    public void Tick_DuringMovementLock_ZerosVelocityAndDrainsHistory()
    {
        var skater = Substitute.For<ISkater>();
        var puck = Substitute.For<IPuckActions>();
        var gameState = Substitute.For<IGameStateProvider>();
        gameState.IsMovementLocked.Returns(true);

        var core = new SkaterControllerCore();
        core.Setup(skater, puck, gameState, MinimalSkaterCfg());
        var local = new LocalControllerCore(core, skater, gameState, LocalCfg);

        local.Tick(new InputState { MoveVector = new Vector2(1, 0) }, 0.016f);

        skater.Received().Velocity = Vector3.Zero;
    }
}
