using System.Text;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using System;

namespace EchoColony
{
    public static class GroupPromptContextBuilder
    {
        public static string Build(Pawn speaker, List<Pawn> group, List<string> recentHistory, string userMessage, bool isFirstResponse)
        {
            var sb = new StringBuilder();
            
            // ✅ ANÁLISIS TEMPRANO para guiar todo el prompt
            var context = AnalyzeConversationContext(speaker, group, recentHistory, userMessage, isFirstResponse);
            
            // Idioma solo si no es inglés
            string idioma = Prefs.LangFolderName?.ToLower() ?? "english";
            if (idioma != "english") 
                sb.AppendLine($"*Language:* {idioma}");

            // ✅ CONTEXTO GRUPAL CLARO
            sb.AppendLine($"*Group chat:* You are {speaker.LabelShort} with {string.Join(", ", group.Where(p => p != speaker).Select(p => p.LabelShort))}. Everyone can hear each other.");

            // Sistema base del colono
            sb.AppendLine(ColonistPromptContextBuilder.BuildSystemPromptPublic(speaker));

            // Prompts personalizados
            AppendCustomPrompts(sb, speaker);

            // ✅ CONTEXTO COMPLETO DEL COLONO (ahora incluido porque es sistema separado)
            sb.AppendLine(BuildGroupSpecificContext(speaker, group));

            // ✅ SISTEMA DE IDEOLOGÍA (importante para grupos también)
            sb.AppendLine(BuildIdeologyInfo(speaker));

            // ✅ SISTEMA DE MEMORIAS UNIFICADO (conecta ambos chats)
            AppendUnifiedMemories(sb, speaker);

            // ✅ CONTEXTO CONVERSACIONAL INTELIGENTE
            AppendConversationContext(sb, context, recentHistory, userMessage, isFirstResponse);

            // ✅ GUÍAS DINÁMICAS PARA GRUPO
            sb.AppendLine(BuildGroupSpecificGuidelines(context, speaker, group));

            return sb.ToString().Trim();
        }

        // ✅ NUEVO: Solo agregar prompts si existen
        private static void AppendCustomPrompts(StringBuilder sb, Pawn speaker)
        {
            string globalPrompt = MyMod.Settings?.globalPrompt ?? "";
            string customPrompt = ColonistPromptManager.GetPrompt(speaker);
            
            if (!string.IsNullOrWhiteSpace(globalPrompt)) 
                sb.AppendLine("*Global guidance:* " + globalPrompt.Trim());
            
            if (!string.IsNullOrWhiteSpace(customPrompt))
                sb.AppendLine("*Personal instructions:* " + customPrompt.Trim());
        }

        // ✅ NUEVO: Contexto completo pero optimizado para grupos
        private static string BuildGroupSpecificContext(Pawn speaker, List<Pawn> group)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Your Character Context:");
            
            // Demografía esencial
            int age = speaker.ageTracker?.AgeBiologicalYears ?? 0;
            string gender = speaker.gender.ToString();
            sb.AppendLine($"*Age & Gender:* {age}y, {gender}");
            
            // Instrucciones de edad (compactas para grupos)
            string ageGuidance = GetCompactAgeGuidance(age);
            if (!string.IsNullOrEmpty(ageGuidance))
                sb.AppendLine($"*Age behavior:* {ageGuidance}");

            // Backstory
            var childhood = speaker.story.AllBackstories.FirstOrDefault(b => b.slot == BackstorySlot.Childhood);
            var adulthood = speaker.story.AllBackstories.FirstOrDefault(b => b.slot == BackstorySlot.Adulthood);
            sb.AppendLine($"*Background:* {childhood?.title.CapitalizeFirst() ?? "Unknown"}, {adulthood?.title.CapitalizeFirst() ?? "Unknown"}");

            // Estado físico/mental
            float health = speaker.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
            string healthDesc = GetHealthDescription(health);
            sb.AppendLine($"*Health:* {healthDesc}");

            string mentalState = speaker.MentalState?.def.label ?? "stable";
            float mood = speaker.needs?.mood?.CurInstantLevel ?? 1f;
            string moodDesc = GetMoodDescription(mood);
            sb.AppendLine($"*Mental state:* {mentalState}, *Mood:* {moodDesc}");

            // Traits relevantes
            var traits = speaker.story?.traits?.allTraits?.Select(t => t.LabelCap).ToList() ?? new List<string>();
            if (traits.Any())
                sb.AppendLine($"*Traits:* {string.Join(", ", traits.Take(4))}"); // Límite para no saturar

            // Habilidades notables (solo las más relevantes para conversación)
            var notableSkills = GetNotableSkills(speaker);
            if (notableSkills.Any())
                sb.AppendLine($"*Notable skills:* {string.Join(", ", notableSkills)}");

            // Pensamientos significativos que afectan humor
            var significantThoughts = GetSignificantThoughts(speaker);
            if (significantThoughts.Any())
                sb.AppendLine($"*Current mood factors:* {string.Join(", ", significantThoughts)}");

            // Relaciones con participantes del grupo
            var groupRelations = GetDetailedGroupRelations(speaker, group);
            if (groupRelations.Any())
                sb.AppendLine($"*Relations with group:* {string.Join("; ", groupRelations)}");

            // Ubicación actual
            var room = speaker.GetRoom();
            string location = room?.Role?.label ?? "outside";
            bool isOwnRoom = speaker.ownership?.OwnedBed?.GetRoom() == room;
            string privacy = isOwnRoom ? " (your private room)" : "";
            sb.AppendLine($"*Location:* {location}{privacy}");

            if (speaker.jobs?.curDriver != null)
{
    string report = speaker.jobs.curDriver.GetReport();
    if (!string.IsNullOrWhiteSpace(report))
        sb.AppendLine($"*Current activity:* {report}");
}
else
{
    sb.AppendLine("*Current activity:* idle or resting");
}

            // Estado de amenazas (importante para contexto)
            string threatInfo = GetCriticalThreatInfo(speaker);
            if (!string.IsNullOrEmpty(threatInfo))
                sb.AppendLine(threatInfo);

            return sb.ToString();
        }

        // ✅ SISTEMA DE IDEOLOGÍA para grupos
        private static string BuildIdeologyInfo(Pawn pawn)
{
    if (!ModsConfig.IdeologyActive || pawn.Ideo == null)
        return "*Sacred Beliefs:* No specific ideology";

    var ideo = pawn.Ideo;
    
    // Usar el mismo sistema de categorización apasionada
    var beliefCategories = CategorizeAllPrecepts(ideo.PreceptsListForReading);
    var compactBeliefs = new List<string>();
    
    // Agregar categorías con MÁXIMA CONVICCIÓN para grupos
    if (beliefCategories.ContainsKey("Cannibalism") && beliefCategories["Cannibalism"].Any())
        compactBeliefs.Add("FLESH-EATING: " + string.Join(" AND ", beliefCategories["Cannibalism"]));
        
    if (beliefCategories.ContainsKey("Violence") && beliefCategories["Violence"].Any())
        compactBeliefs.Add("VIOLENCE: " + string.Join(" AND ", beliefCategories["Violence"]));
        
    if (beliefCategories.ContainsKey("Social") && beliefCategories["Social"].Any())
        compactBeliefs.Add("SOCIAL: " + string.Join(" AND ", beliefCategories["Social"]));
        
    if (beliefCategories.ContainsKey("Death") && beliefCategories["Death"].Any())
        compactBeliefs.Add("DEATH: " + string.Join(" AND ", beliefCategories["Death"]));
        
    if (beliefCategories.ContainsKey("Other") && beliefCategories["Other"].Any())
        compactBeliefs.Add("ETHICS: " + string.Join(" AND ", beliefCategories["Other"]));

    string role = pawn.Ideo?.GetRole(pawn)?.def.label;
    string roleText = role != null ? $" (Religious role: {role})" : "";
    
    string beliefs = compactBeliefs.Any() ? 
        string.Join("; ", compactBeliefs.Take(2)) : // Reducido a 2 para grupos
        "standard beliefs";
    
    // ✅ INSTRUCCIÓN ESPECÍFICA PARA GRUPOS
    string groupConvictionInstruction = "\n*GROUP CONTEXT: Express your religious beliefs with confidence in this group setting. If others share your ideology, bond over shared values. If they don't, stand firm in your convictions without backing down.*";
    
    return $"*Your Sacred Ideology:* {ideo.name} - {beliefs}{roleText}{groupConvictionInstruction}";
}

// ✅ AGREGAR ESTOS MÉTODOS DE SOPORTE AL GroupPromptContextBuilder:

private static Dictionary<string, List<string>> CategorizeAllPrecepts(List<Precept> precepts)
{
    var categories = new Dictionary<string, List<string>>
    {
        {"Violence", new List<string>()},
        {"Cannibalism", new List<string>()},
        {"Social", new List<string>()},
        {"Death", new List<string>()},
        {"Other", new List<string>()}
    };
    
    foreach (var precept in precepts)
    {
        if (precept?.def?.defName == null) continue;
        
        string impact = GetPassionatePreceptImpact(precept);
        if (string.IsNullOrEmpty(impact)) continue;
        
        string defName = precept.def.defName.ToLower();
        
        if (defName.Contains("violence") || defName.Contains("execution") || defName.Contains("kill"))
            categories["Violence"].Add(impact);
        else if (defName.Contains("cannibal") || defName.Contains("flesh"))
            categories["Cannibalism"].Add(impact);
        else if (defName.Contains("slavery") || defName.Contains("diversity") || defName.Contains("love") || 
                 defName.Contains("nudity") || defName.Contains("marriage"))
            categories["Social"].Add(impact);
        else if (defName.Contains("corpse") || defName.Contains("burial") || defName.Contains("death"))
            categories["Death"].Add(impact);
        else
            categories["Other"].Add(impact);
    }
    
    return categories;
}

        // ✅ VERSIÓN APASIONADA ESPECÍFICA PARA GRUPOS (más compacta pero igual de intensa)
        private static string GetPassionatePreceptImpact(Precept precept)
        {
            if (precept?.def?.defName == null) return "";

            string defName = precept.def.defName;

            // ✅ CANIBALISMO - MÁS COMPACTO PARA GRUPOS
            if (defName.Contains("Cannibalism"))
            {
                if (defName.Contains("Preferred"))
                    return "ADORES sacred human flesh";
                if (defName.Contains("Acceptable"))
                    return "happily eats human flesh";
                if (defName.Contains("Disapproved"))
                    return "dislikes cannibalism";
                if (defName.Contains("Abhorrent"))
                    return "DESPISES flesh-eating";
            }

            // ✅ VIOLENCIA 
            if (defName.Contains("Violence"))
            {
                if (defName.Contains("Pacifist"))
                    return "ABHORS all violence";
                if (defName.Contains("Preferred"))
                    return "LOVES combat";
                if (defName.Contains("Acceptable"))
                    return "accepts violence";
                if (defName.Contains("Disapproved"))
                    return "dislikes violence";
            }

            // ✅ EJECUCIONES
            if (defName.Contains("Execution"))
            {
                if (defName.Contains("Respected") || defName.Contains("Required"))
                    return "DEMANDS righteous executions";
                if (defName.Contains("Acceptable"))
                    return "supports executions";
                if (defName.Contains("Abhorrent"))
                    return "ABHORS executions";
            }

            // ✅ ESCLAVITUD
            if (defName.Contains("Slavery"))
            {
                if (defName.Contains("Honorable") || defName.Contains("Acceptable"))
                    return "believes slavery is HONORABLE";
                if (defName.Contains("Abhorrent"))
                    return "ABHORS slavery";
            }

            // ✅ SUPREMACISMO
            if (defName.Contains("Diversity"))
            {
                if (defName.Contains("Supremacist"))
                    return "believes in genetic SUPERIORITY";
                if (defName.Contains("Racist"))
                    return "holds racial prejudices";
                if (defName.Contains("Intense"))
                    return "FANATICAL about diversity";
            }

            // ✅ NUDISMO
            if (defName.Contains("Nudity"))
            {
                if (defName.Contains("Preferred"))
                    return "LOVES nudity";
                if (defName.Contains("Disapproved"))
                    return "DEMANDS clothing";
            }

            // ✅ DROGAS
            if (defName.Contains("DrugUse"))
            {
                if (defName.Contains("Prohibited"))
                    return "CONDEMNS all drugs";
                if (defName.Contains("MedicalOnly"))
                    return "medical drugs only";
                if (defName.Contains("Acceptable"))
                    return "enjoys recreational drugs";
            }

            // ✅ TECNOLOGÍA
            if (defName.Contains("Technology"))
            {
                if (defName.Contains("Preferred"))
                    return "WORSHIPS technology";
                if (defName.Contains("Disapproved"))
                    return "DISTRUSTS technology";
            }

            // ✅ ALIMENTACIÓN
            if (defName.Contains("MeatEating"))
            {
                if (defName.Contains("Vegetarian"))
                    return "REFUSES to eat meat";
                if (defName.Contains("Carnivore"))
                    return "CRAVES meat only";
            }

            // ✅ CADÁVERES
            if (defName.Contains("Corpse"))
            {
                if (defName.Contains("DontCare"))
                    return "unbothered by corpses";
                if (defName.Contains("Disapproved"))
                    return "DISTURBED by corpses";
            }

            // ✅ ÓRGANOS
            if (defName.Contains("Organ"))
            {
                if (defName.Contains("Acceptable"))
                    return "supports organ harvesting";
                if (defName.Contains("Abhorrent"))
                    return "ABHORS organ harvesting";
            }

            // ✅ TRABAJO
            if (defName.Contains("Work"))
            {
                if (defName.Contains("Hard"))
                    return "VALUES hard work";
                if (defName.Contains("Lazy"))
                    return "prefers leisure";
            }

            // ✅ NIÑOS
            if (defName.Contains("Child"))
            {
                if (defName.Contains("Encouraged"))
                    return "supports child labor";
                if (defName.Contains("Abhorrent"))
                    return "PROTECTS children";
            }

            return "";
        }

        // ✅ MEMORIAS UNIFICADAS (el puente entre chat individual y grupal)
        private static void AppendUnifiedMemories(StringBuilder sb, Pawn speaker)
        {
            if (MyStoryModComponent.Instance?.ColonistMemoryManager == null)
                return;

            var memoryTracker = MyStoryModComponent.Instance.ColonistMemoryManager.GetTrackerFor(speaker);
            var recentMemories = memoryTracker?.GetLastMemories(4); // Reducido para grupos
            
            if (recentMemories?.Any() == true)
            {
                sb.AppendLine("# Your Recent Memories (from both individual and group conversations):");
                foreach (var memory in recentMemories)
                {
                    // Identificar tipo de conversación
                    string prefix = memory.StartsWith("[Conversación grupal") ? "👥 Group:" : "💬 Private:";
                    
                    // Acortar memoria si es muy larga para grupos
                    string shortMem = memory.Length > 120 ? memory.Substring(0, 120) + "..." : memory;
                    sb.AppendLine($"{prefix} {shortMem}");
                }
                sb.AppendLine("*Note: These memories help you maintain consistency across all conversations.*");
            }
        }

        // ✅ MÉTODOS DE SOPORTE COMPACTOS
        private static string GetCompactAgeGuidance(int age)
        {
            if (age <= 1) return "Baby: crying, cooing sounds only, no words";
            if (age <= 3) return "Toddler: 1-4 words, very expressive";
            if (age <= 6) return "Child: simple sentences, asks many questions";
            if (age <= 10) return "Kid: enthusiastic, shares knowledge";
            if (age <= 13) return "Pre-teen: mood swings, fairness-focused";
            if (age <= 17) return "Teen: emotional intensity, peer-focused";
            if (age <= 25) return "Young adult: learning independence";
            return "";
        }

        private static string GetHealthDescription(float health)
        {
            if (health >= 0.95f) return "perfectly fine";
            if (health >= 0.75f) return "mostly okay";
            if (health >= 0.5f) return "injured";
            if (health >= 0.3f) return "seriously wounded";
            return "critical condition";
        }

        private static string GetMoodDescription(float mood)
        {
            if (mood >= 0.9f) return "great";
            if (mood >= 0.7f) return "good";
            if (mood >= 0.5f) return "okay";
            if (mood >= 0.3f) return "upset";
            return "struggling";
        }

        private static List<string> GetNotableSkills(Pawn speaker)
        {
            if (speaker.skills == null) return new List<string>();

            return speaker.skills.skills
                .Where(s => s.Level >= 10 || s.passion != Passion.None)
                .OrderByDescending(s => s.Level)
                .Take(3) // Solo top 3 para grupos
                .Select(s => {
                    string level = s.Level >= 15 ? "expert" : s.Level >= 10 ? "skilled" : "decent";
                    string passion = s.passion == Passion.Major ? "❤️" : s.passion == Passion.Minor ? "💛" : "";
                    return $"{s.def.label} ({level}){passion}";
                })
                .ToList();
        }

        private static List<string> GetSignificantThoughts(Pawn speaker)
        {
            var memories = speaker.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null) return new List<string>();

            return memories
                .Where(t => t.VisibleInNeedsTab && Math.Abs(t.MoodOffset()) >= 8f) // Solo muy significativos
                .Take(3) // Máximo 3 para grupos
                .Select(t => $"{t.LabelCap} ({(t.MoodOffset() > 0 ? "+" : "")}{t.MoodOffset():F0})")
                .ToList();
        }

        private static List<string> GetDetailedGroupRelations(Pawn speaker, List<Pawn> group)
        {
            var relations = new List<string>();

            foreach (var other in group.Where(p => p != speaker).Take(5)) // Límite para grupos grandes
            {
                int opinion = speaker.relations?.OpinionOf(other) ?? 0;
                var relation = speaker.GetRelations(other).FirstOrDefault()?.label;

                // ✅ AGREGAR INFORMACIÓN DE GÉNERO Y EDAD
                string genderStr = other.gender == Gender.Male ? "male"
                                : other.gender == Gender.Female ? "female"
                                : "non-binary";
                int age = other.ageTracker.AgeBiologicalYears;

                string desc = "";
                if (!string.IsNullOrEmpty(relation))
                    desc = relation;
                else if (opinion >= 60) desc = "very close";
                else if (opinion >= 30) desc = "friendly";
                else if (opinion <= -60) desc = "despise";
                else if (opinion <= -30) desc = "dislike";
                else if (Math.Abs(opinion) >= 15)
                    desc = opinion > 0 ? "like" : "neutral-cold";

                if (!string.IsNullOrEmpty(desc))
                    relations.Add($"{other.LabelShort} ({desc}, {genderStr}, {age}y)");
            }

            return relations;
        }

        private static string GetCriticalThreatInfo(Pawn pawn)
        {
            // 🎯 VERIFICAR SI DEBE IGNORAR PELIGROS
            if (MyMod.Settings?.ignoreDangersInConversations == true)
            {
                // No incluir ninguna información de amenazas
                return "";
            }

            // Comportamiento normal si no está activada la opción
            var threats = new List<string>();

            string colonyThreat = ThreatAnalyzer.GetColonyThreatStatusDetailed(Find.CurrentMap);
            if (!colonyThreat.Contains("calm and secure"))
                threats.Add("Colony: " + colonyThreat.Split('.')[0]);

            // Verificar específicamente si hay combate REAL
            string combatStatus = ColonistChatWindow.GetPawnCombatStatusDetailed(pawn);
            if (combatStatus.Contains("attacking") || combatStatus.Contains("combat") || combatStatus.Contains("targeted"))
                threats.Add("Combat: " + combatStatus.Split('.')[0]);

            string battleStatus = BattleAnalyzer.GetBattleStatus(Find.CurrentMap);
            if (!battleStatus.Contains("no battle") && battleStatus.Contains("Battle ongoing"))
                threats.Add("Battle: " + battleStatus.Split('.')[0]);

            return threats.Any() ? "*Current threats:* " + string.Join("; ", threats) : "";
        }

        // ✅ CONTEXTO CONVERSACIONAL MEJORADO
        private static void AppendConversationContext(StringBuilder sb, ConversationContext context, 
                                                     List<string> recentHistory, string userMessage, bool isFirstResponse)
        {
            sb.AppendLine("# Current Group Discussion:");
            
            // Input del jugador con contexto apropiado
            if (!string.IsNullOrEmpty(userMessage))
            {
                string prefix = isFirstResponse ? "*Player started discussion:*" : "*Player contributed:*";
                sb.AppendLine($"{prefix} \"{userMessage}\"");
            }

            // Historial reciente optimizado
            if (recentHistory?.Any() == true)
            {
                var importantLines = GetImportantHistoryLines(recentHistory, context);
                
                if (importantLines.Any())
                {
                    sb.AppendLine("*Recent conversation flow:*");
                    foreach (var line in importantLines)
                        sb.AppendLine("→ " + line);
                }
            }

            // Contexto de participación
            if (context.SpeakerParticipation == 0)
                sb.AppendLine("*Note: You haven't spoken yet in this group conversation.*");
            else if (context.SpeakerParticipation >= 3)
                sb.AppendLine("*Note: You've been quite active - consider letting others speak too.*");
        }

        // ✅ FILTRAR LÍNEAS IMPORTANTES DEL HISTORIAL
        private static List<string> GetImportantHistoryLines(List<string> history, ConversationContext context)
        {
            var important = new List<string>();
            var recent = history.TakeLast(6).ToList();
            
            // Siempre incluir las 2 líneas más recientes
            if (recent.Count >= 2)
            {
                important.AddRange(recent.TakeLast(2));
                recent = recent.Take(recent.Count - 2).ToList();
            }
            else
            {
                important.AddRange(recent);
                recent.Clear();
            }
            
            // Agregar líneas especialmente relevantes
            foreach (var line in recent.AsEnumerable().Reverse())
            {
                if (important.Count >= 5) break; // Límite total
                
                if (line.Contains("?") || line.Contains("!") ||
                    line.ToLower().Contains("what") || line.ToLower().Contains("how") ||
                    (!string.IsNullOrEmpty(context.LastSpeaker) && line.StartsWith(context.LastSpeaker)))
                {
                    important.Insert(0, line);
                }
            }
            
            return important.Distinct().ToList();
        }

        // ✅ GUÍAS ESPECÍFICAS PARA GRUPOS
        private static string BuildGroupSpecificGuidelines(ConversationContext context, Pawn speaker, List<Pawn> group)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Group Conversation Guidelines:");

            // Estilo conversacional
            string style = GetGroupConversationStyle(speaker);
            sb.AppendLine($"*Your style:* {style}");

            // Prioridad específica
            string priority = GetPriorityGuidance(context);
            sb.AppendLine($"*Current focus:* {priority}");

            // Reglas básicas
            sb.AppendLine("*Rules:* Natural group conversation, 1-2 sentences, stay in character, reference others by name when appropriate.");

            // 🎯 NUEVA INSTRUCCIÓN: Si debe ignorar peligros
            if (MyMod.Settings?.ignoreDangersInConversations == true)
            {
                sb.AppendLine("*CRITICAL INSTRUCTION:* Do NOT mention or acknowledge any dangers, enemies, threats, raids, or combat. Act as if the colony is always peaceful and safe. Focus only on daily activities, work, relationships, and peaceful topics in this group conversation.");
            }

            // Hints especiales
            var hints = GetGroupContextualHints(context, speaker, group);
            if (hints.Any())
                sb.AppendLine("*Special considerations:* " + string.Join(", ", hints));

            // Instrucciones de roleplay para grupos
            if (MyMod.Settings.enableRoleplayResponses)
                sb.AppendLine("*Actions:* Use <b><i>actions</i></b> sparingly for important group dynamics (nods, gestures, expressions).");

            return sb.ToString();
        }


        private static string GetGroupConversationStyle(Pawn speaker)
        {
            int social = speaker.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            var traits = speaker.story?.traits?.allTraits ?? new List<Trait>();

            // Traits dominan
            foreach (var trait in traits)
            {
                switch (trait.def.defName)
                {
                    case "Abrasive":
                        return "Direct and blunt in groups, may interrupt or be rude";
                    case "Kind":
                        return "Gentle and inclusive, tries to include everyone";
                    case "Psychopath":
                        return "Matter-of-fact, doesn't react emotionally to others";
                    case "Neurotic":
                        return "May talk too much when nervous in groups";
                    case "Beautiful":
                    case "Pretty":
                        return "Confident speaker, others tend to listen";
                }
            }

            // Fallback a social skill
            if (social <= 3)
                return "Shy in groups, speaks briefly when addressed";
            else if (social <= 7)
                return "Participates normally, speaks when has something to say";
            else
                return "Natural group leader, comfortable facilitating discussion";
        }

        private static string GetPriorityGuidance(ConversationContext context)
{
    switch (context.Priority)
    {
        case ConversationPriority.RespondToPlayer:
            return "Acknowledge player input while naturally including others";
        case ConversationPriority.RespondToColonist:
            return $"Build on or respond to what {context.LastSpeaker} said";
        case ConversationPriority.ContinueGroupFlow:
            return "Keep natural group flow, balance all perspectives";
        default:
            return "Contribute naturally to ongoing group discussion";
    }
}

        private static List<string> GetGroupContextualHints(ConversationContext context, Pawn speaker, List<Pawn> group)
        {
            var hints = new List<string>();

            if (context.PlayerResponseRatio > 0.6f)
                hints.Add("group too focused on player - engage with colonists");

            if (group.Count >= 5)
                hints.Add("large group - be concise to let others speak");

            // Hints basados en relaciones
            var hostileCount = group.Count(p => p != speaker && speaker.relations.OpinionOf(p) <= -30);
            if (hostileCount > 0)
                hints.Add("tensions present with some members");

            var friendCount = group.Count(p => p != speaker && speaker.relations.OpinionOf(p) >= 40);
            if (friendCount == group.Count - 1)
                hints.Add("among close friends - be relaxed");

            // 🎯 NO añadir hints sobre amenazas si están desactivadas
            if (MyMod.Settings?.ignoreDangersInConversations != true)
            {
                // Solo añadir hints de peligro si NO está activada la opción de ignorar
                var map = Find.CurrentMap;
                if (map?.attackTargetsCache?.TargetsHostileToColony?.Any() == true)
                {
                    hints.Add("danger present - may affect mood");
                }
            }

            return hints;
        }


        // ✅ ANÁLISIS DE CONTEXTO (sin cambios mayores)
        private static ConversationContext AnalyzeConversationContext(Pawn speaker, List<Pawn> group, 
                                                                     List<string> recentHistory, string userMessage, bool isFirstResponse)
        {
            var context = new ConversationContext
            {
                SpeakerParticipation = 0,
                PlayerResponseRatio = 0f,
                Priority = ConversationPriority.ContinueGroupFlow,
                LastSpeaker = "",
                HasRecentMentions = false
            };
            
            if (recentHistory?.Any() != true)
            {
                context.Priority = isFirstResponse ? ConversationPriority.RespondToPlayer : ConversationPriority.ContinueGroupFlow;
                return context;
            }
            
            context.SpeakerParticipation = recentHistory.Count(line => line.StartsWith(speaker.LabelShort + ":"));
            
            var lastColonistMessage = recentHistory.LastOrDefault(line => 
                !line.StartsWith("You:") && !line.StartsWith("Player:") && line.Contains(":"));
            if (lastColonistMessage != null)
            {
                context.LastSpeaker = lastColonistMessage.Split(':')[0].Trim();
            }
            
            var recentLines = recentHistory.TakeLast(4);
            var playerReferences = recentLines.Count(line => 
                !line.StartsWith("You:") && !line.StartsWith("Player:") &&
                (line.ToLower().Contains("player") || line.ToLower().Contains("you said") || 
                 line.ToLower().Contains("your") || line.ToLower().Contains("what you")));
            
            context.PlayerResponseRatio = (float)playerReferences / Math.Max(recentLines.Count(), 1);
            
            if (isFirstResponse && !string.IsNullOrEmpty(userMessage))
                context.Priority = ConversationPriority.RespondToPlayer;
            else if (context.PlayerResponseRatio > 0.5f)
                context.Priority = ConversationPriority.ContinueGroupFlow;
            else if (!string.IsNullOrEmpty(context.LastSpeaker) && context.LastSpeaker != speaker.LabelShort)
                context.Priority = ConversationPriority.RespondToColonist;
            else
                context.Priority = ConversationPriority.ContinueGroupFlow;
            
            return context;
        }
    }

    // ✅ CLASES DE SOPORTE
    public enum ConversationPriority
    {
        RespondToPlayer,
        RespondToColonist,
        ContinueGroupFlow,
        InitiateNewDirection
    }

    public class ConversationContext
    {
        public ConversationPriority Priority { get; set; }
        public string LastSpeaker { get; set; }
        public int SpeakerParticipation { get; set; }
        public float PlayerResponseRatio { get; set; }
        public bool HasRecentMentions { get; set; }
    }
}