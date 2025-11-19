using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

namespace EchoColony
{
    public static class TTSVoiceCache
    {
        public static List<TTSVoice> Voices = new List<TTSVoice>();
        public static bool IsLoading = false;
        public static bool HasLoaded = false;

        public static IEnumerator LoadVoices()
        {
            if (IsLoading)
            {
                Log.Message("[EchoColony] Voice loading already in progress...");
                yield break;
            }

            IsLoading = true;
            Log.Message("[EchoColony] Starting to load TTS voices...");

            UnityWebRequest req = UnityWebRequest.Get("http://127.0.0.1:4315/v1/tts/voices");
            req.SetRequestHeader("accept", "application/json; charset=utf-8");
            req.SetRequestHeader("player2-game-key", "Rimworld-EchoColony");

            yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                Log.Error($"[EchoColony] ❌ Error loading TTS voices: {req.error}");
                Log.Error($"[EchoColony] Response code: {req.responseCode}");
                Log.Error($"[EchoColony] Response text: {req.downloadHandler.text}");
                IsLoading = false;
                yield break;
            }

            string json = req.downloadHandler.text;
            Log.Message($"[EchoColony] Raw TTS response: {json}");

            try
            {
                var parsed = SimpleJSON.JSON.Parse(json);
                
                if (parsed == null)
                {
                    Log.Error("[EchoColony] ❌ Failed to parse JSON response");
                    IsLoading = false;
                    yield break;
                }

                if (parsed["voices"] == null)
                {
                    Log.Error("[EchoColony] ❌ No 'voices' field in response");
                    Log.Error($"[EchoColony] Available fields: {string.Join(", ", parsed.Keys)}");
                    IsLoading = false;
                    yield break;
                }

                Voices.Clear();
                int voiceCount = 0;

                foreach (SimpleJSON.JSONNode node in parsed["voices"].AsArray)
                {
                    var voice = new TTSVoice
                    {
                        id = node["id"],
                        name = node["name"],
                        gender = node["gender"],
                        language = node["language"]
                    };
                    
                    Voices.Add(voice);
                    voiceCount++;
                    
                    Log.Message($"[EchoColony] Loaded voice: {voice.name} ({voice.id}) [{voice.language}]");
                }

                HasLoaded = true;
                Log.Message($"[EchoColony] ✅ Successfully loaded {voiceCount} TTS voices");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[EchoColony] ❌ Exception parsing voices: {ex.Message}");
                Log.Error($"[EchoColony] Stack trace: {ex.StackTrace}");
            }
            finally
            {
                IsLoading = false;
                req.Dispose();
            }
        }

        // Método para forzar recarga
        public static IEnumerator ReloadVoices()
        {
            HasLoaded = false;
            Voices.Clear();
            yield return LoadVoices();
        }

        public class TTSVoice
        {
            public string id;
            public string name;
            public string gender;
            public string language;
        }
    }
}