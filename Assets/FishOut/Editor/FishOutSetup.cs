using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.Animations;
using System.Linq;

namespace FishOut.Editor
{
    public static class FishOutSetup
    {
        public const string ScenePath="Assets/FishOut/Scenes/FishOut.unity";
        [MenuItem("Fish Out/Create or update game scene")]
        public static void CreateScene()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before creating the scene.");
            Directory.CreateDirectory("Assets/FishOut/Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var background=new GameObject("Ocean background",typeof(Camera)).GetComponent<Camera>();
            background.depth=-10;background.cullingMask=0;background.clearFlags=CameraClearFlags.SolidColor;background.backgroundColor=new Color(.035f,.22f,.29f);
            var cameraObject=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));cameraObject.tag="MainCamera";
            var camera=cameraObject.GetComponent<Camera>();camera.transform.position=new Vector3(0,12,-9);camera.transform.LookAt(new Vector3(0,.2f,0));
            camera.orthographic=true;camera.orthographicSize=4.3f;camera.backgroundColor=new Color(.035f,.22f,.29f);camera.clearFlags=CameraClearFlags.SolidColor;
            var light=new GameObject("Sunlight",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;
            light.transform.rotation=Quaternion.Euler(50,-30,0);light.shadows=LightShadows.Soft;
            RenderSettings.ambientLight=new Color(.69f,.81f,.86f);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            var go=new GameObject("Fish Out Game");var game=go.AddComponent<FishOutGame>();
            const string root="Assets/Cube Animals 02/Customize Here/00 Sample W Root/";
            var names=new[]{"Fish Blue","Fish Grey","Fish Orange","Turtle Blue","Turtle Brown","Turtle Green","Octopus Blue","Octopus Orange","Octopus Purple"};
            game.fishPrefabs=new GameObject[names.Length];
            for(int i=0;i<names.Length;i++)
            {
                game.fishPrefabs[i]=AssetDatabase.LoadAssetAtPath<GameObject>(root+names[i]+".prefab");
                if(!game.fishPrefabs[i])throw new FileNotFoundException("Missing model "+names[i]);
            }
            game.crabPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(root+"Crab Orange.prefab");
            game.uiFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");game.boardCamera=camera;
            game.characterAnimations=new RuntimeAnimatorController[4];
            var species=new[]{"Fish","Turtle","Octopus","Crab"};
            Directory.CreateDirectory("Assets/FishOut/Animations");AssetDatabase.Refresh();
            for(int i=0;i<species.Length;i++)game.characterAnimations[i]=CreateAnimations(species[i]);
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft=false;PlayerSettings.allowedAutorotateToLandscapeRight=false;
            PlayerSettings.allowedAutorotateToPortrait=true;PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
            PlayerSettings.Android.targetSdkVersion=(AndroidSdkVersions)35;
            PlayerSettings.runInBackground=true;
            EditorSceneManager.SaveScene(scene,ScenePath);
            var previous=EditorBuildSettings.scenes;
            var scenes=new System.Collections.Generic.List<EditorBuildSettingsScene>{new EditorBuildSettingsScene(ScenePath,true)};
            foreach(var s in previous)if(s.path!=ScenePath)scenes.Add(new EditorBuildSettingsScene(s.path,false));
            EditorBuildSettings.scenes=scenes.ToArray();
            EditorUtility.SetDirty(game);AssetDatabase.SaveAssets();
            SetPortraitGameView();
            Selection.activeGameObject=go;
            Debug.Log("Fish Out gameplay scene ready: "+ScenePath);
        }
        private static RuntimeAnimatorController CreateAnimations(string species)
        {
            string destination="Assets/FishOut/Animations/"+species+".controller";
            var existing=AssetDatabase.LoadAssetAtPath<AnimatorController>(destination);if(existing)return existing;
            var controller=AnimatorController.CreateAnimatorControllerAtPath(destination);
            controller.AddParameter("Swimming",AnimatorControllerParameterType.Bool);
            string basePath="Assets/Cube Animals 02/FBX/With Root/"+species+"/"+species;
            var idle=AssetDatabase.LoadAllAssetsAtPath(basePath+"@Idle.fbx").OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__"));
            string move=species=="Turtle"||species=="Crab"?"Walk":"Move";
            var swimming=AssetDatabase.LoadAllAssetsAtPath(basePath+"@"+move+" Forward In Place.fbx").OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__"));
            var machine=controller.layers[0].stateMachine;
            var idleState=machine.AddState("Idle");idleState.motion=idle;
            var swimState=machine.AddState("Swim");swimState.motion=swimming?swimming:idle;machine.defaultState=idleState;
            var begin=idleState.AddTransition(swimState);begin.hasExitTime=false;begin.duration=.08f;begin.AddCondition(AnimatorConditionMode.If,0,"Swimming");
            var end=swimState.AddTransition(idleState);end.hasExitTime=false;end.duration=.08f;end.AddCondition(AnimatorConditionMode.IfNot,0,"Swimming");
            return controller;
        }
        [MenuItem("Fish Out/Set portrait Game view")]
        public static void SetPortraitGameView()
        {
            // Game view sizing has no public API. Keep this editor-only, with a safe fallback.
            try
            {
                var assembly=typeof(UnityEditor.Editor).Assembly;
                var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
                var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizes=singleton.GetProperty("instance").GetValue(null);
                var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{0});
                var groupType=group.GetType();var sizeType=assembly.GetType("UnityEditor.GameViewSize");var enumType=assembly.GetType("UnityEditor.GameViewSizeType");
                int count=(int)groupType.GetMethod("GetTotalCount").Invoke(group,null),index=-1;
                for(int i=0;i<count;i++)
                {
                    var item=groupType.GetMethod("GetGameViewSize").Invoke(group,new object[]{i});
                    if((int)sizeType.GetProperty("width").GetValue(item)==1080&&(int)sizeType.GetProperty("height").GetValue(item)==1920)index=i;
                }
                if(index<0)
                {
                    var item=Activator.CreateInstance(sizeType,new object[]{Enum.ToObject(enumType,1),1080,1920,"Fish Out Portrait"});
                    groupType.GetMethod("AddCustomSize").Invoke(group,new[]{item});index=count;
                }
                var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
                view.GetType().GetProperty("selectedSizeIndex").SetValue(view,index);
            }
            catch(Exception ex){Debug.LogWarning("Select a 1080 x 1920 Game view manually. "+ex.Message);}
        }
    }
}
