using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace EchoColony
{
    /// <summary>
    /// Parser de comandos para el chat del storyteller
    /// Interpreta comandos como /solar, /asalto, /raid, etc.
    /// </summary>
    public static class StorytellerCommandParser
    {
        // Diccionario de comandos disponibles
        private static Dictionary<string, IncidentDef> commandToIncident = new Dictionary<string, IncidentDef>();
        
        static StorytellerCommandParser()
        {
            InitializeCommands();
        }

        private static void InitializeCommands()
        {
            commandToIncident.Clear();
            
            // Helper para buscar IncidentDef por nombre (sin warnings)
            IncidentDef GetIncident(string defName)
            {
                return DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
            }
            
            // ===== CLIMA / WEATHER =====
            var solarFlare = GetIncident("SolarFlare");
            if (solarFlare != null)
            {
                commandToIncident["solar"] = solarFlare;
                commandToIncident["solarflare"] = solarFlare;
                commandToIncident["tormenta_solar"] = solarFlare;
            }
            
            var eclipse = GetIncident("Eclipse");
            if (eclipse != null)
            {
                commandToIncident["eclipse"] = eclipse;
            }
            
            var toxicFallout = GetIncident("ToxicFallout");
            if (toxicFallout != null)
            {
                commandToIncident["toxico"] = toxicFallout;
                commandToIncident["toxic"] = toxicFallout;
                commandToIncident["lluvia_toxica"] = toxicFallout;
            }
            
            // ===== DLC ANOMALY (solo si está disponible) =====
            var voidCuriosity = GetIncident("VoidCuriosity");
            if (voidCuriosity != null)
            {
                commandToIncident["void"] = voidCuriosity;
            }
            
            var shambler = GetIncident("ShamblerSwarm");
            if (shambler != null)
            {
                commandToIncident["shambler"] = shambler;
                commandToIncident["enjambre"] = shambler;
            }
            
            var noxiousHaze = GetIncident("NoxiousHaze");
            if (noxiousHaze != null)
            {
                commandToIncident["haze"] = noxiousHaze;
                commandToIncident["niebla"] = noxiousHaze;
            }
            
            var psychicRitual = GetIncident("PsychicRitualSiege");
            if (psychicRitual != null)
            {
                commandToIncident["psychic"] = psychicRitual;
                commandToIncident["psiquico"] = psychicRitual;
            }
            
            // ===== AMENAZAS / THREATS =====
            var raidEnemy = GetIncident("RaidEnemy");
            if (raidEnemy != null)
            {
                commandToIncident["raid"] = raidEnemy;
                commandToIncident["asalto"] = raidEnemy;
                commandToIncident["ataque"] = raidEnemy;
            }
            
            var raidFriendly = GetIncident("RaidFriendly");
            if (raidFriendly != null)
            {
                commandToIncident["raid_amigo"] = raidFriendly;
                commandToIncident["friendly_raid"] = raidFriendly;
            }
            
            var manhunter = GetIncident("ManhunterPack");
            if (manhunter != null)
            {
                commandToIncident["manhunter"] = manhunter;
                commandToIncident["cazadores"] = manhunter;
                commandToIncident["animales"] = manhunter;
            }
            
            var infestation = GetIncident("Infestation");
            if (infestation != null)
            {
                commandToIncident["infestation"] = infestation;
                commandToIncident["infestacion"] = infestation;
                commandToIncident["insectos"] = infestation;
            }
            
            var mechCluster = GetIncident("MechCluster");
            if (mechCluster != null)
            {
                commandToIncident["mech"] = mechCluster;
                commandToIncident["mecanoides"] = mechCluster;
            }
            
            // ===== VISITANTES / VISITORS =====
            var wanderer = GetIncident("WandererJoin");
            if (wanderer != null)
            {
                commandToIncident["wanderer"] = wanderer;
                commandToIncident["viajero"] = wanderer;
            }
            
            var visitorGroup = GetIncident("VisitorGroup");
            if (visitorGroup != null)
            {
                commandToIncident["visitantes"] = visitorGroup;
                commandToIncident["visitors"] = visitorGroup;
            }
            
            var travelerGroup = GetIncident("TravelerGroup");
            if (travelerGroup != null)
            {
                commandToIncident["viajeros"] = travelerGroup;
                commandToIncident["travelers"] = travelerGroup;
            }
            
            // ===== COMERCIO / TRADE =====
            var traderCaravan = GetIncident("TraderCaravanArrival");
            if (traderCaravan != null)
            {
                commandToIncident["caravana"] = traderCaravan;
                commandToIncident["caravan"] = traderCaravan;
                commandToIncident["comerciantes"] = traderCaravan;
            }
            
            var orbitalTrader = GetIncident("OrbitalTraderArrival");
            if (orbitalTrader != null)
            {
                commandToIncident["orbital"] = orbitalTrader;
                commandToIncident["nave"] = orbitalTrader;
            }
            
            // ===== EVENTOS ESPECIALES / SPECIAL =====
            var giveQuest = GetIncident("GiveQuest_Random");
            if (giveQuest != null)
            {
                commandToIncident["quest"] = giveQuest;
                commandToIncident["mision"] = giveQuest;
            }
            
            var shipChunk = GetIncident("ShipChunkDrop");
            if (shipChunk != null)
            {
                commandToIncident["ship"] = shipChunk;
                commandToIncident["nave_caida"] = shipChunk;
            }
            
            var herdMigration = GetIncident("HerdMigration");
            if (herdMigration != null)
            {
                commandToIncident["migracion"] = herdMigration;
                commandToIncident["migration"] = herdMigration;
            }
            
            var farmAnimals = GetIncident("FarmAnimalsWanderIn");
            if (farmAnimals != null)
            {
                commandToIncident["animales_granja"] = farmAnimals;
                commandToIncident["farm_animals"] = farmAnimals;
            }
            
            // ===== OTROS / OTHERS =====
            var lavaFlow = GetIncident("LavaFlow");
            if (lavaFlow != null)
            {
                commandToIncident["lava"] = lavaFlow;
            }
            
            var drought = GetIncident("Drought");
            if (drought != null)
            {
                commandToIncident["sequia"] = drought;
                commandToIncident["drought"] = drought;
            }
            
            Log.Message($"[EchoColony] Storyteller command parser initialized with {commandToIncident.Count} commands");
        }

        /// <summary>
        /// Intenta parsear un mensaje y ejecutar el comando si existe
        /// </summary>
        public static bool TryParseCommand(string message, out string response)
        {
            response = "";
            
            if (string.IsNullOrWhiteSpace(message) || !message.StartsWith("/"))
                return false;

            // Remover el "/" y limpiar
            string command = message.Substring(1).Trim().ToLowerInvariant();
            
            // Comando especial: listar todos los comandos
            if (command == "help" || command == "ayuda" || command == "comandos")
            {
                response = GetHelpText();
                return true;
            }

            // Comando especial: listar todos los incidentes disponibles
            if (command == "list" || command == "lista")
            {
                response = GetAllIncidentsText();
                return true;
            }

            // Comando para listar condiciones activas
            if (command == "active" || command == "activos" || command == "status")
            {
                response = GetActiveConditionsText();
                return true;
            }

            // Comandos para detener condiciones
            if (command.StartsWith("stop ") || command.StartsWith("detener "))
            {
                string conditionName = command.Contains("stop ") 
                    ? command.Substring(5).Trim() 
                    : command.Substring(8).Trim();
                
                response = TryStopCondition(conditionName);
                return true;
            }

            // Buscar el comando en el diccionario
            if (commandToIncident.TryGetValue(command, out IncidentDef incidentDef))
            {
                bool success = StorytellerIncidentExecutor.TryExecuteIncident(incidentDef);
                
                if (success)
                {
                    response = $"✓ Command executed: {incidentDef.label}";
                }
                else
                {
                    response = $"✗ Failed to execute: {incidentDef.label}";
                }
                
                return true;
            }

            // Si no se encuentra el comando
            response = $"Unknown command: /{command}\nType /help to see available commands.";
            return true;
        }

        private static string GetHelpText()
        {
            var lines = new List<string>
            {
                "=== STORYTELLER COMMANDS ===",
                "",
                "CLIMATE & EVENTS:",
                "  /solar or /tormenta_solar - Solar flare",
                "  /eclipse - Solar eclipse",
                "  /toxico or /toxic - Toxic fallout",
                "  /sequia or /drought - Drought",
                "  /lava - Lava flow",
                "  /void - Void curiosity",
                "",
                "THREATS:",
                "  /raid or /asalto - Enemy raid",
                "  /manhunter or /cazadores - Manhunter pack",
                "  /infestation or /infestacion - Insect infestation",
                "  /mech or /mecanoides - Mech cluster",
                "  /shambler or /enjambre - Shambler swarm",
                "",
                "VISITORS & TRADERS:",
                "  /wanderer or /viajero - Wanderer joins",
                "  /visitantes or /visitors - Visitor group",
                "  /caravana or /caravan - Trade caravan",
                "  /orbital or /nave - Orbital trader",
                "",
                "ANIMALS:",
                "  /migracion or /migration - Herd migration",
                "  /animales_granja - Farm animals wander in",
                "",
                "SPECIAL:",
                "  /psychic or /psiquico - Psychic ritual siege",
                "  /quest or /mision - Random quest",
                "  /ship or /nave_caida - Ship chunk drop",
                "  /haze or /niebla - Noxious haze",
                "",
                "STOP EVENTS:",
                "  /active or /status - List active conditions",
                "  /stop <name> - Stop a condition (e.g., /stop toxic)",
                "  /detener <name> - Stop a condition (Spanish)",
                "",
                "UTILITY:",
                "  /help - Show this help",
                "  /list - List all available incidents",
                ""
            };
            
            return string.Join("\n", lines);
        }

        private static string GetAllIncidentsText()
        {
            var incidents = DefDatabase<IncidentDef>.AllDefs
                .Where(def => def.workerClass != null)
                .OrderBy(def => def.category?.label ?? "Unknown")
                .ThenBy(def => def.label)
                .Take(50);

            var lines = new List<string>
            {
                "=== AVAILABLE INCIDENTS (first 50) ===",
                ""
            };

            string lastCategory = "";
            foreach (var incident in incidents)
            {
                string category = incident.category?.label ?? "Unknown";
                if (category != lastCategory)
                {
                    lines.Add("");
                    lines.Add($"{category.ToUpperInvariant()}:");
                    lastCategory = category;
                }
                lines.Add($"  {incident.defName} - {incident.label}");
            }

            lines.Add("");
            lines.Add("Note: Not all incidents may work with all maps/conditions");
            
            return string.Join("\n", lines);
        }

        private static string GetActiveConditionsText()
        {
            var conditions = StorytellerIncidentExecutor.GetActiveConditions();
            
            if (conditions.Count == 0)
            {
                return "No active game conditions at the moment.";
            }
            
            var lines = new List<string>
            {
                "=== ACTIVE GAME CONDITIONS ===",
                ""
            };
            
            foreach (var condition in conditions)
            {
                string timeLeft = condition.Permanent 
                    ? "Permanent" 
                    : $"{condition.TicksLeft.ToStringTicksToPeriod()} remaining";
                
                lines.Add($"• {condition.def.label} ({condition.def.defName}) - {timeLeft}");
            }
            
            lines.Add("");
            lines.Add("Use /stop <name> to end a condition");
            
            return string.Join("\n", lines);
        }

        private static string TryStopCondition(string conditionName)
        {
            // Primero intentar detener por defName exacto
            bool stopped = StorytellerIncidentExecutor.TryStopGameCondition(conditionName);
            
            if (stopped)
            {
                return $"✓ Stopped condition: {conditionName}";
            }
            
            // Si no funciona, intentar búsqueda parcial
            int count = StorytellerIncidentExecutor.StopAllConditionsOfType(conditionName);
            
            if (count > 0)
            {
                return $"✓ Stopped {count} condition(s) matching '{conditionName}'";
            }
            
            return $"✗ No active condition found matching '{conditionName}'\nUse /active to see current conditions";
        }

        /// <summary>
        /// Obtiene una lista de comandos disponibles para autocompletado
        /// </summary>
        public static List<string> GetAvailableCommands()
        {
            return commandToIncident.Keys
                .Select(cmd => "/" + cmd)
                .OrderBy(cmd => cmd)
                .ToList();
        }
    }
}