using System;
using UnityEngine;

namespace AimTrainer.Audio
{
    /// <summary>
    /// 运行时用代码合成的音效，不需要任何音频文件。第一次使用时生成并缓存。
    /// </summary>
    public static class ProceduralSounds
    {
        const int SampleRate = 44100;

        static AudioClip gunshot;
        static AudioClip enemyShot;
        static AudioClip headshot;
        static AudioClip hit;
        static AudioClip kill;
        static AudioClip hurt;
        static AudioClip reload;
        static AudioClip dryFire;

        /// <summary>自己开枪：短促的噪声爆破 + 低频冲击。</summary>
        public static AudioClip Gunshot => gunshot ??= Shot("AT_Gunshot", 0.16f, 0.032f, 0.55f, 120f, 1);

        /// <summary>假人开枪：更闷一些，便于和自己的枪声区分。</summary>
        public static AudioClip EnemyShot => enemyShot ??= Shot("AT_EnemyShot", 0.2f, 0.04f, 0.3f, 90f, 2);

        /// <summary>爆头：清脆的“叮”。</summary>
        public static AudioClip Headshot => headshot ??= Tones("AT_Headshot", 0.16f, new[] { 1900f, 2850f }, new[] { 0.5f, 0.2f }, 0.045f);

        /// <summary>打中身体：短促的“嗒”。</summary>
        public static AudioClip Hit => hit ??= Tones("AT_Hit", 0.06f, new[] { 820f }, new[] { 0.35f }, 0.018f, square: true);

        /// <summary>击杀确认：上扬的音。</summary>
        public static AudioClip Kill => kill ??= Sweep("AT_Kill", 0.14f, 950f, 1500f, 0.4f);

        /// <summary>自己受伤：低沉的闷响。</summary>
        public static AudioClip Hurt => hurt ??= Sweep("AT_Hurt", 0.2f, 180f, 70f, 0.6f);

        /// <summary>换弹：两下咔嗒声。</summary>
        public static AudioClip Reload => reload ??= Clicks("AT_Reload", 0.5f, new[] { 0.02f, 0.38f });

        /// <summary>空仓扣扳机。</summary>
        public static AudioClip DryFire => dryFire ??= Clicks("AT_DryFire", 0.08f, new[] { 0.005f });

        static AudioClip Shot(string name, float seconds, float noiseDecay, float noiseGain, float thumpHz, int seed)
        {
            var data = new float[Samples(seconds)];
            var rng = new System.Random(seed);
            float filtered = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                filtered += (white - filtered) * 0.35f; // 一阶低通，去掉刺耳的高频
                float noise = filtered * noiseGain * MathF.Exp(-t / noiseDecay);
                float thump = MathF.Sin(2f * MathF.PI * thumpHz * t * (1f - t * 2f)) * 0.5f * MathF.Exp(-t / 0.05f);
                data[i] = Clamp(noise + thump);
            }
            return Create(name, data);
        }

        static AudioClip Tones(string name, float seconds, float[] freqs, float[] gains, float decay, bool square = false)
        {
            var data = new float[Samples(seconds)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float v = 0f;
                for (int k = 0; k < freqs.Length; k++)
                {
                    float s = MathF.Sin(2f * MathF.PI * freqs[k] * t);
                    if (square) s = s >= 0f ? 0.6f : -0.6f;
                    v += s * gains[k];
                }
                data[i] = Clamp(v * MathF.Exp(-t / decay) * Attack(t));
            }
            return Create(name, data);
        }

        static AudioClip Sweep(string name, float seconds, float fromHz, float toHz, float gain)
        {
            var data = new float[Samples(seconds)];
            double phase = 0.0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float f = fromHz + (toHz - fromHz) * (t / seconds);
                phase += 2.0 * Math.PI * f / SampleRate;
                float tri = (float)(2.0 / Math.PI * Math.Asin(Math.Sin(phase)));
                data[i] = Clamp(tri * gain * (1f - t / seconds) * Attack(t));
            }
            return Create(name, data);
        }

        static AudioClip Clicks(string name, float seconds, float[] times)
        {
            var data = new float[Samples(seconds)];
            var rng = new System.Random(7);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float v = 0f;
                foreach (float start in times)
                {
                    float dt = t - start;
                    if (dt < 0f || dt > 0.04f) continue;
                    v += (float)(rng.NextDouble() * 2.0 - 1.0) * 0.5f * MathF.Exp(-dt / 0.006f);
                    v += MathF.Sin(2f * MathF.PI * 2400f * dt) * 0.2f * MathF.Exp(-dt / 0.01f);
                }
                data[i] = Clamp(v);
            }
            return Create(name, data);
        }

        static int Samples(float seconds) => Math.Max(1, (int)(seconds * SampleRate));
        static float Attack(float t) => MathF.Min(1f, t / 0.002f);
        static float Clamp(float v) => v < -1f ? -1f : v > 1f ? 1f : v;

        static AudioClip Create(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
