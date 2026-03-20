using UnityEngine;
using Verse;
using UnityEngine.Networking;
using System.Collections;
using RimWorld;

namespace EchoColony
{
    public class Player2Heartbeat : MonoBehaviour
    {
        private float timer              = 0f;
        private const float Interval     = 60f;
        private const float InitialDelay = 5f;

        private bool hasPerformedInitialAuth = false;
        private bool isPinging               = false;

        private int consecutiveFailures    = 0;
        private const int MAX_LOG_FAILURES = 3;

        void Start()
        {
            StartCoroutine(InitialAuthAndCheck());
        }

        void Update()
        {
            if (MyMod.Settings == null) return;
            if (MyMod.Settings.modelSource != ModelSource.Player2)
            {
                timer = 0f;
                return;
            }

            timer += Time.unscaledDeltaTime;

            if (timer >= Interval)
            {
                timer = 0f;
                StartCoroutine(PingWebApi());
            }
        }

        // ── Initial auth ──────────────────────────────────────────────────────────

        private IEnumerator InitialAuthAndCheck()
        {
            yield return new WaitForSeconds(InitialDelay);

            if (hasPerformedInitialAuth) yield break;
            hasPerformedInitialAuth = true;

            if (MyMod.Settings == null) yield break;
            if (MyMod.Settings.modelSource != ModelSource.Player2) yield break;

            // If we already have a stored key, validate it first
            if (Player2AuthManager.IsAuthenticated)
            {
                Log.Message("[EchoColony] Player2: Stored key found, validating...");
                bool valid = false;
                yield return ValidateStoredKey(ok => valid = ok);

                if (valid)
                {
                    Log.Message("[EchoColony] Player2: Stored key is valid");
                    Player2AuthManager.OnStoredKeyValidated();
                    yield break;
                }

                Log.Warning("[EchoColony] Player2: Stored key invalid or expired, re-authenticating...");
                MyMod.Settings.player2ApiKey = "";
            }

            // Try silent auto-auth via local app
            yield return Player2AuthManager.AuthenticateAuto(success =>
            {
                if (success)
                    Log.Message("[EchoColony] Player2: Silent auto-auth completed");
                else
                    Log.Message("[EchoColony] Player2: Auto-auth not available. " +
                                "User can connect manually in Mod Settings.");
            });
        }

        // ── Periodic health ping ──────────────────────────────────────────────────

        private IEnumerator PingWebApi()
        {
            if (isPinging) yield break;
            if (MyMod.Settings == null) yield break;
            if (MyMod.Settings.modelSource != ModelSource.Player2) yield break;

            isPinging = true;

            var request = UnityWebRequest.Get(Player2AuthManager.WebApiBase + "/health");
            request.SetRequestHeader("player2-game-key", "Rimworld-EchoColony");

            string authHeader = Player2AuthManager.GetAuthHeader();
            if (!string.IsNullOrEmpty(authHeader))
                request.SetRequestHeader("Authorization", authHeader);

            request.timeout = 5;
            yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            bool ok = request.result == UnityWebRequest.Result.Success;
#else
            bool ok = !request.isNetworkError && !request.isHttpError;
#endif

            if (ok)
            {
                consecutiveFailures = 0;
                if (MyMod.Settings.debugMode)
                    Log.Message("[EchoColony] Player2 Web API heartbeat OK");
            }
            else
            {
                consecutiveFailures++;

                // 401 = key expired → re-auth silently
                if (request.responseCode == 401 && !string.IsNullOrEmpty(MyMod.Settings.player2ApiKey))
                {
                    Log.Warning("[EchoColony] Player2: API key expired, attempting re-auth...");
                    MyMod.Settings.player2ApiKey = "";
                    hasPerformedInitialAuth = false;
                    StartCoroutine(InitialAuthAndCheck());
                }
                else if (MyMod.Settings.debugMode && consecutiveFailures <= MAX_LOG_FAILURES)
                {
                    Log.Warning($"[EchoColony] Player2 heartbeat failed " +
                                $"({consecutiveFailures}): {request.responseCode} {request.error}");
                }
            }

            request.Dispose();
            isPinging = false;
        }

        // ── Key validation ────────────────────────────────────────────────────────

        private IEnumerator ValidateStoredKey(System.Action<bool> onResult)
        {
            var request = UnityWebRequest.Get(Player2AuthManager.WebApiBase + "/health");
            request.SetRequestHeader("player2-game-key", "Rimworld-EchoColony");

            string authHeader = Player2AuthManager.GetAuthHeader();
            if (!string.IsNullOrEmpty(authHeader))
                request.SetRequestHeader("Authorization", authHeader);

            request.timeout = 5;
            yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            bool ok = request.result == UnityWebRequest.Result.Success;
#else
            bool ok = !request.isNetworkError && !request.isHttpError;
#endif

            request.Dispose();
            onResult?.Invoke(ok && request.responseCode != 401);
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void ForceCheckPlayer2()
        {
            hasPerformedInitialAuth = false;
            StartCoroutine(InitialAuthAndCheck());
        }
    }
}