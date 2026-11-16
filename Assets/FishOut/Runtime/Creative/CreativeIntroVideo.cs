using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FishOut
{
    // Ad scenes: a video plays full screen when the scene opens. When it ends (or the player skips it, if allowed)
    // it fades out and the gameplay starts from a fresh board. Pick the clip in the Inspector; with no clip the scene
    // goes straight to gameplay.
    public sealed class CreativeIntroVideo : MonoBehaviour
    {
        public enum Fit { FillScreen, ShowWholeVideo }

        [Tooltip("Video played before the gameplay. Drop any imported video (mp4, mov, webm) here.")]
        public VideoClip video;
        [Tooltip("Fill Screen crops the edges so there are no bars; Show Whole Video keeps the full frame with bars.")]
        public Fit fit=Fit.FillScreen;
        [Tooltip("Colour of the bars (Show Whole Video) and of the screen while the video loads.")]
        public Color background=Color.black;
        [Tooltip("Play the video's own sound.")]
        public bool playSound=true;
        [Range(0,1)]public float volume=1;
        [Tooltip("A tap ends the video early.")]
        public bool tapToSkip;
        [Tooltip("Fade from the video into the gameplay, in seconds.")]
        public float fadeOut=.35f;
        [Tooltip("Draw order of the video canvas; above the tap hand (900) and the end cards.")]
        public int sortingOrder=1000;

        // True from the moment the scene opens until the video has faded out. The ad director waits for it.
        public static bool Playing{get;private set;}

        private VideoPlayer player;
        private RenderTexture target;
        private GameObject screen;
        private bool finished;

        private void Awake()
        {
            Playing=video!=null&&isActiveAndEnabled;
            if(Playing)Build();
        }
        private void OnDisable()
        {
            Playing=false;
        }
        private void OnDestroy()
        {
            Playing=false;
            if(target){target.Release();Destroy(target);}
        }
        private void Build()
        {
            screen=new GameObject("Intro video canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster),typeof(CanvasGroup));
            screen.transform.SetParent(transform,false);
            var canvas=screen.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=sortingOrder;
            // The backdrop blocks every tap on the board while the video is up (the game ignores presses over UI).
            var back=Child("Background",screen.transform).AddComponent<Image>();
            back.color=background;Stretch(back.rectTransform);
            var picture=Child("Video",screen.transform).AddComponent<RawImage>();
            picture.raycastTarget=false;picture.color=Color.clear;
            var frame=picture.gameObject.AddComponent<AspectRatioFitter>();
            frame.aspectMode=fit==Fit.FillScreen?AspectRatioFitter.AspectMode.EnvelopeParent:AspectRatioFitter.AspectMode.FitInParent;
            frame.aspectRatio=video.height>0?(float)video.width/video.height:9f/16f;
            Stretch(picture.rectTransform);

            target=new RenderTexture((int)Mathf.Max(16,video.width),(int)Mathf.Max(16,video.height),0){name="Intro video"};
            picture.texture=target;
            player=gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake=false;player.isLooping=false;player.waitForFirstFrame=true;
            player.source=VideoSource.VideoClip;player.clip=video;
            player.renderMode=VideoRenderMode.RenderTexture;player.targetTexture=target;
            player.timeUpdateMode=VideoTimeUpdateMode.UnscaledGameTime;
            player.audioOutputMode=playSound?VideoAudioOutputMode.Direct:VideoAudioOutputMode.None;
            if(playSound)for(ushort track=0;track<video.audioTrackCount;track++){player.EnableAudioTrack(track,true);player.SetDirectAudioVolume(track,volume);}
            player.prepareCompleted+=source=>{picture.color=Color.white;source.Play();};
            player.loopPointReached+=source=>Finish();
            player.errorReceived+=(source,message)=>{Debug.LogWarning("Intro video: "+message);Finish();};
            player.Prepare();
        }
        private void Update()
        {
            if(!Playing||finished||!tapToSkip)return;
            if(Pressed())Finish();
        }
        private void Finish()
        {
            if(finished)return;
            finished=true;
            StartCoroutine(FadeAway());
        }
        private IEnumerator FadeAway()
        {
            // The board is rebuilt under the video first, so the fade reveals the fresh stage.
            Playing=false;
            yield return null;
            var group=screen.GetComponent<CanvasGroup>();group.blocksRaycasts=false;
            for(float t=0;t<fadeOut;t+=Time.unscaledDeltaTime){group.alpha=1-t/fadeOut;yield return null;}
            if(player)player.Stop();
            Destroy(screen);
            if(target){target.Release();Destroy(target);target=null;}
        }
        private static GameObject Child(string name,Transform parent)
        {
            var child=new GameObject(name,typeof(RectTransform));
            child.transform.SetParent(parent,false);
            return child;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
        private static bool Pressed()
        {
#if ENABLE_INPUT_SYSTEM
            if(Touchscreen.current!=null)foreach(var touch in Touchscreen.current.touches)if(touch.press.wasPressedThisFrame)return true;
            return Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0)||(Input.touchCount>0&&Input.GetTouch(0).phase==TouchPhase.Began);
#endif
        }
    }
}
