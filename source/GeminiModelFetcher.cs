using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace EchoColony
{
    /// <summary>
    /// Helper MonoBehaviour to run coroutines from non-MonoBehaviour contexts (like mod settings)
    /// </summary>
    public class GeminiModelFetcher : MonoBehaviour
    {
        private static GeminiModelFetcher _instance;

        private static GeminiModelFetcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("EchoColony_ModelFetcher");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<GeminiModelFetcher>();
                }
                return _instance;
            }
        }

        public static void FetchModels(string apiKey, Action<List<GeminiModelInfo>> onComplete)
        {
            Instance.StartCoroutine(GeminiAPI.FetchAvailableModels(apiKey, onComplete));
        }
    }
}