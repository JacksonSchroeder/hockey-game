using HockeyGame.Logic.Config;
using HockeyGame.Logic.Rules;
using Xunit;

namespace HockeyGame.Tests.Domain.Rules;

public class InfractionRulesTests
{
    // Offside — Team 0 attacks -Z

    [Fact]
    public void Team0_NotOffside_WhenBehindBlueLine()
    {
        // Skater at z = 0 (center ice, not in attacking zone)
        Assert.False(InfractionRules.IsOffside(
            skaterZ: 0f, skaterTeamId: 0, puckZ: 0f, skaterIsCarrier: false));
    }

    [Fact]
    public void Team0_Offside_WhenPastBlueLineAheadOfPuck()
    {
        // Skater is in attacking zone (z < -BlueLineZ), puck is still outside
        Assert.True(InfractionRules.IsOffside(
            skaterZ: -10f, skaterTeamId: 0, puckZ: 0f, skaterIsCarrier: false));
    }

    [Fact]
    public void Team0_NotOffside_WhenPuckAlsoInZone()
    {
        // Puck entered the zone before/with the skater
        Assert.False(InfractionRules.IsOffside(
            skaterZ: -10f, skaterTeamId: 0, puckZ: -10f, skaterIsCarrier: false));
    }

    [Fact]
    public void Team1_Offside_WhenPastBlueLineAheadOfPuck()
    {
        // Team 1 attacks +Z — skater in +Z zone while puck is still behind
        Assert.True(InfractionRules.IsOffside(
            skaterZ: 10f, skaterTeamId: 1, puckZ: 0f, skaterIsCarrier: false));
    }

    [Fact]
    public void Carrier_NeverOffside()
    {
        Assert.False(InfractionRules.IsOffside(
            skaterZ: -10f, skaterTeamId: 0, puckZ: 0f, skaterIsCarrier: true));
    }

    [Fact]
    public void PuckOnBlueLine_ClearsOffside()
    {
        // Puck at exactly -BlueLineZ is considered "across" from team 0's perspective
        Assert.False(InfractionRules.IsOffside(
            skaterZ: -10f, skaterTeamId: 0, puckZ: -GameRules.BlueLineZ, skaterIsCarrier: false));
    }

    // Icing — released from own half, crossed opponent's goal line

    [Fact]
    public void Team0Icing_ReleasedFromOwnHalf_CrossesOpponentGoalLine()
    {
        // Team 0 released from z > 0, puck now past -GoalLineZ (opponent's goal line)
        int result = InfractionRules.CheckIcing(
            lastCarrierTeamId: 0, lastCarrierZ: 5f, puckZ: -30f);
        Assert.Equal(0, result);
    }

    [Fact]
    public void Team0_NoIcing_WhenReleasedFromAttackingHalf()
    {
        // Released from z < 0 — that's legal offensive play
        int result = InfractionRules.CheckIcing(
            lastCarrierTeamId: 0, lastCarrierZ: -5f, puckZ: -30f);
        Assert.Equal(-1, result);
    }

    [Fact]
    public void Team1Icing_ReleasedFromOwnHalf_CrossesOpponentGoalLine()
    {
        int result = InfractionRules.CheckIcing(
            lastCarrierTeamId: 1, lastCarrierZ: -5f, puckZ: 30f);
        Assert.Equal(1, result);
    }

    [Fact]
    public void NoIcing_WhenNoCarrier()
    {
        int result = InfractionRules.CheckIcing(
            lastCarrierTeamId: -1, lastCarrierZ: 0f, puckZ: -30f);
        Assert.Equal(-1, result);
    }

    [Fact]
    public void NoIcing_WhenPuckShortOfGoalLine()
    {
        int result = InfractionRules.CheckIcing(
            lastCarrierTeamId: 0, lastCarrierZ: 5f, puckZ: -20f); // short of -26.6
        Assert.Equal(-1, result);
    }
}
