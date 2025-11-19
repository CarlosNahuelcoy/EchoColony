using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Verse;
using System.Linq;

namespace EchoColony
{
   public static class Player2Importer
{
    private const string ENDPOINT = "http://127.0.0.1:4315/v1/selected_characters";

    public static IEnumerator LoadCharacters()
    {
        UnityWebRequest request = UnityWebRequest.Get(ENDPOINT);
        request.SetRequestHeader("accept", "application/json; charset=utf-8");
        request.SetRequestHeader("player2-game-key", "Rimworld-EchoColony");

        yield return request.SendWebRequest();

        if (request.isNetworkError || request.isHttpError)
        {
            Log.Warning("[EchoColony] Failed to fetch Player2 characters: " + request.error);
            yield break;
        }
        try
        {
            var response = request.downloadHandler.text;
            var parsed = SimpleJSON.JSON.Parse(response);
            var characters = parsed["characters"];

            Player2CharacterCache.Characters.Clear();

            foreach (var node in characters.Children)
            {
                Player2Character character = new Player2Character
                {
                    id = node["id"],
                    short_name = node["short_name"],
                    description = node["description"],
                    voice_id = node["voice_ids"]?[0]
                };

                Player2CharacterCache.Characters.Add(character);
            }

            Log.Message($"[EchoColony] Imported {Player2CharacterCache.Characters.Count} characters from Player2.");
        }
        catch (System.Exception ex)
        {
            Log.Error("[EchoColony] Error parsing Player2 response: " + ex.Message);
        }
    }
}

}
