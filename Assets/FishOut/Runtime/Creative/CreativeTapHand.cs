using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FishOut
{
    // Ad scenes: a hand shows where the player taps. Every press on a fish (or a crab) brings the hand up on that
    // spot with its fingertip on the press: it pushes in, then fades out. Drop the prefab into an ad scene and edit
    // the look (sprite, size, fingertip pivot) in Prefab Mode.
    public sealed class CreativeTapHand : MonoBehaviour
    {
        [Tooltip("The hand image. Its pivot is the fingertip: that point lands on the press.")]
        public RectTransform hand;
        [Tooltip("Fades the hand in and out (on the hand image).")]
        public CanvasGroup fade;
        [Tooltip("Only show the hand on presses that land on a fish or a crab.")]
        public bool onlyOnPieces=true;
        [Tooltip("How close to a piece a press must land to count, in world units (a cell is about 1).")]
        public float reach=.6f;
        [Header("Animation (seconds, unscaled time)")]
        [Tooltip("Size the hand starts at, then pushes in to, as a share of its prefab size.")]
        public float startScale=1.15f,pressScale=.88f;
        public float pressTime=.12f,holdTime=.2f,fadeTime=.35f;

        private Canvas canvas;
        private Vector3 size=Vector3.one;
        private Coroutine running;

        private void Awake()
        {
            canvas=GetComponentInParent<Canvas>();
            if(hand)size=hand.localScale;
            if(fade)fade.alpha=0;
        }
        private void Update()
        {
            // Nothing under an open end card (the ad freezes the game there).
            if(Time.timeScale==0||!hand)return;
            if(!Pressed(out var screen))return;
            if(onlyOnPieces&&!OnPiece(screen))return;
            Show(screen);
        }
        public void Show(Vector2 screen)
        {
            var area=canvas?canvas.transform as RectTransform:null;
            if(!area)return;
            var eye=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(area,screen,eye,out var local))return;
            hand.localPosition=local;
            if(running!=null)StopCoroutine(running);
            running=StartCoroutine(Tap());
        }
        private IEnumerator Tap()
        {
            if(fade)fade.alpha=1;
            for(float t=0;t<pressTime;t+=Time.unscaledDeltaTime)
            {
                hand.localScale=size*Mathf.Lerp(startScale,pressScale,Mathf.SmoothStep(0,1,t/pressTime));
                yield return null;
            }
            for(float t=0;t<.1f;t+=Time.unscaledDeltaTime){hand.localScale=size*Mathf.Lerp(pressScale,1,t/.1f);yield return null;}
            hand.localScale=size;
            for(float t=0;t<holdTime;t+=Time.unscaledDeltaTime)yield return null;
            for(float t=0;t<fadeTime;t+=Time.unscaledDeltaTime){if(fade)fade.alpha=1-t/fadeTime;yield return null;}
            if(fade)fade.alpha=0;
            running=null;
        }
        private static bool Pressed(out Vector2 screen)
        {
            screen=Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var touch=Touchscreen.current;
            if(touch!=null&&touch.primaryTouch.press.wasPressedThisFrame){screen=touch.primaryTouch.position.ReadValue();return true;}
            var mouse=Mouse.current;
            if(mouse!=null&&mouse.leftButton.wasPressedThisFrame){screen=mouse.position.ReadValue();return true;}
            return false;
#else
            if(!Input.GetMouseButtonDown(0))return false;
            screen=Input.mousePosition;return true;
#endif
        }
        // The same test the game uses for a tap: a piece's collider under the press, or a piece close to it on the board.
        private bool OnPiece(Vector2 screen)
        {
            var game=FindFirstObjectByType<FishOutGame>();
            var cam=game&&game.boardCamera?game.boardCamera:Camera.main;
            if(!cam)return false;
            var ray=cam.ScreenPointToRay(screen);
            if(Physics.Raycast(ray,out var hit,1000f)&&hit.collider.GetComponentInParent<FishActor>())return true;
            if(!game||game.World==null)return false;
            var floor=new Plane(Vector3.up,Vector3.up*.1f);
            if(!floor.Raycast(ray,out var enter))return false;
            var point=ray.GetPoint(enter);
            foreach(var pair in game.World.actors)
            {
                if(!pair.Value)continue;
                var p=pair.Value.transform.position;p.y=point.y;
                if((p-point).sqrMagnitude<=reach*reach)return true;
            }
            return false;
        }
    }
}
