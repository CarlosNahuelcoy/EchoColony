using Verse;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;

namespace EchoColony
{
    public class ChatGameComponent : GameComponent
    {
        private Dictionary<string, List<string>> savedChats = new Dictionary<string, List<string>>();
        public static ChatGameComponent Instance => Current.Game.GetComponent<ChatGameComponent>();

        private Dictionary<string, string> pawnVoiceMap = new Dictionary<string, string>();
        
        // ✅ AGREGAR: Tracking de fechas por pawn
        private Dictionary<string, int> lastChatDay = new Dictionary<string, int>();

        public ChatGameComponent(Game game) { }

        public List<string> GetChat(Pawn pawn)
        {
            string key = pawn.ThingID;
            if (!savedChats.ContainsKey(key))
                savedChats[key] = new List<string>();

            return savedChats[key];
        }

        // ✅ MODIFICAR: Agregar separadores de fecha automáticamente
        public void AddLine(Pawn pawn, string line)
        {
            string key = pawn.ThingID;
            if (!savedChats.ContainsKey(key))
                savedChats[key] = new List<string>();

            int currentDay = GenDate.DaysPassed;
            
            // Verificar si es un nuevo día desde la última conversación
            if (!lastChatDay.ContainsKey(key) || lastChatDay[key] != currentDay)
            {
                // Agregar separador de fecha
                string dateHeader = GetFormattedDateHeader(currentDay);
                savedChats[key].Add($"[DATE_SEPARATOR] {dateHeader}");
                lastChatDay[key] = currentDay;
            }

            savedChats[key].Add(line);
        }

        // ✅ AGREGAR: Método para formatear fecha
        private string GetFormattedDateHeader(int day)
{
    // Usar el formato nativo de RimWorld (ya completamente localizado)
    string nativeDate = GenDate.DateFullStringWithHourAt(GenTicks.TicksAbs, Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile));
    string[] parts = nativeDate.Split(' ');
    string dateOnly = parts.Length >= 3 ? $"{parts[0]} {parts[1]} {parts[2]}" : nativeDate;
    
    return $"--- {dateOnly} ---";
}

        // ✅ MODIFICAR: Limpiar también el tracking de fechas
        public void ClearChat(Pawn pawn)
        {
            string key = pawn.ThingID;
            if (savedChats.ContainsKey(key))
            {
                savedChats[key].Clear();
            }
            
            // Limpiar también el tracking de fecha
            if (lastChatDay.ContainsKey(key))
            {
                lastChatDay.Remove(key);
            }
        }

        // ✅ MODIFICAR: Guardar también el tracking de fechas
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref savedChats, "savedChats", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref lastChatDay, "lastChatDay", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref pawnVoiceMap, "pawnVoiceMap", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (savedChats == null)
                    savedChats = new Dictionary<string, List<string>>();
                    
                if (lastChatDay == null)
                    lastChatDay = new Dictionary<string, int>();
                    
                if (pawnVoiceMap == null)
                    pawnVoiceMap = new Dictionary<string, string>();
            }
        }

        public void SetVoiceForPawn(Pawn pawn, string voiceId)
        {
            pawnVoiceMap[pawn.ThingID.ToString()] = voiceId;
        }

        public string GetVoiceForPawn(Pawn pawn)
        {
            return pawnVoiceMap.TryGetValue(pawn.ThingID.ToString(), out var voiceId) ? voiceId : null;
        }

        // 🔄 Este método se ejecuta automáticamente cuando se carga la partida
        public override void FinalizeInit()
        {
            base.FinalizeInit();

            if (MyMod.Settings.enableTTS) // Asegura que TTS esté activo
            {
                foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonists)
                {
                    if (ColonistVoiceManager.HasVoice(pawn))
                    {
                        string savedVoice = ColonistVoiceManager.GetVoice(pawn);
                        if (!string.IsNullOrEmpty(savedVoice))
                        {
                            SetVoiceForPawn(pawn, savedVoice);
                        }
                    }
                }
            }
        }
    }
}