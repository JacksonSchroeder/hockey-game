using HockeyGame.Logic.Rules;
using Xunit;

namespace HockeyGame.Tests.Domain.Rules;

public class PlayerRulesTests
{
    [Theory]
    [InlineData(0, 0, 0)]   // tie → team 0
    [InlineData(1, 0, 1)]   // team 1 smaller
    [InlineData(0, 1, 0)]   // team 0 smaller
    [InlineData(3, 2, 1)]   // balance toward smaller
    public void AssignTeam_BalancesByCount(int team0, int team1, int expected) =>
        Assert.Equal(expected, PlayerRules.AssignTeam(team0, team1));

    [Fact]
    public void Team0Color_InWarmRange()
    {
        // Team 0 hue range is 340-380° (reds wrapping to near-red).
        // In normalized [0,1] this is ~0.944-0.055. With jitter=0 and slot=0,
        // center is 340 + 0.5*(40/3) = 346.66° → ~0.963
        var color = PlayerRules.GeneratePlayerColor(teamId: 0, existingCount: 0, jitter: 0f);
        // Verify it's a valid red-ish color (R dominates, or wraps through red)
        Assert.True(color.R > 0.5f || color.R > color.G);
    }

    [Fact]
    public void Team1Color_InCoolRange()
    {
        // Team 1 hue range 200-260° (cyans/blues).
        var color = PlayerRules.GeneratePlayerColor(teamId: 1, existingCount: 0, jitter: 0f);
        // Blue dominates cool colors
        Assert.True(color.B > color.R);
    }

    [Fact]
    public void DifferentSlots_ProduceDifferentHues()
    {
        var c0 = PlayerRules.GeneratePlayerColor(teamId: 0, existingCount: 0, jitter: 0f);
        var c1 = PlayerRules.GeneratePlayerColor(teamId: 0, existingCount: 1, jitter: 0f);
        Assert.NotEqual((c0.R, c0.G, c0.B), (c1.R, c1.G, c1.B));
    }

    [Fact]
    public void Jitter_StayedDeterministic()
    {
        var a = PlayerRules.GeneratePlayerColor(0, 0, 0.5f);
        var b = PlayerRules.GeneratePlayerColor(0, 0, 0.5f);
        Assert.Equal(a, b);
    }
}
