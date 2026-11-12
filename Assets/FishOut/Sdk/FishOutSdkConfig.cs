using UnityEngine;

namespace FishOut.Sdk
{
    public enum SdkEnvironment { Sandbox, Production }

    // Third-party IDs for FishOut. Fill them in the Inspector (Assets/FishOut/Sdk/Resources/SdkConfig.asset).
    [CreateAssetMenu(fileName = "SdkConfig", menuName = "FishOut/SDK Config")]
    public sealed class FishOutSdkConfig : ScriptableObject
    {
        public const string ResourcePath = "SdkConfig";

        [Header("Adjust")]
        [Tooltip("App token from the Adjust dashboard (12 characters).")]
        public string adjustAppToken = "";
        [Tooltip("Sandbox while testing, Production for the store build.")]
        public SdkEnvironment adjustEnvironment = SdkEnvironment.Sandbox;
        [Tooltip("Verbose Adjust logs (only used in Sandbox).")]
        public bool adjustVerboseLogging = true;

        [Header("Startup")]
        [Tooltip("Initialise the SDK in the Unity Editor too (it only logs there).")]
        public bool initializeInEditor;

        public string AdjustAppToken => (adjustAppToken ?? "").Trim();
        public bool HasAdjust => AdjustAppToken.Length > 0;

        public static FishOutSdkConfig Load() => Resources.Load<FishOutSdkConfig>(ResourcePath);
    }
}
