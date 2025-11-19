using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;
using System.Text.RegularExpressions;

namespace EchoColony
{
    public static class EventLogger
    {
        private const int MaxEvents = 1000;
        public static List<string> events = new List<string>();

        public static void LogEvent(string message)
        {
            try
            {
                if (Current.Game == null || Find.WorldGrid == null || Find.CurrentMap == null)
                    return;

                if (string.IsNullOrWhiteSpace(message))
                    return;

                int ticks = Find.TickManager.TicksGame;
                int tile = Find.CurrentMap.Tile;
                Vector2 longLat = Find.WorldGrid.LongLatOf(tile);

                string timestamp = GenDate.DateFullStringWithHourAt(ticks, longLat);
                string cleanMessage = StripRichText(message);
                string fullMessage = $"[{timestamp}] {cleanMessage}";

                // Asegura que no supere los 1000 eventos
                if (events.Count >= MaxEvents)
                {
                    events.RemoveRange(0, events.Count - MaxEvents + 1);
                }

                events.Add(fullMessage);

                // Guardar también en el componente persistente
                var comp = Current.Game.GetComponent<EventLogComponent>();
                if (comp != null)
                {
                    comp.savedEvents = new List<string>(events);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[EchoColony] Failed to log event: {ex.Message}");
            }
        }

        public static List<string> GetEvents()
        {
            // Devuelve una copia limpia, por si acaso
            List<string> cleaned = new List<string>();
            foreach (string e in events)
            {
                cleaned.Add(StripRichText(e));
            }
            return cleaned;
        }

        // Limpieza de etiquetas como <color=#...> y otras
        private static string StripRichText(string input)
        {
            return Regex.Replace(input, "<.*?>", string.Empty);
        }
    }
}
