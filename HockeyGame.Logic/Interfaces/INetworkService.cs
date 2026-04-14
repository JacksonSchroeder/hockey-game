using System.Numerics;

namespace HockeyGame.Logic.Interfaces;

// Contract for what the application layer needs from the network layer.
// The Godot NetworkManager node implements this using ENet RPCs.
public interface INetworkService
{
    bool IsHost { get; }
    int LocalPeerId { get; }

    void NotifyGoalToAll(int scoringTeamId, int score0, int score1);
    void NotifyResetToAll();

    void SendSlotAssignment(int peerId, int slot, int teamId, PlayerColorData color);
    void SendSyncExistingPlayers(int peerId, object[] existingPlayers);
    void SendSpawnRemoteSkater(int peerId, int slot, int teamId, PlayerColorData color);

    void SendFaceoffPositions(FaceoffPositionEntry[] positions);

    void SendPuckPickedUp(int peerId);
    void SendPuckStolen(int peerId);
    void SendPuckRelease(Vector3 direction, float power);

    void RegisterLocalController(object controller);
    void RegisterRemoteController(int peerId, object controller);
    void UnregisterRemoteController(int peerId);
}

// DTOs for the network interface — avoids importing Godot types into Logic.
public readonly record struct PlayerColorData(float R, float G, float B, float A);
public readonly record struct FaceoffPositionEntry(int PeerId, float X, float Y, float Z);
