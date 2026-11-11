using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FishOut.Editor
{
    // Points the HUD skin at sprites of the GUI Pro-SuperCasual pack already in the project.
    public static class HudSkinBuilder
    {
        private const string AssetPath = "Assets/FishOut/Resources/UI/HudSkin.asset";
        private const string Root = "Assets/GUI Pro-SuperCasual/ResourcesData/Sprites/Components/";

        [MenuItem("Fish Out/Cập nhật skin HUD (GUI Pro)")]
        public static void Build()
        {
            var skin = AssetDatabase.LoadAssetAtPath<HudSkin>(AssetPath);
            if (!skin) { skin = ScriptableObject.CreateInstance<HudSkin>(); AssetDatabase.CreateAsset(skin, AssetPath); }
            skin.topButton = Load("Button/Button01_s_Blue.png");
            skin.plate = Load("Button/Button01_s_DarkGray.png");
            skin.hintButton = Load("Button/Button_SkillBtn_Orange.png");
            skin.rescueButton = Load("Button/Button_SkillBtn_Blue.png");
            skin.badge = Load("Label/Label_Badge01_Red.png");
            skin.bubble = Load("Label/Label_Bubble_Bg.png");
            skin.heart = Load("Icon_ItemIcons/128/ItemIcon_Heart_Red.Png");
            skin.heartSpent = Load("Icon_ItemIcons/128/ItemIcon_Heart_Dimmed.Png");
            skin.settingsIcon = Load("Icon_PictoIcons/128/PictoIcon_Setting.Png");
            skin.retryIcon = Load("Icon_PictoIcons/128/PictoIcon_Refresh_1.Png");
            skin.fishIcon = Load("Icon_PictoIcons/128/PictoIcon_Fish.Png");
            skin.hintIcon = Load("Icon_PictoIcons/128/PictoIcon_Bulb.Png");
            skin.rescueIcon = Load("Icon_PictoIcons/128/PictoIcon_Fishing.Png");
            skin.popup = Load("Popup/Popup01_Basic_Single.png");
            skin.ribbon = Load("Label/Title_Ribbon01_Blue.png");
            skin.primaryButton = Load("Button/Button01_l_Yellow.png");
            skin.secondaryButton = Load("Button/Button01_l_Sky.png");
            skin.levelButton = Load("Button/Button01_s_Blue.png");
            skin.currentLevelButton = Load("Button/Button01_s_Yellow.png");
            skin.toggleBg = Load("Button/Button01_s_DarkGray.png");
            skin.toggleCheck = Load("UI_Etc/Toggle_Check_Single_Icon.png");
            skin.claimButton = Load("Button/Button01_l_Purple.png");
            skin.coin = Load("Icon_ItemIcons/256/ItemIcon_Money_Coin.Png");
            skin.tutorialHand = Load("Icon_TutorialHand/512/tutorial_hand_3.png");
            skin.whiteTile = Load("Button/Button_Round02_White_Bg.png");
            skin.whitePill = Load("Button/Button01_l_White_Bg.png");
            skin.whitePanel = Load("Popup/Popup01_Basic_White_Bg.png");
            skin.levelRibbon = Load("Label/Title_Ribbon01_Purple.png");
            skin.homeIcon = Load("Icon_PictoIcons/128/PictoIcon_Home_1.Png");
            skin.circle = Load("Button/Button_Circle74_Bg.png");
            skin.soundIcon = Load("Icon_PictoIcons/128/PictoIcon_Sound.Png");
            skin.soundOffIcon = Load("Icon_PictoIcons/128/PictoIcon_Sound_Off.Png");
            skin.musicIcon = Load("Icon_PictoIcons/128/PictoIcon_Music_1.Png");
            skin.musicOffIcon = Load("Icon_PictoIcons/128/PictoIcon_Music_1_Off.Png");
            skin.vibrationIcon = Load("Icon_PictoIcons/128/PictoIcon_Phone.Png");
            skin.playIcon = Load("Icon_PictoIcons/128/PictoIcon_Play_1.Png");
            skin.levelsIcon = Load("Icon_PictoIcons/128/PictoIcon_Map_1.Png");
            skin.lockIcon = Load("Icon_PictoIcons/128/PictoIcon_Lock_1.Png");
            EditorUtility.SetDirty(skin); AssetDatabase.SaveAssetIfDirty(skin);
            Debug.Log("Fish Out: HUD skin updated at " + AssetPath);
        }
        private static Sprite Load(string path)
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(Root + path).OfType<Sprite>().FirstOrDefault();
            if (!sprite) Debug.LogWarning("Fish Out: missing HUD sprite " + Root + path);
            return sprite;
        }
    }
}
