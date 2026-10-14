using System.Collections.Generic;
using AimTrainer.Combat;
using AimTrainer.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace AimTrainer.Bots
{
    /// <summary>
    /// 在运行时用方块和球体拼出一个假人。尺寸（米）：
    /// 腿 0~0.8，身体 0.8~1.44，头部中心 1.6（看到的头半径 0.15，判定框半径 0.18，比模型大 20%），总高约 1.75。
    /// 每个部位是单独的碰撞体，挂 Hitbox 区分头 / 身体 / 腿。
    /// </summary>
    public static class BotFactory
    {
        /// <summary>头部判定框半径与看到的头部半径之比。</summary>
        public const float HeadHitboxScale = 1.2f;

        public const float HeadRadius = 0.15f;
        public const float HeadCenterHeight = 1.6f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        static readonly Color BodyColor = new Color(0.86f, 0.27f, 0.24f);
        static readonly Color LegColor = new Color(0.32f, 0.33f, 0.38f);
        static readonly Color HeadColor = new Color(0.96f, 0.62f, 0.5f);
        static readonly Color GunColor = new Color(0.15f, 0.15f, 0.17f);

        public static BotAgent Create(Vector3 feet, float yawDegrees, BotSkill skill, PlayerController target,
            BotBehavior behavior, bool canShoot, BotSpawnPoint spawnPoint, int seed, float maxHealth = 150f)
        {
            var root = new GameObject("Bot");
            root.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yawDegrees, 0f));
            var health = root.AddComponent<Health>();
            health.Configure(maxHealth);

            var renderers = new List<Renderer>();
            var colliders = new List<Collider>();

            AddBox(root.transform, "Legs", new Vector3(0f, 0.4f, 0f), new Vector3(0.38f, 0.8f, 0.24f), LegColor,
                HitZone.Legs, health, renderers, colliders);
            AddBox(root.transform, "Body", new Vector3(0f, 1.12f, 0f), new Vector3(0.5f, 0.64f, 0.3f), BodyColor,
                HitZone.Body, health, renderers, colliders);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, HeadCenterHeight, 0f);
            head.transform.localScale = Vector3.one * (HeadRadius * 2f);
            // 球体网格半径是 0.5，判定框按比例放大，比看到的头大一圈。
            head.GetComponent<SphereCollider>().radius = 0.5f * HeadHitboxScale;
            head.AddComponent<Hitbox>().Configure(HitZone.Head, health);
            Paint(head.GetComponent<Renderer>(), HeadColor);
            renderers.Add(head.GetComponent<Renderer>());
            colliders.Add(head.GetComponent<Collider>());

            // 手里的枪（只是外观，没有碰撞），枪口用来画弹道线。
            GameObject gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gun.name = "Gun";
            Object.Destroy(gun.GetComponent<Collider>());
            gun.transform.SetParent(root.transform, false);
            gun.transform.localPosition = new Vector3(0.2f, 1.3f, 0.3f);
            gun.transform.localScale = new Vector3(0.06f, 0.08f, 0.5f);
            Paint(gun.GetComponent<Renderer>(), GunColor);
            renderers.Add(gun.GetComponent<Renderer>());
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(0.2f, 1.3f, 0.57f);

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 4f;
            audio.maxDistance = 90f;

            var agent = root.AddComponent<BotAgent>();
            agent.BindParts(health, renderers.ToArray(), colliders.ToArray(), audio, muzzle);
            agent.Initialize(skill, target, behavior, canShoot, spawnPoint, seed);
            return agent;
        }

        static void AddBox(Transform parent, string name, Vector3 center, Vector3 size, Color color, HitZone zone,
            Health health, List<Renderer> renderers, List<Collider> colliders)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = center;
            part.transform.localScale = size;
            part.AddComponent<Hitbox>().Configure(zone, health);
            Paint(part.GetComponent<Renderer>(), color);
            renderers.Add(part.GetComponent<Renderer>());
            colliders.Add(part.GetComponent<Collider>());
        }

        static void Paint(Renderer renderer, Color color)
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            var block = new MaterialPropertyBlock();
            block.SetColor(ColorId, color);
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
