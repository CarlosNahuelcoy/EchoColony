using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;

namespace EchoColony
{
    /// <summary>
    /// Sistema de mensajes espontáneos del Storyteller
    /// Basado en RandomCommentSystem del Narrator's Voice pero integrado con EchoColony
    /// </summary>
    public static class StorytellerSpontaneousMessageSystem
    {
        private static int lastMessageTick = 0;
        private static bool isActive = false;

        public enum MessageTriggerType
        {
            Random,
            Incident
        }

        public static void StartSystem()
        {
            try
            {
                isActive = true;
                lastMessageTick = 0;
                Log.Message("[EchoColony] Storyteller spontaneous message system activated");
            }
            catch (Exception ex)
            {
                isActive = false;
                Log.Error($"[EchoColony] StorytellerSpontaneousMessageSystem failed: {ex.Message}");
            }
        }

        public static void StopSystem()
        {
            isActive = false;
            Log.Message("[EchoColony] Storyteller spontaneous message system deactivated");
        }

        public static void Update()
        {
            if (!isActive || !MyMod.Settings.IsStorytellerMessagesActive())
                return;

            // Solo procesar mensajes aleatorios si están habilitados
            if (!MyMod.Settings.AreStorytellerRandomMessagesEnabled())
                return;

            // Inicializar lastMessageTick si es necesario
            if (lastMessageTick == 0 && Find.TickManager != null)
            {
                lastMessageTick = Find.TickManager.TicksGame;
            }

            if (Current.Game == null || Find.TickManager == null || Find.Storyteller == null)
                return;

            int currentTick = Find.TickManager.TicksGame;
            int ticksSinceLastMessage = currentTick - lastMessageTick;

            // Convertir intervalo de minutos a ticks
            float intervalMinutes = MyMod.Settings?.storytellerRandomIntervalMinutes ?? 30f;
            int intervalTicks = (int)(intervalMinutes * 60f * 60f);

            if (ticksSinceLastMessage >= intervalTicks)
            {
                // Agregar algo de aleatoriedad (±25%)
                float randomFactor = UnityEngine.Random.Range(0.75f, 1.25f);
                if (ticksSinceLastMessage >= intervalTicks * randomFactor)
                {
                    GenerateSpontaneousMessage(MessageTriggerType.Random);
                    lastMessageTick = currentTick;
                }
            }
        }

        /// <summary>
        /// Pide un comentario al storyteller. La generación ocurre en una corrutina del
        /// hilo principal (StorytellerCommentService), con cooldown y una petición a la vez.
        /// </summary>
        public static void GenerateSpontaneousMessage(MessageTriggerType triggerType, bool isTest = false,
            string situation = null, string eventKey = null, bool priority = false)
        {
            StorytellerCommentKind kind = isTest ? StorytellerCommentKind.Test
                : triggerType == MessageTriggerType.Incident ? StorytellerCommentKind.Event
                : StorytellerCommentKind.Random;

            StorytellerCommentService.Request(kind, situation, eventKey, priority);
        }

        public static bool IsActive => isActive;

        public static int MinutesUntilNextMessage
        {
            get
            {
                if (!isActive || Find.TickManager == null) return 0;

                int currentTick = Find.TickManager.TicksGame;
                int ticksSinceLastMessage = currentTick - lastMessageTick;
                float intervalMinutes = MyMod.Settings?.storytellerRandomIntervalMinutes ?? 30f;
                int intervalTicks = (int)(intervalMinutes * 60f * 60f);
                int ticksRemaining = Math.Max(0, intervalTicks - ticksSinceLastMessage);

                return (int)(ticksRemaining / (60f * 60f));
            }
        }

        public static string GetDebugStatus()
        {
            var status = new System.Text.StringBuilder();
            status.AppendLine($"Storyteller Messages Enabled: {MyMod.Settings?.IsStorytellerMessagesActive()}");
            status.AppendLine($"System Active: {isActive}");
            status.AppendLine($"Random Messages: {MyMod.Settings?.AreStorytellerRandomMessagesEnabled()}");
            status.AppendLine($"Incident Messages: {MyMod.Settings?.AreStorytellerIncidentMessagesEnabled()}");
            status.AppendLine($"TickManager Available: {Find.TickManager != null}");
            status.AppendLine($"Current Game: {Current.Game != null}");
            status.AppendLine($"Last Message Tick: {lastMessageTick}");
            status.AppendLine($"Comment In Progress: {StorytellerCommentService.IsBusy}");
            
            if (Find.TickManager != null)
            {
                status.AppendLine($"Current Tick: {Find.TickManager.TicksGame}");
                status.AppendLine($"Minutes Until Next: {MinutesUntilNextMessage}");
            }
            
            return status.ToString();
        }
    }
}