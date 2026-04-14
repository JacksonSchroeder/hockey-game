using HockeyGame.Logic.Rules;
using HockeyGame.Logic.State;
using Xunit;

namespace HockeyGame.Tests.Domain.Rules;

public class PhaseRulesTests
{
    [Theory]
    [InlineData(GamePhase.GoalScored, true)]
    [InlineData(GamePhase.FaceoffPrep, true)]
    [InlineData(GamePhase.Playing, false)]
    [InlineData(GamePhase.Faceoff, false)]
    public void IsDeadPuckPhase(GamePhase phase, bool expected) =>
        Assert.Equal(expected, PhaseRules.IsDeadPuckPhase(phase));

    [Theory]
    [InlineData(GamePhase.GoalScored, true)]
    [InlineData(GamePhase.FaceoffPrep, true)]
    [InlineData(GamePhase.Playing, false)]
    [InlineData(GamePhase.Faceoff, false)]
    public void IsMovementLocked(GamePhase phase, bool expected) =>
        Assert.Equal(expected, PhaseRules.IsMovementLocked(phase));
}
