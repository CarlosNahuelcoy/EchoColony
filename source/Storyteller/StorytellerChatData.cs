using System.Collections.Generic;
using Verse;

namespace EchoColony
{
    /// <summary>
    /// Historial de chat de un storyteller. Envoltorio IExposable porque
    /// Scribe no puede guardar un List&lt;string&gt; con LookMode.Deep.
    /// </summary>
    public class StorytellerChatHistory : IExposable
    {
        public List<string> messages = new List<string>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref messages, "messages", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && messages == null)
            {
                messages = new List<string>();
            }
        }
    }

    /// <summary>
    /// GameComponent que persiste el historial de chat con cada storyteller por separado
    /// </summary>
    public class StorytellerChatData : GameComponent
    {
        // Diccionario: defName del storyteller -> historial de chat
        private Dictionary<string, StorytellerChatHistory> chatHistoryByStoryteller = new Dictionary<string, StorytellerChatHistory>();

        // Listas de trabajo para Scribe
        private List<string> tmpKeys;
        private List<StorytellerChatHistory> tmpValues;

        public StorytellerChatData(Game game) : base()
        {
        }

        public List<string> GetChatHistory(string storytellerDefName)
        {
            if (chatHistoryByStoryteller == null)
                chatHistoryByStoryteller = new Dictionary<string, StorytellerChatHistory>();

            if (!chatHistoryByStoryteller.TryGetValue(storytellerDefName, out var history) || history == null)
            {
                history = new StorytellerChatHistory();
                chatHistoryByStoryteller[storytellerDefName] = history;
            }

            if (history.messages == null)
                history.messages = new List<string>();

            return history.messages;
        }

        public void AddMessage(string storytellerDefName, string message)
        {
            GetChatHistory(storytellerDefName).Add(message);
        }

        public void ClearHistory(string storytellerDefName)
        {
            if (chatHistoryByStoryteller == null)
                chatHistoryByStoryteller = new Dictionary<string, StorytellerChatHistory>();

            if (chatHistoryByStoryteller.TryGetValue(storytellerDefName, out var history))
            {
                history?.messages?.Clear();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // Nota: versiones anteriores guardaban Dictionary<string, List<string>> con LookMode.Deep
            // bajo "storytellerChatHistories". List<string> no es IExposable, así que Scribe nunca
            // escribió los valores (solo las claves). Ese nodo se ignora a propósito: no contiene
            // historial recuperable y leerlo solo produciría errores de carga.
            Scribe_Collections.Look(ref chatHistoryByStoryteller, "storytellerChatHistoriesV2",
                LookMode.Value, LookMode.Deep, ref tmpKeys, ref tmpValues);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (chatHistoryByStoryteller == null)
                {
                    chatHistoryByStoryteller = new Dictionary<string, StorytellerChatHistory>();
                }
            }
        }
    }
}
