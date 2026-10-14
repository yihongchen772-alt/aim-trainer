using AimTrainer.Combat;
using UnityEngine;

namespace AimTrainer.Audio
{
    /// <summary>玩家身上的 2D 音效播放器：枪声、命中反馈、受伤、换弹。</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class PlayerAudio : MonoBehaviour
    {
        AudioSource source;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        public void PlayShot() => Play(ProceduralSounds.Gunshot, 0.7f);
        public void PlayDryFire() => Play(ProceduralSounds.DryFire, 0.6f);
        public void PlayReload() => Play(ProceduralSounds.Reload, 0.6f);
        public void PlayHurt() => Play(ProceduralSounds.Hurt, 0.8f);

        /// <summary>命中反馈：爆头“叮”，身体“嗒”，击杀额外一声。</summary>
        public void PlayHitConfirm(HitZone zone, bool killed)
        {
            Play(zone == HitZone.Head ? ProceduralSounds.Headshot : ProceduralSounds.Hit, 0.8f);
            if (killed) Play(ProceduralSounds.Kill, 0.6f);
        }

        void Play(AudioClip clip, float volume)
        {
            if (source == null) source = GetComponent<AudioSource>();
            source.PlayOneShot(clip, volume);
        }
    }
}
