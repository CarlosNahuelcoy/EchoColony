using HarmonyLib;
using RimWorld;
using Verse;

namespace EchoColony
{
    [HarmonyPatch(typeof(PlayLog), nameof(PlayLog.Add))]
    public static class Patch_PlayLog_Add
    {
        public static void Postfix(LogEntry entry)
        {
            if (entry is PlayLogEntry_Interaction interaction)
            {
                // Obtenemos los peones involucrados
                var initiator = Traverse.Create(interaction).Field("initiator").GetValue<Pawn>();
                var recipient = Traverse.Create(interaction).Field("recipient").GetValue<Pawn>();
                var playerFaction = Faction.OfPlayerSilentFail;

                // Solo registramos si ambos son de la colonia
               if (playerFaction != null &&
                initiator?.Faction == playerFaction &&
                recipient?.Faction == playerFaction)
            {
                string message = interaction.ToGameStringFromPOV(initiator, false);
                EventLogger.LogEvent(message);
            }
            }
        }
    }
}
