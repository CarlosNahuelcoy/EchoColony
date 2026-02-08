using System;
using Verse;

namespace EchoColony.SpontaneousMessages
{
    /// <summary>
    /// Modo de operación del sistema de mensajes espontáneos
    /// </summary>
    public enum SpontaneousMessageMode
    {
        Disabled,           // Sistema completamente desactivado
        RandomOnly,         // Solo mensajes casuales aleatorios
        IncidentsOnly,      // Solo reacciones a incidentes
        Full                // Ambos: aleatorio + incidentes
    }

    /// <summary>
    /// Tipo de trigger que genera el mensaje
    /// </summary>
    public enum TriggerType
    {
        Incident,           // Raids, meteoritos, traders, etc
        CriticalNeed,       // Hambre extrema, cansancio, etc (futuro)
        ColonySituation,    // Falta comida, medicina, etc (futuro)
        Random              // Conversación casual sin contexto específico
    }

    /// <summary>
    /// Incidentes específicos que pueden trigger mensajes
    /// </summary>
    public enum IncidentTrigger
    {
        // ALTA PRIORIDAD - Permiten hasta 2 colonos responder
        Raid,
        MechanoidCluster,
        InfestationSpawned,
        ToxicFallout,
        
        // PRIORIDAD MEDIA - Solo 1 colono responde
        MeteoriteIncoming,
        TraderCaravan,
        SolarFlare,
        Eclipse,
        Manhunter,
        
        // BAJA PRIORIDAD - 1 colono, probabilidad reducida
        WandererJoin,
        RefugeeChased,
        Aurora,
        TransportPodCrash,
        
        // Placeholder para incidentes no categorizados
        Other
    }

    /// <summary>
    /// Request para generar un mensaje espontáneo
    /// </summary>
    public struct MessageRequest
    {
        public Pawn colonist;
        public TriggerType triggerType;
        public IncidentTrigger incidentTrigger; // Solo relevante si triggerType == Incident
        public string contextDescription;
        public float urgency; // 0-1, determina prioridad de selección

        public MessageRequest(Pawn colonist, TriggerType triggerType, string context, float urgency = 0.5f)
        {
            this.colonist = colonist;
            this.triggerType = triggerType;
            this.incidentTrigger = IncidentTrigger.Other;
            this.contextDescription = context;
            this.urgency = urgency;
        }

        public MessageRequest(Pawn colonist, IncidentTrigger incident, string context, float urgency = 0.5f)
        {
            this.colonist = colonist;
            this.triggerType = TriggerType.Incident;
            this.incidentTrigger = incident;
            this.contextDescription = context;
            this.urgency = urgency;
        }
    }

    /// <summary>
    /// Helper methods para IncidentTrigger
    /// </summary>
    public static class IncidentTriggerExtensions
    {
        public static bool IsHighPriority(this IncidentTrigger trigger)
        {
            return trigger == IncidentTrigger.Raid ||
                   trigger == IncidentTrigger.MechanoidCluster ||
                   trigger == IncidentTrigger.InfestationSpawned ||
                   trigger == IncidentTrigger.ToxicFallout;
        }

        public static bool IsMediumPriority(this IncidentTrigger trigger)
        {
            return trigger == IncidentTrigger.MeteoriteIncoming ||
                   trigger == IncidentTrigger.TraderCaravan ||
                   trigger == IncidentTrigger.SolarFlare ||
                   trigger == IncidentTrigger.Eclipse ||
                   trigger == IncidentTrigger.Manhunter;
        }

        public static bool IsLowPriority(this IncidentTrigger trigger)
        {
            return trigger == IncidentTrigger.WandererJoin ||
                   trigger == IncidentTrigger.RefugeeChased ||
                   trigger == IncidentTrigger.Aurora ||
                   trigger == IncidentTrigger.TransportPodCrash;
        }

        /// <summary>
        /// Número máximo de colonos que pueden responder a este incidente
        /// </summary>
        public static int MaxResponders(this IncidentTrigger trigger)
        {
            if (trigger.IsHighPriority()) return 2;
            return 1;
        }

        /// <summary>
        /// Probabilidad base de que un colono elegible quiera responder
        /// </summary>
        public static float BaseResponseChance(this IncidentTrigger trigger)
        {
            if (trigger.IsHighPriority()) return 0.8f;
            if (trigger.IsMediumPriority()) return 0.6f;
            if (trigger.IsLowPriority()) return 0.4f;
            return 0.5f;
        }
    }
}