using UnityEditor;
using UnityEngine;

namespace FishOut.Editor
{
    // Lets anyone replay the game from a first launch: Home shows Level 1 and only Level 1 is unlocked.
    public static class FishOutProgressMenu
    {
        [MenuItem("Fish Out/Chơi lại từ đầu (xoá tiến trình)")]
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey("FishOut.Level");
            PlayerPrefs.DeleteKey("FishOut.Unlocked");
            PlayerPrefs.Save();
            Debug.Log("Fish Out: progress cleared. Press Play to start from Home, Level 1.");
        }
    }
}
