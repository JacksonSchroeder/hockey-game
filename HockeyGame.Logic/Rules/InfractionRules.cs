using HockeyGame.Logic.Config;

namespace HockeyGame.Logic.Rules;

// Rules about hockey infractions that trigger ghost mode.
// Pure functions — take positions/velocities, return bool or decisions.
//
// Team 0 attacks toward -Z (defends +Z goal at z = +GoalLineZ).
// Team 1 attacks toward +Z (defends -Z goal at z = -GoalLineZ).
public static class InfractionRules
{
    // Offside: skater in their attacking zone while the puck hasn't crossed the blue line.
    // Carrier is never offside against themselves.
    public static bool IsOffside(
        float skaterZ,
        int skaterTeamId,
        float puckZ,
        bool skaterIsCarrier)
    {
        if (skaterIsCarrier) return false;
        if (skaterTeamId == 0)
        {
            // Team 0 attacking zone: z < -BlueLineZ
            return skaterZ < -GameRules.BlueLineZ && puckZ >= -GameRules.BlueLineZ;
        }
        else
        {
            // Team 1 attacking zone: z > BlueLineZ
            return skaterZ > GameRules.BlueLineZ && puckZ <= GameRules.BlueLineZ;
        }
    }

    // Icing: puck shot from own half past opponent's goal line.
    // Team 0 releases from z > 0 and puck crosses z < -GoalLineZ → icing on team 0.
    // Team 1 releases from z < 0 and puck crosses z > +GoalLineZ → icing on team 1.
    // Returns the offending team id, or -1 if no icing.
    public static int CheckIcing(int lastCarrierTeamId, float lastCarrierZ, float puckZ)
    {
        if (lastCarrierTeamId == -1) return -1;
        if (lastCarrierTeamId == 0 && lastCarrierZ > 0.0f && puckZ < -GameRules.GoalLineZ)
            return 0;
        if (lastCarrierTeamId == 1 && lastCarrierZ < 0.0f && puckZ > GameRules.GoalLineZ)
            return 1;
        return -1;
    }
}
