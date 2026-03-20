using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Verse;
using RimWorld;
using SimpleJSON;

namespace EchoColony
{
    /// <summary>
    /// Manages Player2 Web API authentication.
    ///
    /// All traffic always goes to https://api.player2.game/v1
    ///
    /// Key acquisition order:
    ///   1. Auto (silent) — if local Player2 App is running, call
    ///                      POST localhost:4315/v1/login/web/{clientId} to get a key
    ///                      invisibly, no user interaction needed.
    ///   2. Device Code   — if app is not running, show a code + URL dialog in-game
    ///                      and open the browser. Polls until approved.
    ///
    /// Once a p2Key is obtained it is stored in GeminiSettings and sent as
    /// "Authorization: Bearer {p2Key}" on every request to the Web API.
    /// </summary>
    public static class Player2AuthManager
    {
        public const string ClientId   = "01977e1d-a813-7a50-a401-6be4fabc6b7f";
        public const string WebApiBase = "https://api.player2.game/v1";
        public const string LocalBase  = "http://127.0.0.1:4315/v1";

        public static bool   IsAuthenticating         { get; private set; } = false;
        public static string PendingUserCode           { get; private set; } = "";
        public static string PendingVerificationUri    { get; private set; } = "";

        /// How the current session was established — shown in Mod Settings UI
        public static string ConnectionMethod { get; private set; } = "";

        public static bool IsAuthenticated =>
            !string.IsNullOrEmpty(MyMod.Settings?.player2ApiKey);

        public static string GetAuthHeader() =>
            IsAuthenticated ? $"Bearer {MyMod.Settings.player2ApiKey}" : null;

        // ── Entry points ──────────────────────────────────────────────────────────

        /// Called automatically on startup. Tries local app silently first.
        public static IEnumerator AuthenticateAuto(Action<bool> onComplete = null)
        {
            if (IsAuthenticating) yield break;
            IsAuthenticating = true;

            Log.Message("[EchoColony] Player2: Attempting silent auto-auth via local app...");

            bool localSuccess = false;
            yield return TryLocalAppAuth(success => localSuccess = success, silent: true);

            if (localSuccess)
            {
                IsAuthenticating = false;
                onComplete?.Invoke(true);
                yield break;
            }

            Log.Message("[EchoColony] Player2: Local app not found. Manual browser auth available in Mod Settings.");
            IsAuthenticating = false;
            onComplete?.Invoke(false);
        }

        /// Triggered by user clicking "Connect via Player2 App" in settings.
        public static IEnumerator AuthenticateViaLocalApp(Action<bool> onComplete = null)
        {
            if (IsAuthenticating) yield break;
            IsAuthenticating = true;
            yield return TryLocalAppAuth(onComplete, silent: false);
            IsAuthenticating = false;
        }

        /// Triggered by user clicking "Connect via Browser" in settings.
        public static IEnumerator AuthenticateViaBrowser(Action<bool> onComplete = null)
        {
            if (IsAuthenticating) yield break;
            IsAuthenticating = true;
            yield return StartDeviceCodeFlow(onComplete);
            IsAuthenticating = false;
        }

        /// Clear stored credentials.
        public static void Disconnect()
        {
            if (MyMod.Settings == null) return;
            MyMod.Settings.player2ApiKey = "";
            MyMod.Settings.Write();
            PendingUserCode        = "";
            PendingVerificationUri = "";
            ConnectionMethod       = "";
            Log.Message("[EchoColony] Player2: Disconnected from Web API");
            Messages.Message("EchoColony: Disconnected from Player2 Web API",
                MessageTypeDefOf.SilentInput, false);
        }

        // ── Stored key validation (called by Heartbeat on startup) ────────────────

        public static void OnStoredKeyValidated()
        {
            ConnectionMethod = "Saved session";
            Messages.Message("EchoColony: Connected to Player2 Web API (saved session)",
                MessageTypeDefOf.SilentInput, false);
        }

        // ── Local App auth ────────────────────────────────────────────────────────

        private static IEnumerator TryLocalAppAuth(Action<bool> onComplete, bool silent)
        {
            var healthReq    = UnityWebRequest.Get($"{LocalBase}/health");
            healthReq.timeout = 3;
            yield return healthReq.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            bool appRunning = healthReq.result == UnityWebRequest.Result.Success;
#else
            bool appRunning = !healthReq.isNetworkError && !healthReq.isHttpError;
#endif

            if (!appRunning)
            {
                if (!silent)
                    Messages.Message(
                        "EchoColony: Player2 App not found. Make sure it's open and try again.",
                        MessageTypeDefOf.RejectInput, false);
                onComplete?.Invoke(false);
                yield break;
            }

            var loginReq = new UnityWebRequest($"{LocalBase}/login/web/{ClientId}", "POST")
            {
                uploadHandler   = new UploadHandlerRaw(new byte[0]),
                downloadHandler = new DownloadHandlerBuffer()
            };
            loginReq.SetRequestHeader("Content-Type", "application/json");
            loginReq.SetRequestHeader("player2-game-key", "Rimworld-EchoColony");
            loginReq.timeout = 5;

            yield return loginReq.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            bool loginOk = loginReq.result == UnityWebRequest.Result.Success;
#else
            bool loginOk = !loginReq.isNetworkError && !loginReq.isHttpError;
#endif

            if (!loginOk)
            {
                Log.Warning($"[EchoColony] Player2 local login failed: {loginReq.error}");
                if (!silent)
                    Messages.Message(
                        "EchoColony: Player2 App found but login failed. Try restarting the app.",
                        MessageTypeDefOf.RejectInput, false);
                onComplete?.Invoke(false);
                yield break;
            }

            try
            {
                var    parsed = JSON.Parse(loginReq.downloadHandler.text);
                string key    = parsed["p2Key"]?.Value;

                if (string.IsNullOrEmpty(key))
                {
                    Log.Warning("[EchoColony] Player2 local login: no p2Key in response");
                    onComplete?.Invoke(false);
                    yield break;
                }

                StoreKey(key);
                ConnectionMethod = "Player2 App (local)";

                if (!silent)
                    Messages.Message("EchoColony: Connected to Player2 via local app!",
                        MessageTypeDefOf.PositiveEvent, false);

                Log.Message("[EchoColony] Player2: Auto-auth successful via local app");
                onComplete?.Invoke(true);
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Player2 local login parse error: {ex.Message}");
                onComplete?.Invoke(false);
            }
        }

        // ── Device Code flow ──────────────────────────────────────────────────────

        private static IEnumerator StartDeviceCodeFlow(Action<bool> onComplete)
        {
            string body  = $"{{\"client_id\": \"{ClientId}\"}}";
            var newReq   = new UnityWebRequest($"{WebApiBase}/login/device/new", "POST")
            {
                uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            newReq.SetRequestHeader("Content-Type", "application/json");
            newReq.timeout = 10;

            yield return newReq.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            if (newReq.result != UnityWebRequest.Result.Success)
#else
            if (newReq.isNetworkError || newReq.isHttpError)
#endif
            {
                Log.Warning($"[EchoColony] Device code request failed: {newReq.error}");
                Messages.Message(
                    "EchoColony: Could not reach Player2 servers. Check your internet connection.",
                    MessageTypeDefOf.RejectInput, false);
                onComplete?.Invoke(false);
                yield break;
            }

            if (MyMod.Settings?.debugMode == true)
                Log.Message($"[EchoColony] Device code raw response: {newReq.downloadHandler.text}");

            string deviceCode;
            string userCode;
            string verificationUri;
            string verificationUriComplete;
            float  pollInterval;

            try
            {
                // Player2 uses camelCase field names in responses
                var parsed              = JSON.Parse(newReq.downloadHandler.text);
                deviceCode              = parsed["deviceCode"]?.Value ?? "";
                userCode                = parsed["userCode"]?.Value ?? "";
                verificationUri         = parsed["verificationUri"]?.Value ?? "https://player2.game";
                verificationUriComplete = parsed["verificationUriComplete"]?.Value ?? verificationUri;
                pollInterval            = parsed["interval"]?.AsFloat ?? 5f;

                if (string.IsNullOrEmpty(deviceCode) || string.IsNullOrEmpty(userCode))
                {
                    Log.Warning($"[EchoColony] Device code response missing required fields. Response: {newReq.downloadHandler.text}");
                    onComplete?.Invoke(false);
                    yield break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[EchoColony] Device code parse error: {ex.Message}");
                onComplete?.Invoke(false);
                yield break;
            }

            PendingUserCode        = userCode;
            PendingVerificationUri = verificationUri;

            Application.OpenURL(verificationUriComplete);
            Find.WindowStack.Add(new Dialog_Player2DeviceCode(userCode, verificationUri));

            Log.Message($"[EchoColony] Device Code flow started. Code: {userCode}");

            float elapsed = 0f;
            float timeout = 300f;

            while (elapsed < timeout)
            {
                yield return new WaitForSeconds(pollInterval);
                elapsed += pollInterval;

                // Poll body: snake_case for the field name (OAuth2 standard)
                string pollBody = $"{{\"client_id\": \"{ClientId}\", \"device_code\": \"{deviceCode}\", \"grant_type\": \"urn:ietf:params:oauth:grant-type:device_code\"}}";
                var pollReq     = new UnityWebRequest($"{WebApiBase}/login/device/token", "POST")
                {
                    uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(pollBody)),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                pollReq.SetRequestHeader("Content-Type", "application/json");
                pollReq.timeout = 10;

                yield return pollReq.SendWebRequest();

                if (MyMod.Settings?.debugMode == true)
                    Log.Message($"[EchoColony] Poll response {pollReq.responseCode}: {pollReq.downloadHandler.text}");

#if UNITY_2020_2_OR_NEWER
                if (pollReq.result != UnityWebRequest.Result.Success)
#else
                if (pollReq.isNetworkError || pollReq.isHttpError)
#endif
                {
                    // 400 = authorization_pending — normal while waiting
                    if (pollReq.responseCode == 400) continue;
                    Log.Warning($"[EchoColony] Poll error {pollReq.responseCode}: {pollReq.error}");
                    continue;
                }

                try
                {
                    var    parsed = JSON.Parse(pollReq.downloadHandler.text);
                    string key    = parsed["p2Key"]?.Value;

                    if (!string.IsNullOrEmpty(key))
                    {
                        StoreKey(key);
                        ConnectionMethod       = "Browser (web login)";
                        PendingUserCode        = "";
                        PendingVerificationUri = "";

                        Find.WindowStack.TryRemove(typeof(Dialog_Player2DeviceCode), false);

                        Messages.Message("EchoColony: Connected to Player2 via browser!",
                            MessageTypeDefOf.PositiveEvent, false);

                        Log.Message("[EchoColony] Player2: Device Code auth successful");
                        onComplete?.Invoke(true);
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[EchoColony] Poll parse error: {ex.Message}");
                }
            }

            PendingUserCode        = "";
            PendingVerificationUri = "";
            Find.WindowStack.TryRemove(typeof(Dialog_Player2DeviceCode), false);
            Messages.Message(
                "EchoColony: Player2 authentication timed out. Please try again.",
                MessageTypeDefOf.RejectInput, false);
            onComplete?.Invoke(false);
        }

        private static void StoreKey(string key)
        {
            if (MyMod.Settings == null) return;
            MyMod.Settings.player2ApiKey = key;
            MyMod.Settings.Write();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Device Code in-game dialog
    // ─────────────────────────────────────────────────────────────────────────────

    public class Dialog_Player2DeviceCode : Window
    {
        private readonly string _userCode;
        private readonly string _verificationUri;

        public override Vector2 InitialSize => new Vector2(480f, 260f);

        public Dialog_Player2DeviceCode(string userCode, string verificationUri)
        {
            _userCode               = userCode;
            _verificationUri        = verificationUri;
            doCloseX                = true;
            closeOnClickedOutside   = false;
            absorbInputAroundWindow = false;
            forcePause              = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "Connect to Player2");
            Text.Font = GameFont.Small;

            float y = 44f;

            GUI.color = Color.gray;
            Widgets.Label(new Rect(0f, y, inRect.width, 22f),
                "A browser window has been opened. Sign in to Player2 and");
            y += 22f;
            Widgets.Label(new Rect(0f, y, inRect.width, 22f),
                "enter the code below when prompted:");
            GUI.color   = Color.white;
            y          += 32f;

            Text.Font   = GameFont.Medium;
            GUI.color   = new Color(0.4f, 0.9f, 1f);
            Rect codeRect = new Rect(inRect.width / 2f - 80f, y, 160f, 40f);
            Widgets.DrawBoxSolid(codeRect, new Color(0.1f, 0.15f, 0.2f, 0.9f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(codeRect, _userCode);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color   = Color.white;
            Text.Font   = GameFont.Small;
            y          += 52f;

            GUI.color   = Color.gray;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(0f, y, inRect.width, 22f), _verificationUri);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color   = Color.white;
            y          += 34f;

            if (Widgets.ButtonText(new Rect(10f, y, 200f, 30f), "Open browser again"))
                Application.OpenURL(_verificationUri);

            if (Widgets.ButtonText(new Rect(inRect.width - 130f, y, 120f, 30f), "Cancel"))
                Close();

            y += 38f;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(0f, y, inRect.width, 22f),
                "This window closes automatically when you're logged in.");
            GUI.color = Color.white;
        }
    }
}