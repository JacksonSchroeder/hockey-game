using System.Numerics;
using HockeyGame.Logic.Rules;
using Xunit;

namespace HockeyGame.Tests.Domain.Rules;

public class ShotMechanicsTests
{
    private static readonly ShotMechanics.WristerConfig WristerCfg = new(
        MinWristerPower: 8f,
        MaxWristerPower: 25f,
        MaxWristerChargeDistance: 3f,
        BackhandPowerCoefficient: 0.75f,
        QuickShotPower: 12f,
        QuickShotThreshold: 0.1f,
        WristerElevation: 0.3f);

    private static readonly ShotMechanics.SlapperConfig SlapperCfg = new(
        MinSlapperPower: 20f,
        MaxSlapperPower: 40f,
        MaxSlapperChargeTime: 1f,
        SlapperElevation: 0.15f);

    [Fact]
    public void QuickShot_UsesQuickShotPower()
    {
        var result = ShotMechanics.ReleaseWrister(
            playerPos: Vector3.Zero,
            mouseWorldPos: new Vector3(10, 0, 0),
            bladeWorldPos: new Vector3(0.5f, 0, 0),
            bladeLocalPos: new Vector3(0.5f, 0, 0),
            shoulderLocalPos: new Vector3(0.35f, 0, 0),
            isLeftHanded: false,
            isElevated: false,
            chargeDistance: 0.01f, // very short charge → quick shot
            cfg: WristerCfg);
        Assert.Equal(WristerCfg.QuickShotPower, result.Power);
    }

    [Fact]
    public void FullCharge_MaxesPower()
    {
        var result = ShotMechanics.ReleaseWrister(
            playerPos: Vector3.Zero,
            mouseWorldPos: new Vector3(10, 0, 0),
            bladeWorldPos: new Vector3(0.5f, 0, 0),
            bladeLocalPos: new Vector3(0.5f, 0, 0),
            shoulderLocalPos: new Vector3(0.35f, 0, 0),
            isLeftHanded: false,
            isElevated: false,
            chargeDistance: 5f, // over max
            cfg: WristerCfg);
        Assert.Equal(WristerCfg.MaxWristerPower, result.Power, 2);
    }

    [Fact]
    public void Backhand_AppliesPenalty()
    {
        // Right-handed forehand: blade on +X side of shoulder
        var forehand = ShotMechanics.ReleaseWrister(
            Vector3.Zero, new Vector3(10, 0, 0),
            bladeWorldPos: new Vector3(0.5f, 0, 0),
            bladeLocalPos: new Vector3(0.5f, 0, 0),
            shoulderLocalPos: new Vector3(0.35f, 0, 0),
            isLeftHanded: false, isElevated: false,
            chargeDistance: 3f, cfg: WristerCfg);

        // Right-handed backhand: blade on -X side of shoulder
        var backhand = ShotMechanics.ReleaseWrister(
            Vector3.Zero, new Vector3(10, 0, 0),
            bladeWorldPos: new Vector3(-0.5f, 0, 0),
            bladeLocalPos: new Vector3(-0.5f, 0, 0),
            shoulderLocalPos: new Vector3(0.35f, 0, 0),
            isLeftHanded: false, isElevated: false,
            chargeDistance: 3f, cfg: WristerCfg);

        Assert.True(backhand.Power < forehand.Power);
    }

    [Fact]
    public void Elevated_AddsYComponent()
    {
        var flat = ShotMechanics.ReleaseWrister(
            Vector3.Zero, new Vector3(10, 0, 0),
            new Vector3(0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(0.35f, 0, 0),
            false, isElevated: false, 3f, WristerCfg);
        var elevated = ShotMechanics.ReleaseWrister(
            Vector3.Zero, new Vector3(10, 0, 0),
            new Vector3(0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(0.35f, 0, 0),
            false, isElevated: true, 3f, WristerCfg);
        Assert.Equal(0f, flat.Direction.Y, 3);
        Assert.True(elevated.Direction.Y > 0);
    }

    [Fact]
    public void Slapper_PowerScalesWithChargeTime()
    {
        var short_ = ShotMechanics.ReleaseSlapper(
            bladeWorldPos: Vector3.Zero, mouseWorldPos: new Vector3(10, 0, 0),
            isElevated: false, chargeTime: 0.1f, cfg: SlapperCfg);
        var long_ = ShotMechanics.ReleaseSlapper(
            Vector3.Zero, new Vector3(10, 0, 0),
            false, chargeTime: 1.0f, cfg: SlapperCfg);
        Assert.True(long_.Power > short_.Power);
        Assert.Equal(SlapperCfg.MaxSlapperPower, long_.Power, 2);
    }

    [Theory]
    [InlineData(0.5f, 0.3f, true)]
    [InlineData(0.2f, 0.3f, false)]
    public void WallPin_TriggersAtThreshold(float squeeze, float threshold, bool expected) =>
        Assert.Equal(expected, ShotMechanics.ShouldReleaseOnWallPin(squeeze, threshold));
}
