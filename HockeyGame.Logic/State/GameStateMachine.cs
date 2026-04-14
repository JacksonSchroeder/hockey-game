using System.Collections.Generic;
using System.Numerics;
using HockeyGame.Logic.Config;
using HockeyGame.Logic.Rules;

namespace HockeyGame.Logic.State;

// Pure-C# orchestrator for game state. Owns the phase FSM, scores, player slot
// registry, and infraction (icing) state. Methods mutate internal state and
// return simple booleans/values that GameManager uses to drive infrastructure.
//
// GameManager never reads `_phase` directly — it checks CurrentPhase, then on
// transitions executes the corresponding infrastructure side effects (reset puck,
// teleport players, send RPCs). All decisions live here; all engine calls live there.
public class GameStateMachine
{
    // ── Config (injected) ─────────────────────────────────────────────────────
    private readonly float _goalPauseDuration;
    private readonly float _faceoffPrepDuration;
    private readonly float _faceoffTimeout;
    private readonly float _icingGhostDuration;
    private readonly Vector3[] _centerFaceoffPositions;

    // ── State ─────────────────────────────────────────────────────────────────
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Playing;
    private float _phaseTimer = 0.0f;

    public int[] Scores { get; } = { 0, 0 };
    public int LastScoringTeamId { get; private set; } = -1;

    // peer_id → PlayerSlot (pure domain view; infra-layer PlayerRecord pairs this
    // with Skater/Controller refs)
    public Dictionary<int, PlayerSlot> Players { get; } = new();
    public int NextSlot { get; private set; } = 1;  // host is always slot 0

    // Icing state
    public int LastCarrierTeamId { get; private set; } = -1;
    private float _lastCarrierZ = 0.0f;
    public int IcingTeamId { get; private set; } = -1;
    private float _icingTimer = 0.0f;

    public GameStateMachine(
        float goalPauseDuration,
        float faceoffPrepDuration,
        float faceoffTimeout,
        float icingGhostDuration,
        Vector3[] centerFaceoffPositions)
    {
        _goalPauseDuration = goalPauseDuration;
        _faceoffPrepDuration = faceoffPrepDuration;
        _faceoffTimeout = faceoffTimeout;
        _icingGhostDuration = icingGhostDuration;
        _centerFaceoffPositions = centerFaceoffPositions;
    }

    public static GameStateMachine CreateDefault() => new(
        GameRules.GoalPauseDuration,
        GameRules.FaceoffPrepDuration,
        GameRules.FaceoffTimeout,
        GameRules.IcingGhostDuration,
        GameRules.CenterFaceoffPositions);

    // ── Frame tick (host) ────────────────────────────────────────────────────
    // Returns true if the phase changed, so GameManager knows to execute entry
    // side effects for the new phase.
    public bool Tick(float delta)
    {
        TickIcing(delta);
        return TickPhase(delta);
    }

    private bool TickPhase(float delta)
    {
        if (CurrentPhase == GamePhase.Playing) return false;
        _phaseTimer += delta;
        switch (CurrentPhase)
        {
            case GamePhase.GoalScored when _phaseTimer >= _goalPauseDuration:
                SetPhase(GamePhase.FaceoffPrep);
                return true;
            case GamePhase.FaceoffPrep when _phaseTimer >= _faceoffPrepDuration:
                SetPhase(GamePhase.Faceoff);
                return true;
            case GamePhase.Faceoff when _phaseTimer >= _faceoffTimeout:
                SetPhase(GamePhase.Playing);
                return true;
            default:
                return false;
        }
    }

    private void TickIcing(float delta)
    {
        if (IcingTeamId == -1) return;
        _icingTimer -= delta;
        if (_icingTimer <= 0.0f) IcingTeamId = -1;
    }

    // ── Events from infrastructure ───────────────────────────────────────────

    // Returns the scoring team id, or -1 if the goal didn't count (wrong phase).
    public int OnGoalScored(int defendingTeamId)
    {
        if (CurrentPhase != GamePhase.Playing) return -1;
        int scoringTeamId = 1 - defendingTeamId;
        Scores[scoringTeamId] += 1;
        LastScoringTeamId = scoringTeamId;
        SetPhase(GamePhase.GoalScored);
        return scoringTeamId;
    }

    // Called when a skater picks up the puck during FACEOFF. Returns true on transition.
    public bool OnFaceoffPuckPickedUp()
    {
        if (CurrentPhase != GamePhase.Faceoff) return false;
        SetPhase(GamePhase.Playing);
        return true;
    }

    // Called each frame (host) while the puck has a carrier. Tracks last-carrier
    // info for icing detection and clears any active icing (opponent pickup clears
    // icing instantly; same-team pickup just refreshes the tracker).
    public void NotifyPuckCarried(int carrierTeamId, float carrierZ)
    {
        LastCarrierTeamId = carrierTeamId;
        _lastCarrierZ = carrierZ;
        if (IcingTeamId != -1)
        {
            IcingTeamId = -1;
            _icingTimer = 0.0f;
        }
    }

    // Called each tick while the puck is loose. Triggers icing if the puck has
    // crossed the opponent's goal line after being shot from own half.
    public void CheckIcingForLoosePuck(float puckZ)
    {
        if (CurrentPhase != GamePhase.Playing) return;
        if (IcingTeamId != -1) return;
        int offender = InfractionRules.CheckIcing(LastCarrierTeamId, _lastCarrierZ, puckZ);
        if (offender != -1)
        {
            IcingTeamId = offender;
            _icingTimer = _icingGhostDuration;
            LastCarrierTeamId = -1;
        }
    }

    // Compute ghost state for all players based on their positions and the puck.
    // Returns a dict of peer_id → should_ghost.
    public Dictionary<int, bool> ComputeGhostState(
        Dictionary<int, Vector3> playerPositions,
        int puckCarrierPeerId,
        Vector3 puckPosition)
    {
        var result = new Dictionary<int, bool>();
        bool isActivePlay = CurrentPhase == GamePhase.Playing || CurrentPhase == GamePhase.Faceoff;
        foreach (var (peerId, pos) in playerPositions)
        {
            if (!Players.TryGetValue(peerId, out var slot))
            {
                result[peerId] = false;
                continue;
            }
            bool ghost = false;
            if (isActivePlay)
            {
                bool isCarrier = peerId == puckCarrierPeerId;
                if (InfractionRules.IsOffside(pos.Z, slot.TeamId, puckPosition.Z, isCarrier))
                    ghost = true;
                else if (IcingTeamId == slot.TeamId)
                    ghost = true;
            }
            result[peerId] = ghost;
        }
        return result;
    }

    // ── Player registry ──────────────────────────────────────────────────────

    public record ConnectionResult(int Slot, int TeamId);

    public ConnectionResult OnPlayerConnected(int peerId)
    {
        int slot = NextSlot;
        NextSlot += 1;
        int team0Count = CountPlayersOnTeam(0);
        int team1Count = CountPlayersOnTeam(1);
        int teamId = PlayerRules.AssignTeam(team0Count, team1Count);
        var playerSlot = new PlayerSlot
        {
            PeerId = peerId,
            Slot = slot,
            TeamId = teamId,
            FaceoffPosition = _centerFaceoffPositions[slot],
        };
        Players[peerId] = playerSlot;
        return new ConnectionResult(slot, teamId);
    }

    // For the host's own slot-0 assignment at startup.
    public ConnectionResult RegisterHost(int peerId)
    {
        var playerSlot = new PlayerSlot
        {
            PeerId = peerId,
            Slot = 0,
            TeamId = PlayerRules.AssignTeam(CountPlayersOnTeam(0), CountPlayersOnTeam(1)),
            FaceoffPosition = _centerFaceoffPositions[0],
        };
        Players[peerId] = playerSlot;
        return new ConnectionResult(0, playerSlot.TeamId);
    }

    public void OnPlayerDisconnected(int peerId) => Players.Remove(peerId);

    public int CountPlayersOnTeam(int teamId)
    {
        int count = 0;
        foreach (var p in Players.Values)
            if (p.TeamId == teamId) count++;
        return count;
    }

    // ── Reset ────────────────────────────────────────────────────────────────

    public void ResetScores()
    {
        Scores[0] = 0;
        Scores[1] = 0;
    }

    // Starts a faceoff prep phase (used by manual reset and after goals).
    public void BeginFaceoffPrep()
    {
        IcingTeamId = -1;
        _icingTimer = 0.0f;
        LastCarrierTeamId = -1;
        SetPhase(GamePhase.FaceoffPrep);
    }

    // ── Remote state application (clients) ──────────────────────────────────
    // Clients receive authoritative phase/scores via world state broadcasts.

    public void ApplyRemoteState(int score0, int score1, GamePhase phase)
    {
        Scores[0] = score0;
        Scores[1] = score1;
        if (phase != CurrentPhase)
        {
            CurrentPhase = phase;
            _phaseTimer = 0.0f;
        }
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public IReadOnlyDictionary<int, Vector3> GetFaceoffPositions()
    {
        var dict = new Dictionary<int, Vector3>();
        foreach (var (peerId, slot) in Players)
            dict[peerId] = _centerFaceoffPositions[slot.Slot];
        return dict;
    }

    public bool IsMovementLocked => PhaseRules.IsMovementLocked(CurrentPhase);

    // ── Internal ─────────────────────────────────────────────────────────────
    private void SetPhase(GamePhase phase)
    {
        CurrentPhase = phase;
        _phaseTimer = 0.0f;
    }
}
