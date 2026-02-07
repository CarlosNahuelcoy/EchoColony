using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Herramientas de debug para diagnosticar problemas con el sistema de mensajes espontáneos
    /// </summary>
    public static class SpontaneousMessagesDebug
    {
        /// <summary>
        /// Verifica el estado completo del sistema y muestra diagnóstico
        /// </summary>
        public static void CheckSystemStatus()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("═══════════════════════════════════════════");
            report.AppendLine("SPONTANEOUS MESSAGES - SYSTEM DIAGNOSTIC");
            report.AppendLine("═══════════════════════════════════════════");
            report.AppendLine();

            // 1. Verificar Settings
            report.AppendLine("1. SETTINGS:");
            if (MyMod.Settings == null)
            {
                report.AppendLine("   ❌ ERROR: MyMod.Settings is NULL!");
                Log.Error(report.ToString());
                Messages.Message("CRITICAL: MyMod.Settings is null!", MessageTypeDefOf.RejectInput);
                return;
            }

            report.AppendLine($"   Mode: {MyMod.Settings.spontaneousMessageMode}");
            report.AppendLine($"   Active: {MyMod.Settings.IsSpontaneousMessagesActive()}");
            report.AppendLine($"   Incidents Enabled: {MyMod.Settings.AreIncidentMessagesEnabled()}");
            report.AppendLine($"   Random Enabled: {MyMod.Settings.AreRandomMessagesEnabled()}");
            report.AppendLine($"   Max Messages/Day: {MyMod.Settings.defaultMaxMessagesPerColonistPerDay}");
            report.AppendLine($"   Cooldown Hours: {MyMod.Settings.defaultColonistCooldownHours}");
            report.AppendLine();

            // 2. Verificar GameComponent
            report.AppendLine("2. GAME COMPONENT:");
            var tracker = SpontaneousMessageTracker.Instance;
            if (tracker == null)
            {
                report.AppendLine("   ❌ ERROR: SpontaneousMessageTracker.Instance is NULL!");
                report.AppendLine("   → GameComponent not initialized!");
                report.AppendLine("   → Check if game is loaded");
            }
            else
            {
                report.AppendLine("   ✅ Tracker initialized");
            }
            report.AppendLine();

            // 3. Verificar MyStoryModComponent
            report.AppendLine("3. STORY MOD COMPONENT:");
            if (MyStoryModComponent.Instance == null)
            {
                report.AppendLine("   ❌ ERROR: MyStoryModComponent.Instance is NULL!");
                report.AppendLine("   → Cannot generate messages without this!");
            }
            else
            {
                report.AppendLine("   ✅ MyStoryModComponent available");
            }
            report.AppendLine();

            // 4. Verificar colonos elegibles
            report.AppendLine("4. ELIGIBLE COLONISTS:");
            if (Current.Game == null)
            {
                report.AppendLine("   ⚠ No game loaded");
            }
            else
            {
                var eligibleIncidents = SpontaneousMessageEvaluator.GetEligibleColonists(TriggerType.Incident);
                var eligibleRandom = SpontaneousMessageEvaluator.GetEligibleColonists(TriggerType.Random);
                
                report.AppendLine($"   For Incidents: {eligibleIncidents.Count} colonists");
                report.AppendLine($"   For Random: {eligibleRandom.Count} colonists");
                
                if (eligibleIncidents.Count == 0 && eligibleRandom.Count == 0)
                {
                    report.AppendLine("   ⚠ WARNING: No eligible colonists!");
                    report.AppendLine("   → Running detailed check...");
                    report.AppendLine();
                    CheckWhyNoEligibleColonists(report);
                }
                else
                {
                    report.AppendLine();
                    report.AppendLine("   Eligible for incidents:");
                    foreach (var pawn in eligibleIncidents.Take(5))
                    {
                        report.AppendLine($"   - {pawn.LabelShort}");
                    }
                    if (eligibleIncidents.Count > 5)
                        report.AppendLine($"   ... and {eligibleIncidents.Count - 5} more");
                }
            }
            report.AppendLine();

            // 5. Verificar Harmony patches
            report.AppendLine("5. HARMONY PATCHES:");
            report.AppendLine("   Checking if IncidentTriggers patch is active...");
            report.AppendLine("   (Patch should be auto-applied via [StaticConstructorOnStartup])");
            report.AppendLine();

            // 6. Verificar ChatGameComponent
            report.AppendLine("6. CHAT SYSTEM:");
            if (ChatGameComponent.Instance == null)
            {
                report.AppendLine("   ❌ ERROR: ChatGameComponent.Instance is NULL!");
            }
            else
            {
                report.AppendLine("   ✅ ChatGameComponent available");
            }
            report.AppendLine();

            // Resultado final
            report.AppendLine("═══════════════════════════════════════════");
            
            string fullReport = report.ToString();
            Log.Message(fullReport);
            
            // Determinar mensaje para el usuario
            if (tracker == null || MyStoryModComponent.Instance == null)
            {
                Messages.Message("❌ CRITICAL ERRORS FOUND! Check log for details.", MessageTypeDefOf.RejectInput);
            }
            else if (!MyMod.Settings.IsSpontaneousMessagesActive())
            {
                Messages.Message("⚠ System is DISABLED in settings!", MessageTypeDefOf.CautionInput);
            }
            else
            {
                Messages.Message("✅ System appears functional. Check log for full report.", MessageTypeDefOf.TaskCompletion);
            }
        }

        /// <summary>
        /// Chequea en detalle por qué no hay colonos elegibles
        /// </summary>
        private static void CheckWhyNoEligibleColonists(StringBuilder report)
        {
            if (Current.Game == null || Find.Maps == null)
            {
                report.AppendLine("   → No game or maps loaded");
                return;
            }

            int totalColonists = 0;
            int failedAge = 0;
            int failedTalking = 0;
            int failedSettings = 0;
            int failedCooldown = 0;
            int failedConsciousness = 0;
            int failedMentalState = 0;
            int failedDowned = 0;

            var tracker = SpontaneousMessageTracker.Instance;

            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    totalColonists++;
                    
                    // Check age
                    if (pawn.ageTracker != null && pawn.ageTracker.AgeBiologicalYearsFloat < 3f)
                    {
                        failedAge++;
                        continue;
                    }

                    // Check talking
                    var talking = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Talking) ?? 0f;
                    if (talking < 0.3f)
                    {
                        failedTalking++;
                        continue;
                    }

                    // Check settings
                    var settings = MyMod.Settings.GetOrCreateColonistSettings(pawn);
                    if (!settings.enabled)
                    {
                        failedSettings++;
                        continue;
                    }

                    // Check cooldown
                    if (tracker != null && !tracker.CanSendMessage(pawn, TriggerType.Incident))
                    {
                        failedCooldown++;
                        continue;
                    }

                    // Check consciousness
                    var consciousness = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Consciousness) ?? 1f;
                    if (consciousness < MyMod.Settings.minConsciousnessPercent / 100f)
                    {
                        failedConsciousness++;
                        continue;
                    }

                    // Check mental state
                    if (ColonistWillingnessEvaluator.IsInInvalidMentalState(pawn))
                    {
                        failedMentalState++;
                        continue;
                    }

                    // Check downed
                    if (pawn.Downed)
                    {
                        failedDowned++;
                        continue;
                    }
                }
            }

            report.AppendLine($"   DETAILED BREAKDOWN (Total: {totalColonists} colonists):");
            report.AppendLine($"   ❌ Too young (< 3 years): {failedAge}");
            report.AppendLine($"   ❌ Cannot talk (< 30%): {failedTalking}");
            report.AppendLine($"   ❌ Disabled in settings: {failedSettings}");
            report.AppendLine($"   ❌ On cooldown: {failedCooldown}");
            report.AppendLine($"   ❌ Low consciousness: {failedConsciousness}");
            report.AppendLine($"   ❌ Invalid mental state: {failedMentalState}");
            report.AppendLine($"   ❌ Downed: {failedDowned}");
        }

        /// <summary>
        /// Fuerza la generación de un mensaje de prueba desde un colono aleatorio
        /// </summary>
        public static void ForceTestMessage()
        {
            if (Current.Game == null)
            {
                Messages.Message("Need active game to test!", MessageTypeDefOf.RejectInput);
                return;
            }

            if (!MyMod.Settings.IsSpontaneousMessagesActive())
            {
                Messages.Message("System is DISABLED! Enable it in settings first.", MessageTypeDefOf.RejectInput);
                return;
            }

            // Obtener colonos elegibles
            var eligible = SpontaneousMessageEvaluator.GetEligibleColonists(TriggerType.Random);
            
            if (!eligible.Any())
            {
                // Si no hay elegibles para random, intentar con incidents
                eligible = SpontaneousMessageEvaluator.GetEligibleColonists(TriggerType.Incident);
            }

            if (!eligible.Any())
            {
                Messages.Message("No eligible colonists found! Check system status.", MessageTypeDefOf.RejectInput);
                Log.Warning("[EchoColony] ForceTestMessage: No eligible colonists");
                return;
            }

            // Seleccionar colono aleatorio
            var colonist = eligible.RandomElement();
            
            Log.Message($"[EchoColony] Forcing test message from: {colonist.LabelShort}");

            // Crear request de prueba
            var request = new MessageRequest(
                colonist,
                TriggerType.Random,
                "This is a forced test message to verify the system works",
                0.5f
            );

            // Generar mensaje
            if (MyStoryModComponent.Instance != null)
            {
                MyStoryModComponent.Instance.StartCoroutine(
                    SpontaneousMessageGenerator.GenerateAndSendMessage(request)
                );
                
                Messages.Message($"Test message queued for {colonist.LabelShort}. Watch for notification!", MessageTypeDefOf.TaskCompletion);
            }
            else
            {
                Messages.Message("ERROR: MyStoryModComponent not available!", MessageTypeDefOf.RejectInput);
            }
        }

        /// <summary>
        /// Simula un incidente para testing
        /// </summary>
        public static void SimulateIncident()
        {
            if (Current.Game == null)
            {
                Messages.Message("Need active game to test!", MessageTypeDefOf.RejectInput);
                return;
            }

            if (!MyMod.Settings.AreIncidentMessagesEnabled())
            {
                Messages.Message("Incident messages are DISABLED! Enable them first.", MessageTypeDefOf.RejectInput);
                return;
            }

            Log.Message("[EchoColony] Simulating TEST RAID incident...");
            
            // Simular un raid
            IncidentTriggers.TriggerSpontaneousMessage(
                IncidentTrigger.Raid,
                "TEST INCIDENT: Simulated raid for testing purposes"
            );

            Messages.Message("Test raid incident triggered! Watch for colonist reactions.", MessageTypeDefOf.TaskCompletion);
        }

        /// <summary>
        /// Resetea todos los cooldowns para testing
        /// </summary>
        public static void ResetAllCooldowns()
        {
            var tracker = SpontaneousMessageTracker.Instance;
            if (tracker == null)
            {
                Messages.Message("Tracker not initialized!", MessageTypeDefOf.RejectInput);
                return;
            }

            if (Current.Game == null)
            {
                Messages.Message("Need active game!", MessageTypeDefOf.RejectInput);
                return;
            }

            int resetCount = 0;
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.FreeColonists)
                {
                    var colonistTracker = tracker.GetTrackerFor(pawn);
                    if (colonistTracker != null)
                    {
                        colonistTracker.messagesToday = 0;
                        colonistTracker.lastMessageTick = 0;
                        colonistTracker.lastTriggerTime.Clear();
                        colonistTracker.hasPendingResponse = false;
                        resetCount++;
                    }
                }
            }

            Messages.Message($"Reset cooldowns for {resetCount} colonists", MessageTypeDefOf.TaskCompletion);
            Log.Message($"[EchoColony] Reset cooldowns for {resetCount} colonists");
        }

        /// <summary>
        /// Lista todos los colonos y su estado de elegibilidad
        /// </summary>
        public static void ListColonistsStatus()
        {
            if (Current.Game == null)
            {
                Messages.Message("Need active game!", MessageTypeDefOf.RejectInput);
                return;
            }

            var tracker = SpontaneousMessageTracker.Instance;
            StringBuilder report = new StringBuilder();
            report.AppendLine("═══════════════════════════════════════════");
            report.AppendLine("COLONISTS STATUS FOR SPONTANEOUS MESSAGES");
            report.AppendLine("═══════════════════════════════════════════");
            report.AppendLine();
            
            // Información del juego actual
            report.AppendLine($"Current Game Ticks: {Find.TickManager.TicksGame}");
            report.AppendLine($"Current Day: {GenDate.DaysPassed}");
            report.AppendLine($"Game Hours Passed: {Find.TickManager.TicksGame / (float)GenDate.TicksPerHour:F1}");
            report.AppendLine();

            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    report.AppendLine($"▶ {pawn.LabelShort}");
                    
                    // Age
                    float age = pawn.ageTracker?.AgeBiologicalYearsFloat ?? 0f;
                    report.AppendLine($"  Age: {age:F1} years {(age < 3f ? "❌ TOO YOUNG" : "✅")}");
                    
                    // Talking
                    var talking = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Talking) ?? 0f;
                    report.AppendLine($"  Talking: {talking:P0} {(talking < 0.3f ? "❌ CANNOT TALK" : "✅")}");
                    
                    // Consciousness
                    var consciousness = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Consciousness) ?? 1f;
                    report.AppendLine($"  Consciousness: {consciousness:P0}");
                    
                    // Settings
                    var settings = MyMod.Settings.GetOrCreateColonistSettings(pawn);
                    report.AppendLine($"  Enabled: {(settings.enabled ? "✅" : "❌ DISABLED")}");
                    report.AppendLine($"  Max/Day: {settings.maxMessagesPerDay}");
                    report.AppendLine($"  Cooldown: {settings.cooldownHours}h");
                    
                    // Tracker info
                    if (tracker != null)
                    {
                        var colonistTracker = tracker.GetTrackerFor(pawn);
                        if (colonistTracker != null)
                        {
                            report.AppendLine($"  Messages today: {colonistTracker.messagesToday}/{settings.maxMessagesPerDay}");
                            
                            // Información detallada de ticks
                            report.AppendLine($"  Last message tick: {colonistTracker.lastMessageTick}");
                            report.AppendLine($"  Current tick: {Find.TickManager.TicksGame}");
                            report.AppendLine($"  Ticks since last: {Find.TickManager.TicksGame - colonistTracker.lastMessageTick}");
                            
                            int hoursSinceLast = (Find.TickManager.TicksGame - colonistTracker.lastMessageTick) / GenDate.TicksPerHour;
                            report.AppendLine($"  Hours since last: {hoursSinceLast}h (need {settings.cooldownHours}h)");
                            
                            // Diagnóstico de por qué no puede enviar
                            bool canSend = tracker.CanSendMessage(pawn, TriggerType.Random);
                            report.AppendLine($"  Can send: {(canSend ? "✅ YES" : "❌ NO")}");
                            
                            if (!canSend)
                            {
                                report.AppendLine("  WHY NOT:");
                                
                                // Check daily limit
                                if (colonistTracker.messagesToday >= settings.maxMessagesPerDay)
                                {
                                    report.AppendLine($"    ❌ Daily limit reached ({colonistTracker.messagesToday}/{settings.maxMessagesPerDay})");
                                }
                                
                                // Check cooldown
                                float hoursSinceLastFloat = (Find.TickManager.TicksGame - colonistTracker.lastMessageTick) / (float)GenDate.TicksPerHour;
                                if (hoursSinceLastFloat < settings.cooldownHours)
                                {
                                    float hoursRemaining = settings.cooldownHours - hoursSinceLastFloat;
                                    report.AppendLine($"    ❌ On cooldown ({hoursSinceLastFloat:F1}h passed, need {settings.cooldownHours}h)");
                                    report.AppendLine($"       → {hoursRemaining:F1}h remaining");
                                }
                            }
                        }
                    }
                    
                    report.AppendLine();
                }
            }

            report.AppendLine("═══════════════════════════════════════════");
            report.AppendLine();
            report.AppendLine("💡 TIP: If colonists show 'On cooldown' but you never saw messages,");
            report.AppendLine("        use 'Reset All Cooldowns' button to fix initialization bug.");
            report.AppendLine("═══════════════════════════════════════════");
            
            Log.Message(report.ToString());
            Messages.Message("Colonists status printed to log", MessageTypeDefOf.TaskCompletion);
        }
    }
}