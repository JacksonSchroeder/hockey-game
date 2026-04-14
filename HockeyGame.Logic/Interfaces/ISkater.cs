using System.Numerics;

namespace HockeyGame.Logic.Interfaces;

// Contract for what controllers (and domain logic) need from a skater. The actual
// Skater node implements this and converts between System.Numerics and Godot types
// at the boundary. Tests use NSubstitute to mock ISkater so controller logic is
// testable without the engine.
public interface ISkater
{
    Vector3 Position { get; set; }
    Vector3 Velocity { get; set; }
    Vector3 Rotation { get; set; }
    Vector2 Facing { get; }
    Vector3 BladePosition { get; }      // local position relative to upper body
    Vector3 ShoulderPosition { get; }    // local position
    float UpperBodyRotationY { get; }
    bool IsLeftHanded { get; }
    bool IsElevated { get; set; }
    bool IsGhost { get; }

    void SetFacing(Vector2 facing);
    void SetBladePosition(Vector3 pos);
    void SetUpperBodyRotation(float rotY);
    void SetGhost(bool ghost);
    void UpdateStickMesh();

    // Coordinate conversions using the upper body as the pivot. Needed by shot
    // mechanics and collision math (e.g. computing world position of blade).
    Vector3 UpperBodyToGlobal(Vector3 localPos);
    Vector3 UpperBodyToLocal(Vector3 worldPos);

    // Blade world-space velocity — computed by the skater node from frame-to-frame
    // blade positions. Controllers use this for charge distance calculations.
    Vector3 BladeWorldVelocity { get; }

    // Blade wall clamping — the Skater owns its RayCast3D. Controllers call this
    // after computing a desired blade position to get the wall-clamped version.
    Vector3 ClampBladeToWalls(Vector3 desired);
    float GetWallSqueeze(Vector3 intended, Vector3 clamped);
    Vector3 GetBladeWallNormal();
    float WallSqueezeThreshold { get; }
}
