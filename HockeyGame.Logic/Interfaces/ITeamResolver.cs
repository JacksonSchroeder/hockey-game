namespace HockeyGame.Logic.Interfaces;

// Resolves which team a skater belongs to. Puck uses this for poke-check
// eligibility (CanPokeCheck rule). GameManager implements this by looking up
// its player records. Prevents Puck from reaching into GameManager directly.
public interface ITeamResolver
{
    // Returns team id (0 or 1), or -1 if the skater isn't registered.
    int GetTeamForSkater(ISkater skater);
}
