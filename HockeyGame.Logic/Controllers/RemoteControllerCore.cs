using System.Collections.Generic;
using System.Numerics;
using HockeyGame.Logic.Interfaces;
using HockeyGame.Logic.NetworkStates;

namespace HockeyGame.Logic.Controllers;

// Remote-player controller: on host, drives the skater from received inputs.
// On clients, interpolates buffered state snapshots with a fixed delay.
public class RemoteControllerCore
{
    public record RemoteConfig(
        float InterpolationDelay,
        int StateBufferCap);

    private readonly SkaterControllerCore _core;
    private readonly ISkater _skater;
    private readonly IGameStateProvider _gameState;
    private readonly RemoteConfig _remoteCfg;
    private readonly bool _isHost;

    private InputState _latestInput = new();
    private int _lastProcessedSequence;
    private float _currentTime;
    private readonly List<BufferedSkaterState> _buffer = new();

    public RemoteControllerCore(
        SkaterControllerCore core,
        ISkater skater,
        IGameStateProvider gameState,
        bool isHost,
        RemoteConfig remoteCfg)
    {
        _core = core;
        _skater = skater;
        _gameState = gameState;
        _isHost = isHost;
        _remoteCfg = remoteCfg;
    }

    public int LastProcessedSequence => _lastProcessedSequence;

    public void ReceiveInput(InputState state) => _latestInput = state;

    // Host-side: drive the remote skater by replaying received inputs.
    // Client-side: advance time and render interpolated state.
    public void Tick(float delta)
    {
        if (_isHost) DriveFromInput(delta);
        else
        {
            _currentTime += delta;
            Interpolate();
            _skater.UpdateStickMesh();
        }
    }

    private void DriveFromInput(float delta)
    {
        // Always advance sequence tracking even during dead-puck phase, so
        // the client's reconcile filter stays current.
        _lastProcessedSequence = _latestInput.Sequence;
        if (_gameState.IsMovementLocked)
        {
            _skater.Velocity = Vector3.Zero;
            return;
        }
        _core.ProcessInput(_latestInput, delta);
    }

    public void ApplyNetworkState(SkaterNetworkState state)
    {
        if (_isHost) return;
        _buffer.Add(new BufferedSkaterState(_currentTime, state));
        if (_buffer.Count > _remoteCfg.StateBufferCap) _buffer.RemoveAt(0);
    }

    private void Interpolate()
    {
        float renderTime = _currentTime - _remoteCfg.InterpolationDelay;
        if (_buffer.Count < 2) return;

        BufferedSkaterState? from = null;
        BufferedSkaterState? to = null;
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
            ApplyStateToSkater(_buffer[^1].State);
            return;
        }

        float t = System.Math.Clamp(
            (renderTime - from.Timestamp) / (to.Timestamp - from.Timestamp), 0f, 1f);
        var interp = new SkaterNetworkState
        {
            Position = Vector3.Lerp(from.State.Position, to.State.Position, t),
            Rotation = Vector3.Lerp(from.State.Rotation, to.State.Rotation, t),
            Velocity = Vector3.Lerp(from.State.Velocity, to.State.Velocity, t),
            BladePosition = Vector3.Lerp(from.State.BladePosition, to.State.BladePosition, t),
            UpperBodyRotationY = Lerp(from.State.UpperBodyRotationY, to.State.UpperBodyRotationY, t),
            Facing = Vector2.Normalize(Vector2.Lerp(from.State.Facing, to.State.Facing, t)),
            IsGhost = to.State.IsGhost,
        };
        ApplyStateToSkater(interp);

        while (_buffer.Count > 2 && _buffer[1].Timestamp < renderTime)
            _buffer.RemoveAt(0);
    }

    private void ApplyStateToSkater(SkaterNetworkState state)
    {
        _skater.Position = state.Position;
        _skater.Rotation = state.Rotation;
        _skater.Velocity = state.Velocity;
        _skater.SetBladePosition(state.BladePosition);
        _skater.SetUpperBodyRotation(state.UpperBodyRotationY);
        _skater.SetFacing(state.Facing);
        _skater.SetGhost(state.IsGhost);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}

public record BufferedSkaterState(float Timestamp, SkaterNetworkState State);
