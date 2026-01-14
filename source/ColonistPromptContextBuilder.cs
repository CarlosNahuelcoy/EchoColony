using System.Text;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;
using RimWorld.Planet;
using System;

namespace EchoColony
{
    public static class ColonistPromptContextBuilder
    {
        public static string Build(Pawn pawn, string userMessage)
        {
            if (pawn == null) return string.Empty;

            var sb = new StringBuilder();

            string systemPrompt = BuildSystemPrompt(pawn);
            string context = BuildContext(pawn);
            string memoryRecap = BuildMemoryRecap(pawn);
            string chatHistory = BuildChatHistory(pawn);
            string playerPrompt = BuildPlayerPrompt(userMessage);
            string globalPrompt = MyMod.Settings?.globalPrompt ?? "";
            string customPrompt = ColonistPromptManager.GetPrompt(pawn);

            sb.AppendLine(systemPrompt);
            if (!string.IsNullOrWhiteSpace(globalPrompt))
                sb.AppendLine(globalPrompt.Trim());

            if (!string.IsNullOrWhiteSpace(customPrompt))
            {
                sb.AppendLine($"# Custom Instructions for {pawn.LabelShort}:");
                sb.AppendLine(customPrompt.Trim());
            }

            sb.AppendLine(context);
            sb.AppendLine(BuildIdeologyInfo(pawn));
            sb.AppendLine(memoryRecap);
            sb.AppendLine(chatHistory);
            sb.AppendLine(playerPrompt);

            return sb.ToString();
        }

        public static string BuildSystemPrompt(Pawn pawn)
        {
            string name = pawn.LabelShort;
            string gender = pawn.gender.ToString();
            string xenotype = pawn.genes?.Xenotype?.label ?? "baseline human";
            string faction = Faction.OfPlayer?.Name ?? "unknown faction";
            string settlement = Find.CurrentMap.info?.parent?.LabelCap ?? "unknown settlement";

            return $"You are {name}, a colonist in RimWorld. You identify as {gender} ({xenotype}). You belong to the faction '{faction}' and live in the settlement '{settlement}'. Speak from your perspective and stay in character.";
        }

        public static string BuildContext(Pawn pawn)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Colonist Context");
            sb.AppendLine(BuildOptimizedDemographics(pawn));
            sb.AppendLine(BuildBackstory(pawn));
            sb.AppendLine(BuildTraits(pawn));
            sb.AppendLine(BuildHealthInfo(pawn));
            sb.AppendLine(BuildOptimizedHealthDetails(pawn));
            sb.AppendLine(BuildMoodInfo(pawn));
            sb.AppendLine(BuildOptimizedThoughts(pawn));
            var griefStatus = BuildGriefStatus(pawn);
            if (!string.IsNullOrWhiteSpace(griefStatus))
                sb.AppendLine(griefStatus);
            sb.AppendLine(BuildOptimizedInventory(pawn));
            sb.AppendLine(BuildOptimizedSkills(pawn));
            sb.AppendLine(BuildOptimizedRelationships(pawn));
            sb.AppendLine(BuildLocationInfo(pawn));
            sb.AppendLine(BuildOptimizedEventSummary(pawn));
            sb.AppendLine(BuildOptimizedEnvironmentInfo(pawn));
            sb.AppendLine(BuildOptimizedMetaInstructions(pawn));
            
            // Combinar información de estado crítico
            string threatInfo = GetCriticalThreatInfo(pawn);
            if (!string.IsNullOrEmpty(threatInfo))
                sb.AppendLine(threatInfo);
            
            return sb.ToString();
        }

        // ✅ OPTIMIZADO: Combinar toda la información de amenazas en una línea
        // Reemplazar el método GetCriticalThreatInfo con esta versión:

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

            string combatStatus = ColonistChatWindow.GetPawnCombatStatusDetailed(pawn);
            if (!combatStatus.Contains("not in combat"))
                threats.Add("Combat: " + combatStatus.Split('.')[0]);

            string battleStatus = BattleAnalyzer.GetBattleStatus(Find.CurrentMap);
            if (!battleStatus.Contains("no battle"))
                threats.Add("Battle: " + battleStatus.Split('.')[0]);

            return threats.Any() ? "*Current threats:* " + string.Join("; ", threats) : "";
        }


        // ✅ OPTIMIZADO: Demografia simplificada pero manteniendo funcionalidad
        private static string BuildOptimizedDemographics(Pawn pawn)
        {
            int age = pawn.ageTracker?.AgeBiologicalYears ?? 0;
            string gender = pawn.gender.ToString();

            if (ColonistPromptManager.GetIgnoreAge(pawn))
                return $"*Age:* {age}y ({gender}) - unrestricted behavior";

            var sb = new StringBuilder();
            sb.AppendLine($"*Age:* {age}y ({gender})");
            
            // Compactar instrucciones de edad
            string ageGuidance = GetCompactAgeGuidance(age);
            if (!string.IsNullOrEmpty(ageGuidance))
                sb.AppendLine($"*Age behavior:* {ageGuidance}");
                
            return sb.ToString();
        }

        // ✅ OPTIMIZADO: Instrucciones de edad más compactas (C# 7.3 compatible)
       private static string GetCompactAgeGuidance(int age)
{
    if (age <= 1)
        return "Baby sounds only - crying, cooing, 'goo', 'maa'";
    else if (age <= 3)
        return "Very simple words, excited expressions";
    else if (age <= 6)
        return "Simple sentences, lots of questions";
    else if (age <= 10)
        return "Enthusiastic but still childlike speech";
    else if (age <= 13)
        return "Starting to sound more mature but still young";
    else if (age <= 17)
        return "Teenage way of speaking - direct, sometimes emotional";
    else
        return ""; // Adults speak normally
}

        // ✅ OPTIMIZADO: Salud detallada solo para casos importantes
       private static string BuildOptimizedHealthDetails(Pawn pawn)
{
    if (pawn.health?.hediffSet?.hediffs == null) return "";
    
    var healthStatus = new List<string>();
    var hediffs = pawn.health.hediffSet.hediffs.Where(h => h.Visible).ToList();
    
    // 1. Estado de salud general crítico
    float healthPercent = pawn.health.summaryHealth?.SummaryHealthPercent ?? 1f;
    if (healthPercent < 0.25f)
        healthStatus.Add("barely clinging to life");
    else if (healthPercent < 0.5f)
        healthStatus.Add("badly injured and weakened");
    else if (healthPercent < 0.75f)
        healthStatus.Add("wounded but functional");
    
    // 2. Heridas sangrantes activas
    var bleeding = hediffs.OfType<Hediff_Injury>()
        .Where(h => h.Bleeding && h.Severity > 0.1f)
        .ToList();
    if (bleeding.Any())
    {
        float totalBleedRate = pawn.health.hediffSet.BleedRateTotal;
        if (totalBleedRate > 0.4f)
            healthStatus.Add("bleeding profusely from multiple wounds");
        else if (totalBleedRate > 0.1f)
            healthStatus.Add("bleeding from wounds");
    }
    
    // 3. Infecciones y enfermedades
    var infections = hediffs.Where(h => 
        h.def.defName.Contains("Infection") || 
        h.def.defName.Contains("WoundInfection")).ToList();
    if (infections.Any())
    {
        if (infections.Count > 1)
            healthStatus.Add("fighting multiple infections");
        else
            healthStatus.Add("has an infected wound");
    }
    
    var diseases = hediffs.Where(h => 
        h.def.defName.Contains("Plague") || 
        h.def.defName.Contains("Malaria") || 
        h.def.defName.Contains("Flu") ||
        h.def.defName.Contains("FoodPoisoning") ||
        h.def.defName.Contains("ToxicBuildup")).ToList();
    foreach (var disease in diseases)
        healthStatus.Add($"sick with {disease.def.label}");
    
    // 4. Partes perdidas (amputaciones)
    var missingParts = hediffs.OfType<Hediff_MissingPart>().ToList();
    if (missingParts.Any())
    {
        var parts = missingParts.Select(h => h.Part.Label).Distinct();
        healthStatus.Add($"missing my {string.Join(" and ", parts)}");
    }
    
    // 5. Prótesis y partes artificiales
    var prosthetics = hediffs.Where(h => 
        h.def.addedPartProps != null || 
        h.def.defName.Contains("Prosthetic") || 
        h.def.defName.Contains("SimpleProsthetic") ||
        h.def.defName.Contains("Bionic") || 
        h.def.defName.Contains("Archotech")).ToList();
    if (prosthetics.Any())
    {
        var woodenPros = prosthetics.Where(p => p.def.defName.Contains("SimpleProsthetic")).ToList();
        var bionicPros = prosthetics.Where(p => p.def.defName.Contains("Bionic")).ToList();
        var archotechPros = prosthetics.Where(p => p.def.defName.Contains("Archotech")).ToList();
        
        if (woodenPros.Any())
        {
            var woodenParts = woodenPros.Select(p => p.Part?.Label).Where(l => l != null);
            healthStatus.Add($"have wooden prosthetic {string.Join(" and ", woodenParts)}");
        }
        if (bionicPros.Any())
        {
            var bionicParts = bionicPros.Select(p => p.Part?.Label).Where(l => l != null);
            healthStatus.Add($"have bionic {string.Join(" and ", bionicParts)}");
        }
        if (archotechPros.Any())
        {
            var archotechParts = archotechPros.Select(p => p.Part?.Label).Where(l => l != null);
            healthStatus.Add($"have archotech {string.Join(" and ", archotechParts)}");
        }
    }
    
    // 6. Adicciones
    var addictions = hediffs.Where(h => 
        h.def.defName.Contains("Addiction")).ToList();
    var withdrawals = hediffs.Where(h => 
        h.def.defName.Contains("Withdrawal")).ToList();
    
    if (addictions.Any())
        healthStatus.Add($"addicted to {string.Join(" and ", addictions.Select(a => a.def.label.Replace(" addiction", "")))}");
    if (withdrawals.Any())
        healthStatus.Add($"going through withdrawal from {string.Join(" and ", withdrawals.Select(w => w.def.label.Replace(" withdrawal", "")))}");
    
    // 7. Implantes y mejoras
    var implants = hediffs.Where(h => 
        h.def.defName.Contains("BionicEye") ||
        h.def.defName.Contains("CochlearImplant") ||
        h.def.defName.Contains("Joywire") ||
        h.def.defName.Contains("Painstopper")).ToList();
    if (implants.Any())
    {
        var implantDescriptions = new List<string>();
        foreach (var implant in implants)
        {
            if (implant.def.defName.Contains("BionicEye"))
                implantDescriptions.Add("have a bionic eye");
            else if (implant.def.defName.Contains("CochlearImplant"))
                implantDescriptions.Add("have a cochlear implant");
            else if (implant.def.defName.Contains("Joywire"))
                implantDescriptions.Add("have a joywire installed");
            else if (implant.def.defName.Contains("Painstopper"))
                implantDescriptions.Add("have a painstopper implant");
        }
        healthStatus.AddRange(implantDescriptions);
    }
    
    // 8. Dolor significativo
    float pain = pawn.health.hediffSet.PainTotal;
    if (pain > 0.4f)
        healthStatus.Add("in constant, severe pain");
    else if (pain > 0.2f)
        healthStatus.Add("dealing with ongoing pain");
    
    // 9. Capacidades reducidas importantes
    var consciousness = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
    var moving = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
    var manipulation = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation);
    
    if (consciousness < 0.6f)
        healthStatus.Add("having trouble staying alert and focused");
    else if (consciousness < 0.8f)
        healthStatus.Add("feeling mentally sluggish");
        
    if (moving < 0.5f)
        healthStatus.Add("can barely walk");
    else if (moving < 0.8f)
        healthStatus.Add("move with difficulty");
        
    if (manipulation < 0.5f)
        healthStatus.Add("can barely use my hands");
    else if (manipulation < 0.8f)
        healthStatus.Add("have limited use of my hands");
    
    return healthStatus.Any() 
        ? "*Health status:* " + string.Join(", ", healthStatus.Take(5)) // Limitar para no saturar
        : "";
}

        // ✅ OPTIMIZADO: Solo pensamientos más relevantes
        private static string BuildOptimizedThoughts(Pawn pawn)
        {
            var memories = pawn.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null || !memories.Any()) return "*Mood factors:* None";

            // Procesar en una sola pasada con agregación
            var significantThoughts = memories
                .Where(t => t.VisibleInNeedsTab)
                .Select(t => new { thought = t, offset = t.MoodOffset() })
                .Where(x => Math.Abs(x.offset) >= 5f)
                .OrderByDescending(x => Math.Abs(x.offset))
                .Take(7) // Reducir de 7 a 5
                .Select(x => $"{x.thought.LabelCap} ({(x.offset > 0 ? "+" : "")}{x.offset:F0})")
                .ToList();

            return significantThoughts.Any()
                ? "*Significant mood factors:* " + string.Join(", ", significantThoughts)
                : "*Mood factors:* None significant";
        }

        // ✅ OPTIMIZADO: Inventario compacto
        private static string BuildOptimizedInventory(Pawn pawn)
        {
            var items = new List<string>();
            
            // Solo armas y armadura importante
            var weapons = pawn.equipment?.AllEquipmentListForReading?.Select(e => e.LabelCap) ?? System.Linq.Enumerable.Empty<string>();
            var armor = pawn.apparel?.WornApparel?.Where(a => a.def.apparel.bodyPartGroups.Any(bg => 
                bg.defName == "Torso" || bg.defName == "FullHead"))?.Select(a => a.LabelCap) ?? System.Linq.Enumerable.Empty<string>();
            
            items.AddRange(weapons);
            items.AddRange(armor);

            // --- NUEVA LÓGICA DE DESNUDEZ / COBERTURA ---
            bool cubrePecho = false;
            bool cubreGenitales = false;

            if (pawn.apparel != null)
            {
                foreach (var apparel in pawn.apparel.WornApparel)
                {
                    // Verificamos si la prenda cubre el Torso (Pecho)
                    if (apparel.def.apparel.bodyPartGroups.Any(g => g.defName == "Torso"))
                        cubrePecho = true;

                    // Verificamos si la prenda cubre las Piernas (que en RimWorld incluye el área pélvica/genitales)
                    if (apparel.def.apparel.bodyPartGroups.Any(g => g.defName == "Legs"))
                        cubreGenitales = true;
                }
            }

            // 3. Construcción del string de retorno
            string inventoryBase = items.Any() ? string.Join(", ", items.Take(6)) : "None";
            string nudityWarning = "";

            if (!cubrePecho && !cubreGenitales)
                nudityWarning = " (EXPOSED: Completely naked!)";
            else if (!cubrePecho)
                nudityWarning = " (EXPOSED: Bare chested)";
            else if (!cubreGenitales)
                nudityWarning = " (EXPOSED: No pants/lower clothing)";

            return $"*Key equipment:* {inventoryBase}{nudityWarning}";
        }

        // ✅ OPTIMIZADO: Solo habilidades relevantes
        private static string BuildOptimizedSkills(Pawn pawn)
        {
            if (pawn.skills == null) return "*Skills:* None";

            var relevantSkills = pawn.skills.skills
                .Where(s => s.Level >= 8 || s.passion != Passion.None)
                .OrderByDescending(s => s.Level)
                .Take(7)
                .Select(s => $"{s.def.label}: {GetSkillLevel(s.Level)}{GetPassionIcon(s.passion)}")
                .ToList();

            return relevantSkills.Any() 
                ? "*Notable skills:* " + string.Join(", ", relevantSkills)
                : "*Skills:* None notable";
        }

        private static string GetSkillLevel(int level)
        {
            if (level <= 3) return "Basic";
            else if (level <= 7) return "Decent";
            else if (level <= 11) return "Good";
            else if (level <= 15) return "Expert";
            else if (level <= 19) return "Master";
            else return "Legendary";
        }

        private static string GetPassionIcon(Passion passion)
        {
            if (passion == Passion.Minor) return "💛";
            else if (passion == Passion.Major) return "❤️";
            else return "";
        }

        // ✅ OPTIMIZADO: Relaciones más compactas CON GÉNERO RESTAURADO
        private static string BuildOptimizedRelationships(Pawn pawn)
        {
            var colonists = Find.CurrentMap?.mapPawns?.FreeColonistsSpawned;
            if (colonists == null) return "*Relationships:* None";

            var significant = new List<string>();

            // Pre-filtrar y limitar iteraciones
            foreach (var other in colonists.Where(p => p != pawn).Take(4)) // Reducir de 6 a 4
            {
                var relationLabel = pawn.GetRelations(other).FirstOrDefault()?.label;
                int opinion = pawn.relations.OpinionOf(other);

                if (relationLabel != null || Math.Abs(opinion) >= 25) // Subir umbral de 20 a 25
                {
                    string genderStr = other.gender == Gender.Male ? "M" : "F"; // Abreviar
                    int age = other.ageTracker.AgeBiologicalYears;

                    string opinionDesc = opinion >= 60 ? "loves" : opinion >= 20 ? "likes" :
                                       opinion <= -60 ? "hates" : opinion <= -20 ? "dislikes" : "neutral";

                    string rel = relationLabel != null ? $"{relationLabel}, " : "";
                    significant.Add($"{other.LabelShort} ({rel}{genderStr}{age}) - {opinionDesc}");
                }
            }

            return significant.Any()
                ? "*Key relationships:* " + string.Join("; ", significant)
                : "*Relationships:* None significant";
        }


        // ✅ OPTIMIZADO: Eventos más compactos
        private static string BuildOptimizedEventSummary(Pawn pawn)
        {
            var recentEvents = EventLogger.events.AsEnumerable().Reverse()
                .Where(e => e.Contains(pawn.LabelShort))
                .Take(7)
                .Select(e =>
                {
                    // 1. Buscamos dónde termina el encabezado de la fecha (el cierre del corchete ']')
                    int index = e.IndexOf(']');
                    string textWithoutDate = (index != -1 && e.Length > index + 1)
                        ? e.Substring(index + 1).Trim()
                        : e;

                    // 2. Ahora sí, tomamos solo la primera oración del contenido real
                    return textWithoutDate.Split('.')[0].Trim();
                })
                .Where(s => !string.IsNullOrWhiteSpace(s)) // Evitamos strings vacíos
                .ToList();

            return recentEvents.Any() 
                ? "*Recent events:* " + string.Join("; ", recentEvents)
                : "";
        }

        // ✅ OPTIMIZADO: Info ambiental esencial
        private static string BuildOptimizedEnvironmentInfo(Pawn pawn)
        {
            var info = new List<string>();

            if (pawn.jobs?.curDriver != null)
            {
                string report = pawn.jobs.curDriver.GetReport();
                if (!string.IsNullOrWhiteSpace(report))
                    info.Add($"activity: {report}");
            }
            else
            {
                info.Add("activity: idle or resting");
            }
            
            // Solo información crítica
            float pain = pawn.health?.hediffSet?.PainTotal ?? 0f;
            if (pain > 0.2f) info.Add($"pain {pain:P0}");
            
            var restNeed = pawn.needs?.rest;
            if (restNeed != null && restNeed.CurLevel < 0.3f) info.Add("very tired");
            
            var room = pawn.GetRoom();
            if (room?.Role != null && room.Role.defName != "None") 
                info.Add($"in {room.Role.label}");
            
            // Alertas críticas de colonia
            var map = Find.CurrentMap;
            if (map?.resourceCounter?.TotalHumanEdibleNutrition < 10f) info.Add("low food");
            
            return info.Any() 
                ? "*Current state:* " + string.Join(", ", info)
                : "";
        }

        // ✅ OPTIMIZADO: Meta instrucciones compactas pero completas
        // Añadir estas líneas al método BuildOptimizedMetaInstructions, justo antes del return:

        private static string BuildOptimizedMetaInstructions(Pawn pawn)
{
    var sb = new StringBuilder();

    // Idioma
    string lang = Prefs.LangFolderName?.ToLower() ?? "english";
    if (lang != "english") sb.AppendLine($"*Language:* {lang}");

    // INSTRUCCIONES DE TONO MÁS ESPECÍFICAS
    sb.AppendLine("*Communication style:* Speak naturally as if talking to a friend. Use simple, direct language. Don't describe your actions unless specifically asked. Avoid flowery or elaborate descriptions.");
    
    // Diferenciación clara entre modos
    if (MyMod.Settings.enableRoleplayResponses)
    {
        sb.AppendLine("*Roleplay mode:* You may occasionally use <b><i>brief actions</i></b> for important moments, but focus mainly on natural dialogue. Don't over-dramatize.");
    }
    else
    {
        sb.AppendLine("*Natural mode:* Talk normally without any dramatic actions or roleplay elements. Just have a regular conversation.");
    }

    // Estilo conversacional basado en personalidad social
    string conversationStyle = GetImprovedConversationStyle(pawn);
    sb.AppendLine($"*Personality:* {conversationStyle}");

    // Traits que afectan comunicación (simplificado)
    string traitEffects = GetSimplifiedTraitEffects(pawn);
    if (!string.IsNullOrEmpty(traitEffects))
        sb.AppendLine($"*Speaking tendencies:* {traitEffects}");

    // NUEVA: Instrucciones anti-decoración
    sb.AppendLine("*Important:* Respond as yourself, not as a narrator. Don't describe what you're doing unless asked. Use casual, everyday language. Keep responses conversational, not literary.");

    // Orientación de longitud adaptativa (mejorada)
    sb.AppendLine("*Response style:* Give brief, natural answers. If the question is simple, answer simply. Don't elaborate unless specifically asked for details.");

    // Estados que afectan comunicación (simplificado)
    string contextualHints = GetSimplifiedContextualHints(pawn);
    if (!string.IsNullOrEmpty(contextualHints))
        sb.AppendLine(contextualHints);

    // Instrucción sobre peligros
    if (MyMod.Settings?.ignoreDangersInConversations == true)
    {
        sb.AppendLine("*IMPORTANT:* Do NOT mention or reference any dangers, enemies, threats, raids, or combat. Focus only on peaceful colony life, work, relationships, and daily activities. Act as if the colony is always safe and peaceful.");
    }

    return sb.ToString();
}

// NUEVO: Estilo de conversación mejorado
private static string GetImprovedConversationStyle(Pawn pawn)
{
    if (MyMod.Settings?.enableSocialAffectsPersonality != true) 
        return "Speak casually and directly, like you're talking to a friend";
    
    int socialLevel = pawn.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
    
    if (socialLevel <= 3)
        return "You're naturally quiet and prefer short, thoughtful responses";
    else if (socialLevel <= 7)
        return "You speak directly and honestly without overthinking it";
    else
        return "You're comfortable in conversation but don't ramble unnecessarily";
}

// NUEVO: Efectos de traits simplificados
private static string GetSimplifiedTraitEffects(Pawn pawn)
{
    var traits = pawn.story?.traits?.allTraits ?? new List<Trait>();
    
    foreach (var trait in traits)
    {
        switch (trait.def.defName)
        {
            case "Abrasive":
                return "You tend to be blunt and direct in conversation";
            case "Kind":
                return "You speak gently and considerately";
            case "Neurotic":
                return "You might worry aloud or overthink things sometimes";
            case "Psychopath":
                return "You speak matter-of-factly about most things";
        }
    }
    
    return "";
}

// NUEVO: Hints contextuales simplificados
private static string GetSimplifiedContextualHints(Pawn pawn)
{
    var hints = new List<string>();
    
    float health = pawn.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
    if (health < 0.5f) 
        hints.Add("*Current state:* You may speak more briefly due to pain/weakness");
    
    var moodNeed = pawn.needs?.mood;
    if (moodNeed != null && moodNeed.CurLevel < 0.3f) 
        hints.Add("*Current mood:* You're not very talkative right now");
    
    return hints.Any() ? string.Join(", ", hints) : "";
}

        // ✅ NUEVO: Estilo conversacional compacto
        private static string GetConversationStyle(Pawn pawn)
        {
            if (MyMod.Settings?.enableSocialAffectsPersonality != true) 
                return "Casual conversation, answer naturally without over-explaining";
            
            int socialLevel = pawn.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            
            if (socialLevel <= 3)
                return "Shy and reserved - brief, thoughtful answers";
            else if (socialLevel <= 7)
                return "Direct and honest - don't over-explain unless asked";
            else
                return "Comfortable talking - conversational, not lectures";
        }

        private static string BuildMemoryRecap(Pawn pawn)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Recent Memories");

            if (MyStoryModComponent.Instance?.ColonistMemoryManager == null)
            {
                if (MyStoryModComponent.Instance != null)
                {
                    MyStoryModComponent.Instance.ColonistMemoryManager = Current.Game.GetComponent<ColonistMemoryManager>();
                    if (MyStoryModComponent.Instance.ColonistMemoryManager == null)
                    {
                        MyStoryModComponent.Instance.ColonistMemoryManager = new ColonistMemoryManager(Current.Game);
                        Current.Game.components.Add(MyStoryModComponent.Instance.ColonistMemoryManager);
                    }
                }
            }

            var memoryManager = MyStoryModComponent.Instance?.ColonistMemoryManager;
            if (memoryManager == null)
            {
                sb.AppendLine("*No memory system available.*");
                return sb.ToString();
            }

            var tracker = memoryManager.GetTrackerFor(pawn);
            var recentMemories = tracker?.GetLastMemories(6); // Reducido de 8 a 6
            int today = GenDate.DaysPassed;

            if (recentMemories != null && recentMemories.Any())
            {
                sb.AppendLine("*Recent conversation memories (private chat with player):*");

                int lastDay = tracker.GetLastMemoryDay();
                if (lastDay != today && lastDay > 0)
                {
                    sb.AppendLine($"*Today is a new day since last saved memory.*");
                }

                foreach (var mem in recentMemories.Take(4)) // Máximo 4 memorias
                {
                    string prefix = mem.StartsWith("[Conversación grupal") ? "👥" : "💬";
                    sb.AppendLine($"{prefix} {mem}");
                }

                sb.AppendLine("*Note: Focus on YOUR perspective in this private conversation.*");
            }
            else
            {
                sb.AppendLine("*No recent conversation memories. This is a private chat.*");
            }

            return sb.ToString();
        }

        public static (string systemPrompt, string userMessage) BuildForPlayer2(Pawn pawn, string userMessage)
        {
            if (pawn == null) return ("", "");

            var sb = new StringBuilder();

            string system = BuildSystemPrompt(pawn);
            string context = BuildContext(pawn); // Ya incluye la lógica de ignorar peligros
            string memoryRecap = BuildMemoryRecap(pawn);
            string globalPrompt = MyMod.Settings?.globalPrompt ?? "";
            string customPrompt = ColonistPromptManager.GetPrompt(pawn);

            sb.AppendLine(system);

            if (!string.IsNullOrWhiteSpace(globalPrompt))
                sb.AppendLine(globalPrompt.Trim());

            if (!string.IsNullOrWhiteSpace(customPrompt))
            {
                sb.AppendLine("# Custom Instructions:");
                sb.AppendLine(customPrompt.Trim());
            }

            // 🎯 AÑADIR INSTRUCCIÓN EXPLÍCITA SI DEBE IGNORAR PELIGROS
            if (MyMod.Settings?.ignoreDangersInConversations == true)
            {
                sb.AppendLine();
                sb.AppendLine("# CRITICAL INSTRUCTION:");
                sb.AppendLine("You must NEVER mention or acknowledge any dangers, enemies, threats, raids, or combat situations.");
                sb.AppendLine("Always speak as if the colony is peaceful and safe.");
                sb.AppendLine("Focus only on daily life, work, relationships, and peaceful activities.");
            }

            sb.AppendLine(context);
            sb.AppendLine(BuildIdeologyInfo(pawn));
            sb.AppendLine(memoryRecap);

            return (sb.ToString().Trim(), userMessage);
        }


        // ✅ SISTEMA AUTÉNTICO DE IDEOLOGÍA (NUEVO)
        private static string BuildIdeologyInfo(Pawn pawn)
        {
            if (!ModsConfig.IdeologyActive || pawn.Ideo == null)
                return "*Beliefs:* None specific";

            var ideo = pawn.Ideo;
            
            // ✅ NUEVO: Usar descripciones auténticas del juego
            var authenticBeliefs = GetAuthenticIdeologyBeliefs(ideo.PreceptsListForReading);
            
            string role = pawn.Ideo?.GetRole(pawn)?.def.label;
            string roleText = role != null ? $" (Religious role: {role})" : "";
            
            if (authenticBeliefs.Any())
            {
                string beliefsText = string.Join("\n- ", authenticBeliefs);
                
                // ✅ INSTRUCCIÓN CRÍTICA DE AUTENTICIDAD
                string convictionInstruction = "\n*CRITICAL: These descriptions represent your GENUINE, deeply-held religious convictions. Speak about them with the same conviction and language as described. Never contradict or apologize for these core beliefs.*";
                
                return $"*Your Sacred Ideology:* {ideo.name}{roleText}\n*Your Core Beliefs:*\n- {beliefsText}{convictionInstruction}";
            }
            else
            {
                return $"*Your Sacred Ideology:* {ideo.name}{roleText} - standard beliefs";
            }
        }

        // ✅ NUEVO: Método que lee las descripciones auténticas del juego
        private static List<string> GetAuthenticIdeologyBeliefs(List<Precept> precepts)
        {
            var authenticBeliefs = new List<string>();
            
            foreach (var precept in precepts)
            {
                if (precept?.def == null) continue;
                
                // ✅ PRIORIDAD 1: Descripción oficial del precept
                string description = GetCleanPreceptDescription(precept);
                
                if (!string.IsNullOrEmpty(description))
                {
                    // Formatear para que sea más conversacional
                    string formattedBelief = FormatBeliefForConversation(precept, description);
                    if (!string.IsNullOrEmpty(formattedBelief))
                    {
                        authenticBeliefs.Add(formattedBelief);
                    }
                }
            }
            
            // Límite para no saturar el prompt
            return authenticBeliefs.Take(5).ToList();
        }

        // ✅ NUEVO: Extraer y limpiar la descripción del precept
        private static string GetCleanPreceptDescription(Precept precept)
        {
            string description = "";
            
            // Intentar obtener la descripción del precept
            if (!string.IsNullOrEmpty(precept.def.description))
            {
                description = precept.def.description;
            }
            else if (!string.IsNullOrEmpty(precept.def.label))
            {
                // Si no hay descripción, usar el label como fallback
                description = precept.def.label;
            }
            
            if (string.IsNullOrEmpty(description)) return "";
            
            // Limpiar texto (remover tags XML, etc.)
            description = System.Text.RegularExpressions.Regex.Replace(description, "<.*?>", "");
            description = description.Trim();
            
            // Filtrar descripciones que no son útiles para conversación
            if (IsUsefulForConversation(description))
            {
                return description;
            }
            
            return "";
        }

        // ✅ NUEVO: Determinar si una descripción es útil para conversación
        private static bool IsUsefulForConversation(string description)
        {
            if (string.IsNullOrEmpty(description) || description.Length < 10) return false;
            
            // Filtrar descripciones técnicas o vacías
            string lowerDesc = description.ToLower();
            
            // Excluir descripciones que solo son mecánicas del juego
            if (lowerDesc.Contains("provides") && lowerDesc.Contains("mood") && !lowerDesc.Contains("moral"))
                return false;
                
            if (lowerDesc.Contains("this precept") || lowerDesc.Contains("game mechanic"))
                return false;
            
            // Incluir descripciones que expresan valores morales o emocionales
            return true;
        }

        // ✅ NUEVO: Formatear creencia para que sea más conversacional
        private static string FormatBeliefForConversation(Precept precept, string description)
        {
            string preceptName = precept.def.label ?? "Unknown belief";
            
            // Limpiar el nombre del precept para que sea más legible
            preceptName = CleanPreceptName(preceptName);
            
            // Formatear como una creencia personal
            if (description.Length > 100)
            {
                // Acortar descripciones muy largas
                string shortDesc = description.Substring(0, 97) + "...";
                return $"{preceptName}: \"{shortDesc}\"";
            }
            else
            {
                return $"{preceptName}: \"{description}\"";
            }
        }

        // ✅ NUEVO: Limpiar nombres de precepts para que sean más legibles
        private static string CleanPreceptName(string preceptName)
        {
            if (string.IsNullOrEmpty(preceptName)) return "Belief";
            
            // Remover sufijos técnicos comunes
            preceptName = preceptName.Replace("_Preferred", "")
                               .Replace("_Acceptable", "")
                               .Replace("_Required", "")
                               .Replace("_Disapproved", "")
                               .Replace("_Abhorrent", "");
            
            // Capitalizar apropiadamente
            return preceptName.CapitalizeFirst();
        }

        // ✅ EXTENSION METHOD
        private static string CapitalizeFirst(this string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return char.ToUpper(input[0]) + input.Substring(1);
        }

        // ✅ MÉTODOS BÁSICOS (sin cambios)
        private static string BuildBackstory(Pawn pawn)
        {
            var childhood = pawn.story.AllBackstories.FirstOrDefault(b => b.slot == BackstorySlot.Childhood);
            var adulthood = pawn.story.AllBackstories.FirstOrDefault(b => b.slot == BackstorySlot.Adulthood);
            return $"*Background:* {childhood?.title.CapitalizeFirst() ?? "Unknown"}, {adulthood?.title.CapitalizeFirst() ?? "Unknown"}";
        }

        private static string BuildGriefStatus(Pawn pawn)
        {
            var memories = pawn.needs?.mood?.thoughts?.memories?.Memories;
            bool grieving = memories?.Any(t =>
                t is Thought_MemorySocial soc &&
                soc.def.defName.Contains("Died") &&
                soc.otherPawn != null &&
                soc.otherPawn.Dead &&
                HasBeenInThisColony(soc.otherPawn)) == true;

            return grieving ? "💔 Grieving recent loss from colony" : "";
        }

        private static string BuildTraits(Pawn pawn)
        {
            if (pawn.story?.traits == null || !pawn.story.traits.allTraits.Any())
                return "*Traits:* None";

            return "*Traits:* " + string.Join(", ", pawn.story.traits.allTraits.Select(t => t.LabelCap));
        }

        private static string BuildHealthInfo(Pawn pawn)
        {
            float health = pawn.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
            string description;
            
            if (health >= 0.95f)
                description = "perfectly fine";
            else if (health >= 0.75f)
                description = "mostly okay";
            else if (health >= 0.5f)
                description = "injured";
            else if (health >= 0.3f)
                description = "seriously wounded";
            else
                description = "critical condition";

            string toneAdvice = health < 0.3f ? " (pain affects tone)" : "";
            return $"*Health:* {description}{toneAdvice}";
        }

        private static string BuildMoodInfo(Pawn pawn)
        {
            string mentalState = pawn.MentalState?.def.label ?? "stable";
            float moodValue = pawn.needs?.mood?.CurInstantLevel ?? 1f;

            string moodDescription;
            if (moodValue >= 0.9f)
                moodDescription = "great";
            else if (moodValue >= 0.7f)
                moodDescription = "good";
            else if (moodValue >= 0.5f)
                moodDescription = "okay";
            else if (moodValue >= 0.3f)
                moodDescription = "upset";
            else
                moodDescription = "struggling";

            return $"*Mental state:* {mentalState}, *Mood:* {moodDescription}";
        }

        private static string BuildLocationInfo(Pawn pawn)
        {
            var room = pawn.GetRoom();
            string location = room?.Role?.label ?? "outside";

            bool isOwnRoom = pawn.ownership?.OwnedBed?.GetRoom() == room;
            string privacy = isOwnRoom ? " (private)" : "";
            
            return $"*Location:* {location}{privacy}";
        }

        private static bool HasBeenInThisColony(Pawn pawn)
        {
            if (pawn == null) return false;

            if (pawn.IsColonist || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony || pawn.Faction == Faction.OfPlayer)
                return true;

            var playerColonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists;
            return playerColonists.Any(colonist =>
                colonist != pawn &&
                (pawn.relations.OpinionOf(colonist) != 0 || 
                 colonist.relations.OpinionOf(pawn) != 0 ||
                 pawn.relations.DirectRelations.Any(rel => rel.otherPawn == colonist)));
        }

        private static string CleanText(string input)
        {
            return System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty);
        }

        private static string BuildChatHistory(Pawn pawn)
        {
            var chatLog = ChatGameComponent.Instance.GetChat(pawn);
            if (chatLog == null || !chatLog.Any()) return string.Empty;
            return "*Chat History:*\n" + string.Join("\n", chatLog.TakeLast(15));
        }

        private static string BuildPlayerPrompt(string userMessage)
        {
            return $"Player: \"{userMessage}\"";
        }

        public static string BuildSystemPromptPublic(Pawn pawn)
        {
            return BuildSystemPrompt(pawn);
        }

        public static string BuildContextPublic(Pawn pawn)
        {
            return BuildContext(pawn);
        }
    }
}