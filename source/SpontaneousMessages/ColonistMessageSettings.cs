using System.Collections.Generic;
using Verse;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Configuración de mensajes espontáneos por colono individual
    /// Permite override de settings globales para cada colono
    /// </summary>
    public class ColonistMessageSettings : IExposable
    {
        public bool enabled = true;
        public int maxMessagesPerDay = 1; // 1-3
        public float cooldownHours = 12f; // Horas de juego entre mensajes

        // Triggers permitidos para este colono
        public HashSet<TriggerType> allowedTriggers = new HashSet<TriggerType>
        {
            TriggerType.Incident,
            TriggerType.CriticalNeed,
            TriggerType.ColonySituation,
            TriggerType.Random
        };

        public ColonistMessageSettings()
        {
            // Constructor vacío para serialización
        }

        public ColonistMessageSettings(int maxMessages, float cooldown)
        {
            this.maxMessagesPerDay = maxMessages;
            this.cooldownHours = cooldown;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref maxMessagesPerDay, "maxMessagesPerDay", 1);
            Scribe_Values.Look(ref cooldownHours, "cooldownHours", 12f);
            Scribe_Collections.Look(ref allowedTriggers, "allowedTriggers", LookMode.Value);

            // Asegurar que el HashSet existe después de cargar
            if (Scribe.mode == LoadSaveMode.LoadingVars && allowedTriggers == null)
            {
                allowedTriggers = new HashSet<TriggerType>
                {
                    TriggerType.Incident,
                    TriggerType.CriticalNeed,
                    TriggerType.ColonySituation,
                    TriggerType.Random
                };
            }
        }

        public bool IsTriggerAllowed(TriggerType trigger)
        {
            return enabled && allowedTriggers.Contains(trigger);
        }

        public void SetTriggerAllowed(TriggerType trigger, bool allowed)
        {
            if (allowed)
                allowedTriggers.Add(trigger);
            else
                allowedTriggers.Remove(trigger);
        }

        /// <summary>
        /// Crea configuración por defecto desde settings globales
        /// </summary>
        public static ColonistMessageSettings CreateDefault()
        {
            return new ColonistMessageSettings
            {
                enabled = true,
                maxMessagesPerDay = MyMod.Settings?.defaultMaxMessagesPerColonistPerDay ?? 1,
                cooldownHours = MyMod.Settings?.defaultColonistCooldownHours ?? 12f,
                allowedTriggers = new HashSet<TriggerType>
                {
                    TriggerType.Incident,
                    TriggerType.CriticalNeed,
                    TriggerType.ColonySituation,
                    TriggerType.Random
                }
            };
        }

        public void ResetToDefaults()
        {
            enabled = true;
            maxMessagesPerDay = MyMod.Settings?.defaultMaxMessagesPerColonistPerDay ?? 1;
            cooldownHours = MyMod.Settings?.defaultColonistCooldownHours ?? 12f;
            allowedTriggers = new HashSet<TriggerType>
            {
                TriggerType.Incident,
                TriggerType.CriticalNeed,
                TriggerType.ColonySituation,
                TriggerType.Random
            };
        }
    }
}