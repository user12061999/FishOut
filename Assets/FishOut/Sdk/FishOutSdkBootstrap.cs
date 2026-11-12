using AdjustSdk;
using UnityEngine;

namespace FishOut.Sdk
{
    // Starts Adjust once, before the first scene loads. Start-up only: no events or ads are sent from here.
    // An empty app token skips Adjust with a warning, so the game always runs.
    public sealed class FishOutSdkBootstrap : MonoBehaviour
    {
        public static bool AdjustStarted { get; private set; }

        private static FishOutSdkBootstrap instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (instance) return;
            var config = FishOutSdkConfig.Load();
            if (!config) { Debug.LogWarning("[SDK] SdkConfig asset not found in Resources; Adjust is not started."); return; }
            if (Application.isEditor && !config.initializeInEditor) return;
            var go = new GameObject("SdkBootstrap");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<FishOutSdkBootstrap>();
            StartAdjust(config);
        }

        private static void StartAdjust(FishOutSdkConfig config)
        {
            if (!config.HasAdjust) { Debug.LogWarning("[SDK] Adjust app token is empty; Adjust skipped."); return; }
            var production = config.adjustEnvironment == SdkEnvironment.Production;
            // Production allows the Suppress level, so a store build writes no Adjust logs at all.
            var adjustConfig = new AdjustConfig(config.AdjustAppToken, production ? AdjustEnvironment.Production : AdjustEnvironment.Sandbox, production);
            adjustConfig.LogLevel = production ? AdjustLogLevel.Suppress : (config.adjustVerboseLogging ? AdjustLogLevel.Verbose : AdjustLogLevel.Info);
            Adjust.InitSdk(adjustConfig);
            AdjustStarted = true;
        }
    }
}
