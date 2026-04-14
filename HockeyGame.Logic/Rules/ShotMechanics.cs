using System;
using System.Numerics;

namespace HockeyGame.Logic.Rules;

// Shot power and direction math. Extracted from SkaterController._release_wrister,
// _release_slapper, and the wall-pin release in _apply_blade_from_mouse.
public static class ShotMechanics
{
    public record WristerConfig(
        float MinWristerPower,
        float MaxWristerPower,
        float MaxWristerChargeDistance,
        float BackhandPowerCoefficient,
        float QuickShotPower,
        float QuickShotThreshold,
        float WristerElevation);

    public record SlapperConfig(
        float MinSlapperPower,
        float MaxSlapperPower,
        float MaxSlapperChargeTime,
        float SlapperElevation);

    public record ShotResult(Vector3 Direction, float Power);

    // Wrister release math. `chargeDistance` accumulates blade movement while aiming;
    // short charges become quick shots (small charge_t), long charges become full wristers.
    public static ShotResult ReleaseWrister(
        Vector3 playerPos,
        Vector3 mouseWorldPos,
        Vector3 bladeWorldPos,
        Vector3 bladeLocalPos,
        Vector3 shoulderLocalPos,
        bool isLeftHanded,
        bool isElevated,
        float chargeDistance,
        WristerConfig cfg)
    {
        var targetXZ = new Vector3(mouseWorldPos.X, 0, mouseWorldPos.Z);
        float chargeT = Clamp01(chargeDistance / cfg.MaxWristerChargeDistance);

        if (chargeT < cfg.QuickShotThreshold)
        {
            // Quick shot — aim is from blade position directly, low power.
            var bladeXZ = new Vector3(bladeWorldPos.X, 0, bladeWorldPos.Z);
            Vector3 dir = Normalize(targetXZ - bladeXZ);
            float y = isElevated ? cfg.WristerElevation : 0.0f;
            return new ShotResult(Normalize(new Vector3(dir.X, y, dir.Z)), cfg.QuickShotPower);
        }
        else
        {
            // Full wrister — aim from player position, power scales with charge.
            var playerXZ = new Vector3(playerPos.X, 0, playerPos.Z);
            Vector3 dir = Normalize(targetXZ - playerXZ);
            float power = Lerp(cfg.MinWristerPower, cfg.MaxWristerPower, chargeT);

            float handSign = isLeftHanded ? -1.0f : 1.0f;
            bool isBackhand = MathF.Sign(bladeLocalPos.X - shoulderLocalPos.X) != MathF.Sign(handSign);
            if (isBackhand) power *= cfg.BackhandPowerCoefficient;

            float y = isElevated ? cfg.WristerElevation : 0.0f;
            return new ShotResult(Normalize(new Vector3(dir.X, y, dir.Z)), power);
        }
    }

    // Slapper release math. Power scales linearly with charge time.
    public static ShotResult ReleaseSlapper(
        Vector3 bladeWorldPos,
        Vector3 mouseWorldPos,
        bool isElevated,
        float chargeTime,
        SlapperConfig cfg)
    {
        var bladeXZ = new Vector3(bladeWorldPos.X, 0, bladeWorldPos.Z);
        var targetXZ = new Vector3(mouseWorldPos.X, 0, mouseWorldPos.Z);
        Vector3 dir = Normalize(targetXZ - bladeXZ);
        float chargeT = Clamp01(chargeTime / cfg.MaxSlapperChargeTime);
        float power = Lerp(cfg.MinSlapperPower, cfg.MaxSlapperPower, chargeT);
        float y = isElevated ? cfg.SlapperElevation : 0.0f;
        return new ShotResult(Normalize(new Vector3(dir.X, y, dir.Z)), power);
    }

    // Should a wall-pin force a puck release? True when the blade is squeezed against
    // a wall hard enough that we'd rather auto-release than fight the physics.
    public static bool ShouldReleaseOnWallPin(float squeeze, float threshold) =>
        squeeze > threshold;

    private static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static Vector3 Normalize(Vector3 v) =>
        v.LengthSquared() > 1e-9f ? Vector3.Normalize(v) : Vector3.Zero;
}
