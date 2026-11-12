using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FishOut.Sdk.Editor
{
    // Menu FishOut > SDK > Open SDK Config, plus a pre-build check that reminds about an empty token or Sandbox mode.
    public sealed class FishOutSdkMenu : IPreprocessBuildWithReport
    {
        private const string AssetPath = "Assets/FishOut/Sdk/Resources/SdkConfig.asset";
        public int callbackOrder => 0;

        [MenuItem("FishOut/SDK/Open SDK Config")]
        public static void Open()
        {
            var config = FindOrCreate();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        public static FishOutSdkConfig FindOrCreate()
        {
            var config = AssetDatabase.LoadAssetAtPath<FishOutSdkConfig>(AssetPath);
            if (config) return config;
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            config = ScriptableObject.CreateInstance<FishOutSdkConfig>();
            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            var config = AssetDatabase.LoadAssetAtPath<FishOutSdkConfig>(AssetPath);
            if (!config) { Debug.LogWarning("[SDK] " + AssetPath + " is missing; Adjust will not start in this build."); return; }
            if (!config.HasAdjust) Debug.LogWarning("[SDK] Adjust app token is empty; Adjust will be skipped in this build.");
            else if (config.adjustEnvironment == SdkEnvironment.Sandbox) Debug.LogWarning("[SDK] Adjust is in Sandbox; switch to Production for the store build.");
        }
    }
}
