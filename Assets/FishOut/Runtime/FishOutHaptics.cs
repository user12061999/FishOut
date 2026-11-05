using UnityEngine;
namespace FishOut
{
    public static class FishOutHaptics
    {
        // A short tick for ordinary taps; Handheld.Vibrate stays reserved for crab hits.
        public static void Tap()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using(var version=new AndroidJavaClass("android.os.Build$VERSION"))
            {
                if(version.GetStatic<int>("SDK_INT")<26)return;
            }
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var vibrator=activity.Call<AndroidJavaObject>("getSystemService","vibrator"))
            using(var effects=new AndroidJavaClass("android.os.VibrationEffect"))
            using(var effect=effects.CallStatic<AndroidJavaObject>("createOneShot",12L,-1))
            {
                vibrator.Call("vibrate",effect);
            }
#endif
        }
    }
}
