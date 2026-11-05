using UnityEngine;

namespace FishOut
{
    public static class FishOutLevels
    {
        public const int Count = 10;
        public static LevelData Get(int number) => ShowcaseLevels.Get(Mathf.Clamp(number, 1, Count));
    }
}
