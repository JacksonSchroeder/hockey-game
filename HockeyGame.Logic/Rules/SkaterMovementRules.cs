using System;
using System.Numerics;

namespace HockeyGame.Logic.Rules;

// Skater movement math extracted from SkaterController._apply_movement.
// Pure functions — take inputs and current state, return new velocity.
public static class SkaterMovementRules
{
    public record MovementConfig(
        float Thrust,
        float Friction,
        float MaxSpeed,
        float MoveDeadzone,
        float BrakeMultiplier,
        float PuckCarrySpeedMultiplier,
        float BackwardThrustMultiplier,
        float CrossoverThrustMultiplier);

    // Compute new velocity given current state, move input, and config.
    // Returns the new velocity to assign to the skater.
    public static Vector3 ApplyMovement(
        Vector3 currentVelocity,
        Vector2 moveInput,
        float skaterFacingRotationY,
        bool hasPuck,
        bool brake,
        float delta,
        MovementConfig cfg)
    {
        Vector3 velocity = currentVelocity;

        if (moveInput.Length() > cfg.MoveDeadzone)
        {
            Vector3 thrustDir = new(moveInput.X, 0, moveInput.Y);
            Vector2 facingDir = new(-MathF.Sin(skaterFacingRotationY), -MathF.Cos(skaterFacingRotationY));
            Vector2 moveNormalized = Vector2.Normalize(moveInput);
            float moveDot = Vector2.Dot(facingDir, moveNormalized);

            float thrustScale = moveDot >= 0.0f
                ? Lerp(cfg.CrossoverThrustMultiplier, 1.0f, moveDot)
                : Lerp(cfg.BackwardThrustMultiplier, cfg.CrossoverThrustMultiplier, moveDot + 1.0f);

            velocity += thrustDir * cfg.Thrust * thrustScale * delta;

            float effectiveMaxSpeed = hasPuck ? cfg.MaxSpeed * cfg.PuckCarrySpeedMultiplier : cfg.MaxSpeed;
            Vector2 horizVel = new(velocity.X, velocity.Z);
            float speed = horizVel.Length();
            if (speed > effectiveMaxSpeed)
            {
                // Preserve over-max speed that was already there before thrust — allows
                // momentum-based plays (e.g. picking up speed from a body check).
                Vector2 preThrust = new(
                    velocity.X - thrustDir.X * cfg.Thrust * thrustScale * delta,
                    velocity.Z - thrustDir.Z * cfg.Thrust * thrustScale * delta);
                float preThrustSpeed = preThrust.Length();
                float targetSpeed = MathF.Max(preThrustSpeed, effectiveMaxSpeed);
                if (speed > targetSpeed)
                {
                    Vector2 limited = Vector2.Normalize(horizVel) * targetSpeed;
                    velocity.X = limited.X;
                    velocity.Z = limited.Y;
                }
            }
        }

        // Friction (or braking)
        Vector2 horiz = new(velocity.X, velocity.Z);
        float currentFriction = brake ? cfg.Friction * cfg.BrakeMultiplier : cfg.Friction;
        horiz = MoveToward(horiz, Vector2.Zero, currentFriction * delta);
        velocity.X = horiz.X;
        velocity.Z = horiz.Y;
        return velocity;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static Vector2 MoveToward(Vector2 from, Vector2 to, float delta)
    {
        Vector2 diff = to - from;
        float len = diff.Length();
        if (len <= delta || len == 0) return to;
        return from + diff / len * delta;
    }
}
