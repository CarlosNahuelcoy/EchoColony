using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Verse;
using System.IO;

namespace EchoColony
{
    public static class TTSManager
    {
        private const string TTS_ENDPOINT = "http://127.0.0.1:4315/v1/tts/speak";

        public static IEnumerator Speak(string text, string voiceId, string gender = "female", string lang = "en_US", float speed = 1f)
        {
            // Validación de entrada
            if (string.IsNullOrEmpty(text))
            {
                Log.Warning("[EchoColony] TTS: Text is empty");
                yield break;
            }

            if (string.IsNullOrEmpty(voiceId))
            {
                Log.Warning("[EchoColony] TTS: VoiceId is empty");
                yield break;
            }

            Log.Message($"[EchoColony] TTS: Speaking text with voice '{voiceId}': {text.Substring(0, Mathf.Min(50, text.Length))}...");

            var requestPayload = new TTSRequest
            {
                audio_format = "mp3",
                play_in_app = true,
                speed = speed,
                text = text,
                voice_gender = gender,
                voice_language = lang,
                voice_ids = new List<string> { voiceId }
            };

            string json = JsonUtility.ToJson(requestPayload);

            // Debug mejorado
            Log.Message($"[EchoColony] TTS JSON payload: {json}");

            // Guardar JSON en un archivo de texto (opcional para debug)
            if (MyMod.Settings.debugMode) // Asumiendo que tienes esta opción
            {
                try
                {
                    string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
                    string filePath = Path.Combine(desktopPath, "EchoColony_TTS_Debug.json");
                    File.WriteAllText(filePath, json);
                }
                catch (System.Exception ex)
                {
                    Log.Warning("[EchoColony] No se pudo guardar JSON en el escritorio: " + ex.Message);
                }
            }

            var req = new UnityWebRequest(TTS_ENDPOINT, "POST")
            {
                uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("player2-game-key", "Rimworld-EchoColony");

            yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                Log.Error($"[EchoColony] TTS failed: {req.error}");
                Log.Error($"[EchoColony] TTS Response Code: {req.responseCode}");
                Log.Error($"[EchoColony] TTS Response: {req.downloadHandler.text}");
            }
            else
            {
                Log.Message("[EchoColony] TTS request completed successfully");
            }

            req.Dispose();
        }

        [System.Serializable]
        public class TTSRequest
        {
            public string audio_format;
            public bool play_in_app;
            public float speed;
            public string text;
            public string voice_gender;
            public string voice_language;
            public List<string> voice_ids;
        }
    }
}