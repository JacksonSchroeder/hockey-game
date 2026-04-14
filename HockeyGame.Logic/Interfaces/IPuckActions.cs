using System.Numerics;

namespace HockeyGame.Logic.Interfaces;

// Contract for what controllers and GameManager need from the puck. The Puck node
// (RigidBody3D) implements this and handles physics/collision behind the interface.
public interface IPuckActions
{
    Vector3 Position { get; }
    Vector3 Velocity { get; }
    bool HasCarrier { get; }
    int CarrierPeerId { get; }  // -1 if no carrier
    bool PickupLocked { get; set; }

    void SetPosition(Vector3 pos);
    void SetVelocity(Vector3 vel);

    // Force release — puck drops to where the carrier was, zero velocity. Used on
    // goal score, disconnect, and reset.
    void Drop();

    // Reset puck to center ice faceoff position. Used during FaceoffPrep.
    void Reset();

    // Release with a specific direction+power — wrister/slapper shot.
    void Release(Vector3 direction, float power);

    // Server vs client mode. Server drives physics; clients run prediction or
    // interpolation depending on carrier state.
    void SetServerMode(bool isServer);
    void SetClientPredictionMode(bool active);
}
