using System.Collections.Generic;
using System.Numerics;
using HockeyGame.Logic.Interfaces;
using HockeyGame.Logic.NetworkStates;
using HockeyGame.Logic.Rules;

namespace HockeyGame.Logic.Controllers;

// Local-player controller: runs prediction + reconciliation on the client.
// Extracts the logic from LocalController.gd. The Godot wrapper feeds inputs
// from the InputGatherer and calls Tick() each physics frame.
public class LocalControllerCore
{
    public record LocalConfig(
        float ReconcilePositionThreshold,
        float ReconcileVelocityThreshold,
        int InputHistoryCap);  // e.g. 120 = 2 seconds at 60Hz

    private readonly SkaterControllerCore _core;
    private readonly LocalConfig _localCfg;
    private readonly IGameStateProvider _gameState;
    private readonly ISkater _skater;

    private InputState _currentInput = new();
    private readonly List<InputState> _inputHistory = new();

    public LocalControllerCore(
        SkaterControllerCore core,
        ISkater skater,
        IGameStateProvider gameState,
        LocalConfig localCfg)
    {
        _core = core;
        _skater = skater;
        _gameState = gameState;
        _localCfg = localCfg;
    }

    public InputState CurrentInput => _currentInput;

    public void TeleportTo(Vector3 pos)
    {
        _core.TeleportTo(pos);
        _inputHistory.Clear();
    }

    // Called from the Godot wrapper each physics frame. `gatheredInput` is
    // built by the LocalInputGatherer; this records it in history and drives
    // the core state machine.
    public void Tick(InputState gatheredInput, float delta)
    {
        if (_gameState.IsMovementLocked)
        {
            // Dead-puck phase: kill velocity and drain history to prevent stale
            // replay when phase lifts.
            _skater.Velocity = Vector3.Zero;
            _inputHistory.Clear();
            return;
        }

        _currentInput = gatheredInput;
        _inputHistory.Add(gatheredInput);
        if (_inputHistory.Count > _localCfg.InputHistoryCap)
            _inputHistory.RemoveAt(0);
        _core.ProcessInput(gatheredInput, delta);
    }

    // Offside prediction on the client. Only predict offside → ghost; icing
    // ghost arrives authoritatively via reconcile().
    public void PredictOffside(int teamId, Vector3 puckPos, bool isCarrier)
    {
        bool offside = InfractionRules.IsOffside(
            _skater.Position.Z, teamId, puckPos.Z, isCarrier);
        if (offside && !_skater.IsGhost) _skater.SetGhost(true);
        // If server says ghost (could be icing), we don't clear here — reconcile
        // corrects within one broadcast cycle.
    }

    // Apply server-authoritative state and replay unprocessed inputs.
    public void Reconcile(SkaterNetworkState serverState)
    {
        // Always apply authoritative ghost state (covers both offside + icing).
        _skater.SetGhost(serverState.IsGhost);

        if (_gameState.IsMovementLocked) return;

        // Drop acknowledged inputs
        _inputHistory.RemoveAll(i => i.Sequence <= serverState.LastProcessedSequence);

        float posError = Vector3.Distance(_skater.Position, serverState.Position);
        float velError = Vector3.Distance(_skater.Velocity, serverState.Velocity);
        if (posError < _localCfg.ReconcilePositionThreshold
            && velError < _localCfg.ReconcileVelocityThreshold) return;

        // Snap to server state and replay
        _skater.Position = serverState.Position;
        _skater.Velocity = serverState.Velocity;
        _skater.SetFacing(serverState.Facing);
        _skater.SetUpperBodyRotation(serverState.UpperBodyRotationY);
        foreach (var input in _inputHistory)
            _core.ProcessInput(input, input.Delta);
    }
}
