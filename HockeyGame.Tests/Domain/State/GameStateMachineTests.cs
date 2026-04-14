using System.Numerics;
using HockeyGame.Logic.Config;
using HockeyGame.Logic.State;
using Xunit;

namespace HockeyGame.Tests.Domain.State;

public class GameStateMachineTests
{
    private static GameStateMachine NewSm() => GameStateMachine.CreateDefault();

    // ── Phase transitions ────────────────────────────────────────────────────

    [Fact]
    public void InitialPhase_IsPlaying()
    {
        var sm = NewSm();
        Assert.Equal(GamePhase.Playing, sm.CurrentPhase);
    }

    [Fact]
    public void Goal_TransitionsToGoalScored_AndIncrementsScore()
    {
        var sm = NewSm();
        int scorer = sm.OnGoalScored(defendingTeamId: 1);
        Assert.Equal(0, scorer);
        Assert.Equal(GamePhase.GoalScored, sm.CurrentPhase);
        Assert.Equal(1, sm.Scores[0]);
        Assert.Equal(0, sm.Scores[1]);
    }

    [Fact]
    public void Goal_IgnoredDuringNonPlayingPhase()
    {
        var sm = NewSm();
        sm.OnGoalScored(defendingTeamId: 1);
        int secondScorer = sm.OnGoalScored(defendingTeamId: 1); // already in GoalScored
        Assert.Equal(-1, secondScorer);
        Assert.Equal(1, sm.Scores[0]);
    }

    [Fact]
    public void GoalPause_ExpiresToFaceoffPrep()
    {
        var sm = NewSm();
        sm.OnGoalScored(1);
        bool changed = sm.Tick(GameRules.GoalPauseDuration + 0.01f);
        Assert.True(changed);
        Assert.Equal(GamePhase.FaceoffPrep, sm.CurrentPhase);
    }

    [Fact]
    public void PartialTick_DoesNotTransition()
    {
        var sm = NewSm();
        sm.OnGoalScored(1);
        bool changed = sm.Tick(GameRules.GoalPauseDuration / 2);
        Assert.False(changed);
        Assert.Equal(GamePhase.GoalScored, sm.CurrentPhase);
    }

    [Fact]
    public void FaceoffPrep_ExpiresToFaceoff()
    {
        var sm = NewSm();
        sm.BeginFaceoffPrep();
        sm.Tick(GameRules.FaceoffPrepDuration + 0.01f);
        Assert.Equal(GamePhase.Faceoff, sm.CurrentPhase);
    }

    [Fact]
    public void PuckPickup_DuringFaceoff_ResumesPlaying()
    {
        var sm = NewSm();
        sm.BeginFaceoffPrep();
        sm.Tick(GameRules.FaceoffPrepDuration + 0.01f); // → Faceoff
        bool changed = sm.OnFaceoffPuckPickedUp();
        Assert.True(changed);
        Assert.Equal(GamePhase.Playing, sm.CurrentPhase);
    }

    [Fact]
    public void PuckPickup_OutsideFaceoff_Noop()
    {
        var sm = NewSm();
        bool changed = sm.OnFaceoffPuckPickedUp(); // during Playing
        Assert.False(changed);
    }

    [Fact]
    public void FaceoffTimeout_ResumesPlaying()
    {
        var sm = NewSm();
        sm.BeginFaceoffPrep();
        sm.Tick(GameRules.FaceoffPrepDuration + 0.01f);
        sm.Tick(GameRules.FaceoffTimeout + 0.01f);
        Assert.Equal(GamePhase.Playing, sm.CurrentPhase);
    }

    [Fact]
    public void FullCycle_PlayingToPlaying()
    {
        var sm = NewSm();
        sm.OnGoalScored(1);
        sm.Tick(GameRules.GoalPauseDuration + 0.01f);      // → FaceoffPrep
        sm.Tick(GameRules.FaceoffPrepDuration + 0.01f);    // → Faceoff
        sm.OnFaceoffPuckPickedUp();                        // → Playing
        Assert.Equal(GamePhase.Playing, sm.CurrentPhase);
    }

    [Fact]
    public void TickDuringPlaying_ReturnsFalse()
    {
        var sm = NewSm();
        Assert.False(sm.Tick(1.0f));
        Assert.Equal(GamePhase.Playing, sm.CurrentPhase);
    }

    // ── Movement locking ─────────────────────────────────────────────────────

    [Fact]
    public void MovementLocked_DuringGoalScored()
    {
        var sm = NewSm();
        sm.OnGoalScored(1);
        Assert.True(sm.IsMovementLocked);
    }

    [Fact]
    public void MovementUnlocked_DuringFaceoff()
    {
        var sm = NewSm();
        sm.BeginFaceoffPrep();
        sm.Tick(GameRules.FaceoffPrepDuration + 0.01f);
        Assert.False(sm.IsMovementLocked);
    }

    // ── Player registry ──────────────────────────────────────────────────────

    [Fact]
    public void FirstPlayer_GetsTeam0()
    {
        var sm = NewSm();
        var r = sm.OnPlayerConnected(peerId: 100);
        Assert.Equal(0, r.TeamId);
        Assert.Equal(1, r.Slot);
    }

    [Fact]
    public void SecondPlayer_GetsTeam1()
    {
        var sm = NewSm();
        sm.OnPlayerConnected(100);
        var r = sm.OnPlayerConnected(200);
        Assert.Equal(1, r.TeamId);
        Assert.Equal(2, r.Slot);
    }

    [Fact]
    public void BalancingAfterDisconnect()
    {
        var sm = NewSm();
        sm.RegisterHost(1);
        sm.OnPlayerConnected(100); // should balance
        sm.OnPlayerConnected(200);
        sm.OnPlayerDisconnected(1);
        var r = sm.OnPlayerConnected(300);
        // host was team 0, after disconnect team 0 has one fewer → new player → team 0
        int t0 = sm.CountPlayersOnTeam(0);
        int t1 = sm.CountPlayersOnTeam(1);
        Assert.True(t0 > 0 && t1 > 0);
    }

    // ── Icing ────────────────────────────────────────────────────────────────

    [Fact]
    public void Icing_TriggeredByLooseRuckPastGoalLine()
    {
        var sm = NewSm();
        sm.NotifyPuckCarried(carrierTeamId: 0, carrierZ: 5f);
        // carrier releases, puck now past -GoalLineZ
        sm.CheckIcingForLoosePuck(puckZ: -30f);
        Assert.Equal(0, sm.IcingTeamId);
    }

    [Fact]
    public void Icing_ExpiresAfterTimer()
    {
        var sm = NewSm();
        sm.NotifyPuckCarried(0, 5f);
        sm.CheckIcingForLoosePuck(-30f);
        Assert.Equal(0, sm.IcingTeamId);
        sm.Tick(GameRules.IcingGhostDuration + 0.01f);
        Assert.Equal(-1, sm.IcingTeamId);
    }

    [Fact]
    public void Icing_ClearedByOpponentPickup()
    {
        var sm = NewSm();
        sm.NotifyPuckCarried(0, 5f);
        sm.CheckIcingForLoosePuck(-30f);
        Assert.Equal(0, sm.IcingTeamId);

        // Team 1 picks up the puck
        sm.NotifyPuckCarried(carrierTeamId: 1, carrierZ: -10f);
        Assert.Equal(-1, sm.IcingTeamId);
    }

    [Fact]
    public void Icing_DoesNotTriggerIfShotFromAttackingHalf()
    {
        var sm = NewSm();
        sm.NotifyPuckCarried(0, -5f); // already in attacking half
        sm.CheckIcingForLoosePuck(-30f);
        Assert.Equal(-1, sm.IcingTeamId);
    }

    // ── Ghost state ──────────────────────────────────────────────────────────

    [Fact]
    public void GhostState_OffsideSkaterGhosted()
    {
        var sm = NewSm();
        sm.RegisterHost(1); // team 0 if they're first
        var positions = new Dictionary<int, Vector3>
        {
            [1] = new(0, 1, -10), // past team 0's attacking blue line
        };
        var puckPos = new Vector3(0, 0, 0); // puck not in zone
        var ghosts = sm.ComputeGhostState(positions, puckCarrierPeerId: -1, puckPos);
        Assert.True(ghosts[1]);
    }

    [Fact]
    public void GhostState_CarrierNotGhostedByOffside()
    {
        var sm = NewSm();
        sm.RegisterHost(1);
        var positions = new Dictionary<int, Vector3>
        {
            [1] = new(0, 1, -10),
        };
        var ghosts = sm.ComputeGhostState(positions, puckCarrierPeerId: 1, new Vector3(0, 0, 0));
        Assert.False(ghosts[1]);
    }

    [Fact]
    public void GhostState_IcingTeamAllGhosted()
    {
        var sm = NewSm();
        sm.RegisterHost(1); // team 0
        sm.NotifyPuckCarried(0, 5f);
        sm.CheckIcingForLoosePuck(-30f); // icing on team 0
        var positions = new Dictionary<int, Vector3> { [1] = new(0, 1, 0) };
        var ghosts = sm.ComputeGhostState(positions, puckCarrierPeerId: -1, new Vector3(0, 0, 0));
        Assert.True(ghosts[1]);
    }

    [Fact]
    public void GhostState_NoGhostsDuringDeadPuckPhase()
    {
        var sm = NewSm();
        sm.RegisterHost(1);
        sm.OnGoalScored(1); // → GoalScored phase
        var positions = new Dictionary<int, Vector3>
        {
            [1] = new(0, 1, -10), // would be offside normally
        };
        var ghosts = sm.ComputeGhostState(positions, puckCarrierPeerId: -1, new Vector3(0, 0, 0));
        Assert.False(ghosts[1]);
    }

    // ── Reset ────────────────────────────────────────────────────────────────

    [Fact]
    public void ResetScores_ZerosBothTeams()
    {
        var sm = NewSm();
        sm.OnGoalScored(1);
        sm.ResetScores();
        Assert.Equal(0, sm.Scores[0]);
        Assert.Equal(0, sm.Scores[1]);
    }

    [Fact]
    public void BeginFaceoffPrep_ClearsIcing()
    {
        var sm = NewSm();
        sm.NotifyPuckCarried(0, 5f);
        sm.CheckIcingForLoosePuck(-30f);
        Assert.Equal(0, sm.IcingTeamId);
        sm.BeginFaceoffPrep();
        Assert.Equal(-1, sm.IcingTeamId);
    }

    // ── Remote state application ────────────────────────────────────────────

    [Fact]
    public void ApplyRemoteState_UpdatesScoresAndPhase()
    {
        var sm = NewSm();
        sm.ApplyRemoteState(score0: 3, score1: 2, phase: GamePhase.Faceoff);
        Assert.Equal(3, sm.Scores[0]);
        Assert.Equal(2, sm.Scores[1]);
        Assert.Equal(GamePhase.Faceoff, sm.CurrentPhase);
    }
}
