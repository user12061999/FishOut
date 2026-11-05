using System.Collections.Generic;
using UnityEngine;

namespace FishOut
{
    public sealed class FishOutAudio : MonoBehaviour
    {
        // A line of fish starts together, so a cue may only retrigger after this gap.
        private const float RetriggerGap=.07f;
        private const int GateVoices=4,GatePitchSteps=7;
        private const float GateResetGap=.5f;
        private AudioSource effects, music;
        private readonly AudioSource[] gateVoices=new AudioSource[GateVoices];
        private readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        private readonly Dictionary<string,float> lastPlayed=new Dictionary<string,float>();
        private bool enabledEffects;
        private int gateStep,gateVoice;
        private float lastGate=float.NegativeInfinity;
        public void Initialize(bool sound, bool background)
        {
            effects=gameObject.AddComponent<AudioSource>(); effects.playOnAwake=false; effects.volume=.7f;
            music=gameObject.AddComponent<AudioSource>(); music.playOnAwake=false; music.loop=true; music.volume=.3f;
            music.clip=Resources.Load<AudioClip>("Audio/bgm");
            for(int i=0;i<gateVoices.Length;i++)
            {
                gateVoices[i]=gameObject.AddComponent<AudioSource>(); gateVoices[i].playOnAwake=false; gateVoices[i].volume=.7f;
            }
            SetSound(sound); SetMusic(background);
        }
        public void SetSound(bool value) { enabledEffects=value; if(!value&&effects)effects.Stop(); }
        public void SetMusic(bool value) { if(!music)return; if(value){if(!music.isPlaying)music.Play();}else music.Stop(); }
        // Cue names match the files in Resources/Audio.
        public void Play(string cue)
        {
            if(!enabledEffects||!effects)return;
            if(lastPlayed.TryGetValue(cue,out var last)&&Time.unscaledTime-last<RetriggerGap)return;
            lastPlayed[cue]=Time.unscaledTime;
            var clip=Clip(cue);
            if(clip)effects.PlayOneShot(clip);
        }
        // Each fish entering soon after the last sounds a semitone higher, so a long line climbs.
        public void PlayGate()
        {
            if(!enabledEffects||!effects)return;
            gateStep=Time.unscaledTime-lastGate>GateResetGap?0:Mathf.Min(gateStep+1,GatePitchSteps);
            lastGate=Time.unscaledTime;
            var clip=Clip("entrance");if(!clip)return;
            var voice=gateVoices[gateVoice];gateVoice=(gateVoice+1)%gateVoices.Length;
            voice.pitch=Mathf.Pow(2,gateStep/12f);voice.PlayOneShot(clip);
        }
        private AudioClip Clip(string cue)
        {
            if(!clips.TryGetValue(cue,out var clip))clips[cue]=clip=Resources.Load<AudioClip>("Audio/"+cue);
            return clip;
        }
    }
}
