using UnityEngine;

namespace FishOut
{
    public sealed class FishOutAudio : MonoBehaviour
    {
        private AudioSource effects, music;
        private AudioClip swim, bump, danger, clear, loop;
        private bool enabledEffects;
        public void Initialize(bool sound, bool background)
        {
            effects=gameObject.AddComponent<AudioSource>(); effects.playOnAwake=false; effects.volume=.25f;
            music=gameObject.AddComponent<AudioSource>(); music.playOnAwake=false; music.loop=true; music.volume=.10f;
            swim=Tone("Swim",560,850,.12f); bump=Tone("Bump",210,110,.11f);
            danger=Tone("Crab warning",160,90,.25f); clear=Tone("Clear",440,1100,.5f);
            const int rate=22050; var data=new float[rate*16];
            var notes=new[]{261.63f,329.63f,392f,329.63f,293.66f,349.23f,440f,349.23f};
            for(int i=0;i<data.Length;i++)
            {
                float t=(float)i/rate; int beat=(int)(t/2); float p=(t%2)/2;
                float envelope=Mathf.Sin(p*Mathf.PI); float frequency=notes[beat%notes.Length];
                data[i]=Mathf.Sin(2*Mathf.PI*frequency*t)*envelope*.18f+Mathf.Sin(2*Mathf.PI*130.815f*t)*.025f;
            }
            loop=AudioClip.Create("Lagoon ambient loop",data.Length,1,rate,false); loop.SetData(data,0); music.clip=loop;
            SetSound(sound); SetMusic(background);
        }
        private AudioClip Tone(string name,float from,float to,float duration)
        {
            const int rate=22050; var data=new float[Mathf.CeilToInt(rate*duration)]; float phase=0;
            for(int i=0;i<data.Length;i++)
            {
                float p=(float)i/data.Length; phase+=2*Mathf.PI*Mathf.Lerp(from,to,p)/rate;
                data[i]=Mathf.Sin(phase)*Mathf.Sin(p*Mathf.PI)*.45f;
            }
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        public void SetSound(bool value) { enabledEffects=value; if(!value&&effects)effects.Stop(); }
        public void SetMusic(bool value) { if(!music)return; if(value){if(!music.isPlaying)music.Play();}else music.Stop(); }
        public void Play(string cue)
        {
            if(!enabledEffects||!effects)return;
            effects.PlayOneShot(cue=="clear"?clear:cue=="danger"?danger:cue=="bump"?bump:swim);
        }
        private void OnDestroy()
        {
            foreach(var clip in new[]{swim,bump,danger,clear,loop})if(clip)Destroy(clip);
        }
    }
}
