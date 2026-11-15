using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FishOut
{
    // End card prefab for the ad scenes (Creative 03): a self-contained overlay canvas. Edit the look in Prefab Mode;
    // the director only wires the button and shows or removes the card.
    public sealed class CreativeEndCard : MonoBehaviour
    {
        [Tooltip("The button that restarts the ad (TRY AGAIN / NEXT).")]
        public Button button;
        [Tooltip("Pieces that pop in one after another when the card opens (title, picture, button...).")]
        public RectTransform[] reveal;
        [Tooltip("Seconds between two pieces popping in.")]
        public float revealStep=.12f;
        [Tooltip("Pulses forever once the card is open (usually the button).")]
        public RectTransform pulse;
        [Tooltip("Pulse strength (0.07 = 7% bigger at the peak) and speed.")]
        public float pulseScale=.07f,pulseSpeed=5f;

        private Vector3[] sizes;
        private Vector3 pulseSize=Vector3.one;

        private void OnEnable()
        {
            if(pulse)pulseSize=pulse.localScale;
            if(reveal==null)reveal=new RectTransform[0];
            sizes=new Vector3[reveal.Length];
            for(int i=0;i<reveal.Length;i++)if(reveal[i]){sizes[i]=reveal[i].localScale;reveal[i].localScale=Vector3.zero;}
            StartCoroutine(Open());
        }
        // Unscaled time: the game is frozen under the card.
        private IEnumerator Open()
        {
            for(int i=0;i<reveal.Length;i++)
            {
                if(reveal[i])StartCoroutine(Pop(reveal[i],sizes[i]));
                for(float t=0;t<revealStep;t+=Time.unscaledDeltaTime)yield return null;
            }
            for(float t=0;t<.35f;t+=Time.unscaledDeltaTime)yield return null;
            if(!pulse)yield break;
            var size=pulseSize;
            for(float t=0;pulse;t+=Time.unscaledDeltaTime){pulse.localScale=size*(1+Mathf.Sin(t*pulseSpeed)*pulseScale);yield return null;}
        }
        private static IEnumerator Pop(RectTransform target,Vector3 size)
        {
            const float time=.35f;
            for(float t=0;t<time&&target;t+=Time.unscaledDeltaTime)
            {
                float k=t/time;
                // Overshoots a little, then settles.
                float s=1+2.2f*Mathf.Pow(k-1,3)+1.2f*Mathf.Pow(k-1,2);
                target.localScale=size*s;yield return null;
            }
            if(target)target.localScale=size;
        }
    }
}
