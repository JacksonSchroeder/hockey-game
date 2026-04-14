using System.Collections.Generic;
using System.Numerics;
using HockeyGame.Logic.Interfaces;
using HockeyGame.Logic.NetworkStates;

namespace HockeyGame.Logic.Controllers;

// Pure-C# puck controller: handles interpolation (clients) and reconciliation
// (client trajectory prediction). Takes callbacks via PuckBridge instead of
// reaching into GameManager.
//
// The Godot-side PuckController thin-wraps this and handles signal connections
// (puck.puck_picked_up, puck.puck_released, puck.puck_stripped on the server).
public class PuckControllerCore
{
    public record Config(
        float InterpolationDelay,
        float PredictionReconcileThreshold,
        float PositionCorrectionBlend,
        float VelocityCorrectionBlend,
        int StateBufferCap);

    // Callbacks the host-side GameManager provides. Lets PuckController stay
    // out of the player registry and network service.
    public class Bridge
    {
        // On server pickup: find peer id for a carrier, notify the peer's
        // controller, send RPC if remote. Returns the carrier's peer id (or -1).
        public System.Func<ISkater, int>? OnServerPickup { get; init; }
        // On server release: notify the ex-carrier's controller.
        public System.Action<int>? OnServerReleased { get; init; }
        // On server strip: find ex-carrier, notify network.
        public System.Action<ISkater>? OnServerStripped { get; init; }
        // Client-only: local player's current blade world position (for carry prediction).
        public System.Func<Vector3?>? GetLocalBladeWorldPos { get; init; }
    }

    private readonly IPuckActions _puck;
    private readonly bool _isServer;
    private readonly int _localPeerId;
    private readonly Config _cfg;
    private readonly Bridge _bridge;

    private int _carrierPeerId = -1;
    private float _currentTime;
    private readonly List<BufferedPuckState> _buffer = new();
    private bool _predictingTrajectory;

    public PuckControllerCore(
        IPuckActions puck,
        bool isServer,
        int localPeerId,
        Config cfg,
        Bridge bridge)
    {
        _puck = puck;
        _isServer = isServer;
        _localPeerId = localPeerId;
        _cfg = cfg;
        _bridge = bridge;
        _puck.SetServerMode(_isServer);
    }

    public int CarrierPeerId => _carrierPeerId;

    // ── Client-side puck behavior ────────────────────────────────────────────
    public void PhysicsTick(float delta)
    {
        if (_isServer) return;
        _currentTime += delta;

        if (_carrierPeerId == _localPeerId) ApplyLocalCarrierPosition();
        else if (!_predictingTrajectory) Interpolate();
    }

    public void NotifyLocalPickup()
    {
        _carrierPeerId = _localPeerId;
        _predictingTrajectory = false;
        _puck.SetClientPredictionMode(false);
    }

    public void NotifyLocalRelease(Vector3 direction, float power)
    {
        _carrierPeerId = -1;
        _predictingTrajectory = true;
        _puck.SetClientPredictionMode(true);
        _puck.SetVelocity(direction * power);
        _buffer.Clear();
    }

    // Called when the server force-ends a carry (e.g. goal scored).
    // Doesn't start trajectory prediction — just drops to interpolation.
    public void NotifyLocalPuckDropped()
    {
        _carrierPeerId = -1;
        _predictingTrajectory = false;
        _puck.SetClientPredictionMode(false);
        _buffer.Clear();
    }

    private void ApplyLocalCarrierPosition()
    {
        var bladePos = _bridge.GetLocalBladeWorldPos?.Invoke();
        if (bladePos == null) return;
        _puck.SetPosition(bladePos.Value);
    }

    // ── Server puck signals (routed through Bridge) ──────────────────────────
    public void OnServerPuckPickedUp(ISkater carrier)
    {
        _carrierPeerId = _bridge.OnServerPickup?.Invoke(carrier) ?? -1;
    }

    public void OnServerPuckReleased()
    {
        _bridge.OnServerReleased?.Invoke(_carrierPeerId);
        _carrierPeerId = -1;
    }

    public void OnServerPuckStripped(ISkater exCarrier)
    {
        _bridge.OnServerStripped?.Invoke(exCarrier);
    }

    // ── State sync ───────────────────────────────────────────────────────────
    public PuckNetworkState GetState() => new()
    {
        Position = _puck.Position,
        Velocity = _puck.Velocity,
        CarrierPeerId = _carrierPeerId,
    };

    public void ApplyState(PuckNetworkState state)
    {
        if (_isServer) return;

        if (_predictingTrajectory)
        {
            if (state.CarrierPeerId != -1)
            {
                // Someone picked it up — hand back to buffered interpolation
                _predictingTrajectory = false;
                _puck.SetClientPredictionMode(false);
            }
            else
            {
                Reconcile(state);
            }
        }

        _buffer.Add(new BufferedPuckState(_currentTime, state));
        if (_buffer.Count > _cfg.StateBufferCap) _buffer.RemoveAt(0);
    }

    private void Reconcile(PuckNetworkState state)
    {
        Vector3 posError = state.Position - _puck.Position;
        if (posError.Length() > _cfg.PredictionReconcileThreshold)
        {
            _puck.SetPosition(state.Position);
            _puck.SetVelocity(state.Velocity);
            _buffer.Clear();
            return;
        }
        Vector3 currentVel = _puck.Velocity;
        _puck.SetVelocity(Vector3.Lerp(currentVel, state.Velocity, _cfg.VelocityCorrectionBlend));
        // Only nudge position when velocities agree — avoids fighting physics
        // during bounces where velocities are briefly opposing.
        if (Vector3.Dot(currentVel, state.Velocity) > 0.0f)
            _puck.SetPosition(_puck.Position + posError * _cfg.PositionCorrectionBlend);
    }

    private void Interpolate()
    {
        float renderTime = _currentTime - _cfg.InterpolationDelay;
        if (_buffer.Count < 2) return;

        BufferedPuckState? from = null;
        BufferedPuckState? to = null;
        for (int i = 0; i < _buffer.Count - 1; i++)
        {
            var a = _buffer[i];
            var b = _buffer[i + 1];
            if (a.Timestamp <= renderTime && renderTime <= b.Timestamp)
            {
                from = a;
                to = b;
                break;
            }
        }
        if (from == null || to == null)
        {
            _puck.SetPosition(_buffer[^1].State.Position);
            return;
        }
        float t = System.Math.Clamp(
            (renderTime - from.Timestamp) / (to.Timestamp - from.Timestamp), 0f, 1f);
        _puck.SetPosition(Vector3.Lerp(from.State.Position, to.State.Position, t));

        while (_buffer.Count > 2 && _buffer[1].Timestamp < renderTime)
            _buffer.RemoveAt(0);
    }
}

public record BufferedPuckState(float Timestamp, PuckNetworkState State);
