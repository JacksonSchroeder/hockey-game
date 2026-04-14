using HockeyGame.Logic.State;

namespace HockeyGame.Logic.Rules;

// Pure game rules about game phases. No engine, no state — just classification.
public static class PhaseRules
{
    // Dead-puck phases suppress all player movement and input. Controllers check
    // this via IGameStateProvider; they don't ask GameManager directly.
    public static bool IsDeadPuckPhase(GamePhase phase) =>
        phase == GamePhase.GoalScored || phase == GamePhase.FaceoffPrep;

    public static bool IsMovementLocked(GamePhase phase) => IsDeadPuckPhase(phase);
}
