using System.Collections.Generic;
using Verse;

namespace EchoColony
{
    /// <summary>
    /// GameComponent que persiste el historial de chat con cada storyteller por separado
    /// </summary>
    public class StorytellerChatData : GameComponent
    {
        // Diccionario: defName del storyteller -> historial de chat
        private Dictionary<string, List<string>> chatHistoryByStoryteller = new Dictionary<string, List<string>>();
        
        public StorytellerChatData(Game game) : base()
        {
        }

        public List<string> GetChatHistory(string storytellerDefName)
        {
            if (chatHistoryByStoryteller == null)
                chatHistoryByStoryteller = new Dictionary<string, List<string>>();
                
            if (!chatHistoryByStoryteller.ContainsKey(storytellerDefName))
            {
                chatHistoryByStoryteller[storytellerDefName] = new List<string>();
            }
            
            return chatHistoryByStoryteller[storytellerDefName];
        }

        public void AddMessage(string storytellerDefName, string message)
        {
            if (chatHistoryByStoryteller == null)
                chatHistoryByStoryteller = new Dictionary<string, List<string>>();
                
            if (!chatHistoryByStoryteller.ContainsKey(storytellerDefName))
            {
                chatHistoryByStoryteller[storytellerDefName] = new List<string>();
            }
            
            chatHistoryByStoryteller[storytellerDefName].Add(message);
        }

        public void ClearHistory(string storytellerDefName)
        {
            if (chatHistoryByStoryteller == null)
                chatHistoryByStoryteller = new Dictionary<string, List<string>>();
                
            if (chatHistoryByStoryteller.ContainsKey(storytellerDefName))
            {
                chatHistoryByStoryteller[storytellerDefName].Clear();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            
            // Guardar el diccionario completo
            Scribe_Collections.Look(ref chatHistoryByStoryteller, "storytellerChatHistories", LookMode.Value, LookMode.Deep);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (chatHistoryByStoryteller == null)
                {
                    chatHistoryByStoryteller = new Dictionary<string, List<string>>();
                }
            }
        }
    }
}