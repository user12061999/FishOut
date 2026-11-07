using UnityEngine;
namespace FishOut
{
    // Sprites from the project's GUI Pro-SuperCasual pack for the in-game HUD (built by Fish Out > Cập nhật skin HUD).
    // Without the asset the HUD falls back to its own drawn shapes.
    public sealed class HudSkin : ScriptableObject
    {
        public const string Resource = "UI/HudSkin";
        public Sprite topButton, plate, hintButton, rescueButton, badge, bubble;
        public Sprite heart, heartSpent, settingsIcon, retryIcon, fishIcon, hintIcon, rescueIcon;
        // Dialogs, home and level select.
        public Sprite popup, ribbon, primaryButton, secondaryButton, levelButton, currentLevelButton, toggleBg, toggleCheck;
        private static HudSkin cached;
        public static HudSkin Load() => cached ? cached : cached = Resources.Load<HudSkin>(Resource);
    }
}
