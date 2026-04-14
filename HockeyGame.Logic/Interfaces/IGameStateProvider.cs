namespace HockeyGame.Logic.Interfaces;

// Exposes the minimal subset of game state that controllers care about. Keeps
// controllers decoupled from GameManager — they get an IGameStateProvider at
// setup time and ask this instead of looking up a singleton.
public interface IGameStateProvider
{
    bool IsMovementLocked { get; }
}
