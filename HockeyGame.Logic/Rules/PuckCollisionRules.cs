using System;
using System.Numerics;

namespace HockeyGame.Logic.Rules;

// Pure physics math for puck interactions. Extracted from Puck.gd so the reasoning
// is testable without a physics engine. Infrastructure (Puck node) calls these and
// applies results to the RigidBody3D.
public static class PuckCollisionRules
{
    // Same team → can't poke check. Different teams or nulls → allowed.
    public static bool CanPokeCheck(int carrierTeamId, int checkerTeamId) =>
        carrierTeamId != checkerTeamId;

    // Billiard-style reflection off the blade. Contact normal is the blade→puck vector.
    // Returns the new velocity (horizontal only; caller layers in elevation separately).
    //
    // `deflectBlend` ∈ [0, 1] lerps between pure pass-through (0) and pure reflection (1).
    // `speedRetain` ∈ [0, 1] multiplier on resulting speed (energy loss).
    public static Vector3 DeflectOffBlade(
        Vector3 incomingVelocity,
        Vector3 contactNormal,
        float deflectBlend,
        float speedRetain)
    {
        Vector3 horiz = new(incomingVelocity.X, 0, incomingVelocity.Z);
        float speed = incomingVelocity.Length();
        Vector3 reflected = horiz - 2.0f * Vector3.Dot(horiz, contactNormal) * contactNormal;
        Vector3 newDir = Vector3.Lerp(Normalize(horiz), Normalize(reflected), deflectBlend);
        return Normalize(newDir) * speed * speedRetain;
    }

    // Apply elevation to a horizontal deflection direction. deflectionAngleDeg is
    // the upward angle (e.g. 35° gives a moderate arc).
    public static Vector3 ApplyDeflectionElevation(Vector3 horizontalDir, float elevationAngleDeg)
    {
        float rad = elevationAngleDeg * MathF.PI / 180.0f;
        return Normalize(new Vector3(
            horizontalDir.X * MathF.Cos(rad),
            MathF.Sin(rad),
            horizontalDir.Z * MathF.Cos(rad)));
    }

    // Body-check strip: transfers checker momentum into puck, scaled by hit direction.
    public static Vector3 BodyCheckStripVelocity(Vector3 hitDirection, float puckSpeed) =>
        hitDirection * puckSpeed;

    // Poke check strip: blend checker blade velocity with a fraction of carrier velocity,
    // or push away from checker if blade is too slow. Returns new puck velocity direction
    // scaled by stripSpeed.
    public static Vector3 PokeStripVelocity(
        Vector3 checkerBladeVel,
        Vector3 carrierBladeVel,
        Vector3 carrierPos,
        Vector3 checkerPos,
        float carrierVelBlend,
        float stripSpeed,
        Vector3 fallbackDirection)
    {
        Vector3 checkerHoriz = new(checkerBladeVel.X, 0, checkerBladeVel.Z);
        Vector3 carrierHoriz = new(carrierBladeVel.X, 0, carrierBladeVel.Z);
        Vector3 stripDir;
        if (checkerHoriz.Length() > 0.5f)
        {
            stripDir = checkerHoriz + carrierHoriz * carrierVelBlend;
        }
        else
        {
            stripDir = new Vector3(carrierPos.X - checkerPos.X, 0, carrierPos.Z - checkerPos.Z);
        }
        stripDir.Y = 0;
        if (stripDir.LengthSquared() > 1e-6f) stripDir = Normalize(stripDir);
        else stripDir = fallbackDirection;
        return stripDir * stripSpeed;
    }

    // Body block: reflect loose puck off skater body. Dampened (billiard style).
    public static Vector3 BodyBlockVelocity(
        Vector3 incomingVelocity,
        Vector3 contactNormal,
        float dampen)
    {
        Vector3 horiz = new(incomingVelocity.X, 0, incomingVelocity.Z);
        Vector3 reflected = horiz - 2.0f * Vector3.Dot(horiz, contactNormal) * contactNormal;
        if (reflected.LengthSquared() < 1e-6f) reflected = contactNormal;
        return Normalize(reflected) * horiz.Length() * dampen;
    }

    private static Vector3 Normalize(Vector3 v) =>
        v.LengthSquared() > 1e-9f ? Vector3.Normalize(v) : Vector3.Zero;
}
