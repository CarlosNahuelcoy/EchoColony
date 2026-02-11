using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private static System.Random _random;
        
        private static System.Random Random
        {
            get
            {
                if (_random == null)
                    _random = new System.Random();
                return _random;
            }
        }

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

        public static void GenerateSpontaneousMessage(MessageTriggerType triggerType, bool isTest = false)
        {
            if (Find.Storyteller?.def == null) return;

            // ⚠️ CRÍTICO: CAPTURAR TODO EN EL HILO PRINCIPAL ANTES DE Task.Run()
            StorytellerDef storyteller;
            string storytellerDefName;
            string colonyContext = "";
            string fullPrompt = "";

            try
            {
                storyteller = Find.Storyteller.def;
                storytellerDefName = storyteller.defName;
                
                // CAPTURAR contexto de colonia AQUÍ (hilo principal)
                try
                {
                    colonyContext = GetColonyStatusContext();
                }
                catch (Exception contextEx)
                {
                    if (MyMod.Settings?.debugMode == true)
                        Log.Warning($"[EchoColony] Error getting colony context: {contextEx.Message}");
                }

                // Construir contexto según el tipo de trigger
                string messageContext = triggerType == MessageTriggerType.Random 
                    ? BuildRandomMessageContext(colonyContext)
                    : BuildIncidentMessageContext(colonyContext);
                
                // Construir prompt completo AQUÍ (hilo principal)
                fullPrompt = StorytellerPromptBuilder.BuildContext(Find.Storyteller, messageContext);
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error preparing storyteller message context: {ex.Message}");
                return;
            }

            // ✅ AHORA SÍ Task.Run() con datos ya capturados
            Task.Run(async () =>
            {
                try
                {
                    // Generar mensaje con IA (ya no accede a nada de RimWorld)
                    string message = await GenerateAIMessage(fullPrompt);

                    if (!string.IsNullOrEmpty(message))
                    {
                        // Volver al hilo principal para mostrar UI
                        LongEventHandler.ExecuteWhenFinished(() =>
                        {
                            ShowStorytellerMessage(message, storytellerDefName, colonyContext, isTest);
                        });

                        if (MyMod.Settings?.debugMode == true)
                        {
                            Log.Message($"[EchoColony] Generated storyteller message ({triggerType}): {message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[EchoColony] Failed to generate storyteller message: {ex.Message}");
                }
            });
        }

        private static string BuildRandomMessageContext(string colonyContext)
        {
            var contextParts = new List<string>();
            contextParts.Add("Make a brief, spontaneous observation about the colony.");
            contextParts.Add("Keep it short (1-2 sentences) and in character.");
            
            if (!string.IsNullOrEmpty(colonyContext))
            {
                contextParts.Add($"Colony status: {colonyContext}");
            }
            
            return string.Join(" ", contextParts);
        }

        private static string BuildIncidentMessageContext(string colonyContext)
        {
            var contextParts = new List<string>();
            contextParts.Add("React briefly to an event that just occurred in the colony.");
            contextParts.Add("Keep it short (1-2 sentences) and in character.");
            
            if (!string.IsNullOrEmpty(colonyContext))
            {
                contextParts.Add($"Colony status: {colonyContext}");
            }
            
            return string.Join(" ", contextParts);
        }

        private static string GetColonyStatusContext()
        {
            try
            {
                Map map = Find.CurrentMap;
                if (map == null) return "";

                var sb = new System.Text.StringBuilder();
                
                // ✅ CAPTURAR todo en variables locales INMEDIATAMENTE
                var colonists = map.mapPawns.FreeColonists.ToList();
                int colonistCount = colonists.Count;
                int injured = colonists.Count(p => p.health.HasHediffsNeedingTend());
                int mental = colonists.Count(p => p.MentalStateDef != null);
                float temperature = map.mapTemperature.OutdoorTemp;
                int hostiles = map.attackTargetsCache.TargetsHostileToColony.Count();
                var conditions = map.GameConditionManager.ActiveConditions.Select(c => c.def.label).ToList();

                // Construir string con datos capturados
                sb.Append($"{colonistCount} colonists");
                
                if (injured > 0) sb.Append($", {injured} injured");
                if (mental > 0) sb.Append($", {mental} in mental break");
                
                sb.Append(". ");
                sb.Append($"Temperature: {temperature:F0}°C. ");

                if (hostiles > 0)
                {
                    sb.Append($"{hostiles} hostiles active. ");
                }

                if (conditions.Any())
                {
                    sb.Append($"Active conditions: {string.Join(", ", conditions)}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                if (MyMod.Settings?.debugMode == true)
                    Log.Warning($"[EchoColony] Error building colony context: {ex.Message}");
                return "";
            }
        }

        private static async Task<string> GenerateAIMessage(string promptContext)
        {
            var tcs = new TaskCompletionSource<string>();
            
            System.Collections.IEnumerator coroutine = null;
            
            // Para Player2, construir JSON apropiado
            if (MyMod.Settings.modelSource == ModelSource.Player2)
            {
                // Construir JSON con formato de mensajes para Player2
                var jsonPayload = new SimpleJSON.JSONObject();
                var jsonMessages = new SimpleJSON.JSONArray();
                
                // System message con el contexto
                var systemMsg = new SimpleJSON.JSONObject();
                systemMsg["role"] = "system";
                systemMsg["content"] = promptContext;
                jsonMessages.Add(systemMsg);
                
                // User message pidiendo el comentario
                var userMsg = new SimpleJSON.JSONObject();
                userMsg["role"] = "user";
                userMsg["content"] = "Make a brief spontaneous comment about the colony (1-2 sentences).";
                jsonMessages.Add(userMsg);
                
                jsonPayload["messages"] = jsonMessages;
                string jsonPrompt = jsonPayload.ToString();
                
                if (MyMod.Settings?.debugMode == true)
                {
                    Log.Message($"[EchoColony] Player2 JSON prompt: {jsonPrompt.Length} chars");
                }
                
                coroutine = GeminiAPI.SendRequestToPlayer2Storyteller(jsonPrompt, (response) => {
                    tcs.SetResult(response);
                });
            }
            else if (MyMod.Settings.modelSource == ModelSource.Gemini)
            {
                coroutine = GeminiAPI.SendRequestToGemini(promptContext, (response) => {
                    tcs.SetResult(response);
                });
            }
            else if (MyMod.Settings.modelSource == ModelSource.OpenRouter)
            {
                coroutine = GeminiAPI.SendRequestToOpenRouter(promptContext, (response) => {
                    tcs.SetResult(response);
                });
            }
            else if (MyMod.Settings.modelSource == ModelSource.Local)
            {
                coroutine = GeminiAPI.SendRequestToLocalModel(promptContext, (response) => {
                    tcs.SetResult(response);
                });
            }

            if (coroutine != null && MyStoryModComponent.Instance != null)
            {
                MyStoryModComponent.Instance.StartCoroutine(coroutine);
            }
            else
            {
                tcs.SetResult("");
            }

            return await tcs.Task;
        }

        private static void ShowStorytellerMessage(string message, string storytellerDefName, string colonyContext, bool isTest)
        {
            try
            {
                // Agregar mensaje al historial del chat
                var chatData = Current.Game.GetComponent<StorytellerChatData>();
                if (chatData != null)
                {
                    chatData.AddMessage(storytellerDefName, "[STORYTELLER] " + message);
                }

                // Mostrar ventana personalizada con auto-close
                Find.WindowStack.Add(new StorytellerMessageDialog(message, storytellerDefName, isTest));

                if (MyMod.Settings?.debugMode == true)
                {
                    Log.Message($"[EchoColony] Storyteller message shown: {message}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Error showing storyteller message: {ex.Message}");
            }
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
            
            if (Find.TickManager != null)
            {
                status.AppendLine($"Current Tick: {Find.TickManager.TicksGame}");
                status.AppendLine($"Minutes Until Next: {MinutesUntilNextMessage}");
            }
            
            return status.ToString();
        }
    }
}