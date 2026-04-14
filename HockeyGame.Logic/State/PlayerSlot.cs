using System.Numerics;

namespace HockeyGame.Logic.State;

// Domain view of a player. The application layer pairs this with infrastructure refs
// (Skater node, SkaterController) in its own PlayerRecord type.
public class PlayerSlot
{
    public int PeerId { get; init; }
    public int Slot { get; init; }
    public int TeamId { get; set; }
    public Vector3 FaceoffPosition { get; set; }
}
