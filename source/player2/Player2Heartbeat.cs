using UnityEngine;
using Verse;
using UnityEngine.Networking;
using System.Collections;
using RimWorld;

namespace EchoColony
{
    public class Player2Heartbeat : MonoBehaviour
    {
        private float timer = 0f;
        private const float Interval = 60f; // 60 segundos = 1 minuto
        private const float InitialCheckDelay = 5f; // Esperar 5 segundos al inicio
        private bool hasPerformedInitialCheck = false;
        private bool isCheckingPlayer2 = false;

        void Start()
        {
            // Realizar check inicial después de un pequeño delay
            StartCoroutine(InitialPlayer2Check());
        }

        void Update()
        {
            // ✅ CORRECTO: Solo hacer heartbeat si Player2 está seleccionado como fuente de modelo
            // Esto es para telemetría de uso, no para detección
            if (MyMod.Settings.modelSource != ModelSource.Player2) 
            {
                // ✅ IMPORTANTE: Resetear timer cuando no está en uso
                timer = 0f;
                return;
            }

            // ✅ VERIFICAR: Asegurar que Time.unscaledDeltaTime funciona correctamente
            float deltaTime = Time.unscaledDeltaTime;
            
            if (MyMod.Settings.debugMode && deltaTime > 0)
            {
                
            }

            timer += deltaTime;

            if (timer >= Interval)
            {
                if (MyMod.Settings.debugMode)
                {
                    Log.Message("[EchoColony] Sending Player2 usage heartbeat...");
                }
                
                timer = 0f; // ✅ Resetear ANTES de enviar
                PingPlayer2();
            }
        }

        private IEnumerator InitialPlayer2Check()
        {
            yield return new WaitForSeconds(InitialCheckDelay);
            
            if (!hasPerformedInitialCheck)
            {
                hasPerformedInitialCheck = true;
                CheckPlayer2Availability();
            }
        }

        private void CheckPlayer2Availability()
        {
            if (isCheckingPlayer2) return;
            
            isCheckingPlayer2 = true;
            
            string endpoint = "http://127.0.0.1:4315/v1/health";
            UnityWebRequest request = UnityWebRequest.Get(endpoint);
            request.SetRequestHeader("Accept", "application/json; charset=utf-8");
            request.SetRequestHeader("player2-game-key", "Rimworld-EchoColony");
            request.timeout = 3; // Timeout más corto para check inicial

            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            op.completed += OnInitialCheckCompleted;
        }

        private void OnInitialCheckCompleted(AsyncOperation op)
        {
            UnityWebRequestAsyncOperation webOp = op as UnityWebRequestAsyncOperation;
            UnityWebRequest request = webOp.webRequest;

            bool player2Available = false;

            if (request != null)
            {
                if (!request.isNetworkError && !request.isHttpError)
                {
                    player2Available = true;
                    
                    // Si Player2 está disponible y aún no está seleccionado como modelo, seleccionarlo automáticamente
                    if (MyMod.Settings.modelSource != ModelSource.Player2)
                    {
                        MyMod.Settings.modelSource = ModelSource.Player2;
                        // Notificar al jugador
                        Messages.Message("EchoColony: Player2 detected and selected automatically", MessageTypeDefOf.PositiveEvent);
                        
                        // ✅ NUEVO: Iniciar timer de telemetría cuando se auto-selecciona
                        timer = 0f;
                        if (MyMod.Settings.debugMode)
                        {
                            Log.Message("[EchoColony] Player2 usage tracking started");
                        }
                    }
                }

                request.Dispose();
            }

            isCheckingPlayer2 = false;

            if (MyMod.Settings.debugMode)
            {
                Log.Message("[EchoColony] Initial Player2 check completed. Available: " + player2Available);
            }
        }

        private void PingPlayer2()
        {
            if (isCheckingPlayer2) return;
            
            // ✅ VERIFICAR: Solo enviar si Player2 sigue seleccionado
            if (MyMod.Settings.modelSource != ModelSource.Player2) 
            {
                if (MyMod.Settings.debugMode)
                {
                    Log.Message("[EchoColony] Cancelling heartbeat - Player2 no longer selected");
                }
                return;
            }
            
            isCheckingPlayer2 = true;
            
            string endpoint = "http://127.0.0.1:4315/v1/health";
            UnityWebRequest request = UnityWebRequest.Get(endpoint);
            request.SetRequestHeader("Accept", "application/json; charset=utf-8");
            request.SetRequestHeader("player2-game-key", "Rimworld-EchoColony");
            
            // ✅ NUEVO: Agregar header de telemetría para indicar uso activo
            request.SetRequestHeader("player2-usage-heartbeat", "true");
            request.SetRequestHeader("player2-session-time", $"{Time.realtimeSinceStartup:F0}");
            
            request.timeout = 5;

            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            op.completed += OnPingCompleted;
        }

        private void OnPingCompleted(AsyncOperation op)
        {
            UnityWebRequestAsyncOperation webOp = op as UnityWebRequestAsyncOperation;
            UnityWebRequest request = webOp.webRequest;

            if (request != null)
            {
                if (request.isNetworkError || request.isHttpError)
                {
                    if (MyMod.Settings.debugMode)
                    {
                        Log.Warning($"[EchoColony] Player2 usage heartbeat failed: {request.error}");
                    }
                    
                    // ✅ OPCIONAL: Desactivar automáticamente si falla repetidamente
                    // MyMod.Settings.modelSource = ModelSource.Gemini;
                }
                else
                {
                    if (MyMod.Settings.debugMode)
                    {
                        Log.Message("[EchoColony] Player2 usage heartbeat sent successfully");
                    }
                }

                request.Dispose();
            }
            
            isCheckingPlayer2 = false;
        }

        // Método público para forzar un nuevo check (útil para botones en la UI)
        public void ForceCheckPlayer2()
        {
            if (!isCheckingPlayer2)
            {
                CheckPlayer2Availability();
            }
        }
        
        // ✅ NUEVO: Método para obtener tiempo de uso actual
        public float GetCurrentUsageTime()
        {
            return MyMod.Settings.modelSource == ModelSource.Player2 ? timer : 0f;
        }
        
        // ✅ NUEVO: Método para forzar heartbeat inmediato (útil para testing)
        public void ForceSendHeartbeat()
        {
            if (MyMod.Settings.modelSource == ModelSource.Player2 && !isCheckingPlayer2)
            {
                timer = 0f;
                PingPlayer2();
            }
        }
    }
}