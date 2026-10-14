using AimTrainer.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace AimTrainer.Weapons
{
    /// <summary>
    /// 第一人称枪模：运行时用几个方块拼出来，挂在摄像机下面。
    /// 负责开枪后坐、跑动晃动、换弹下沉和枪口火焰，只影响画面，不影响弹道。
    /// 枪模缩小并贴近眼睛（枪口约在眼前 0.35 米），角色碰撞半径 0.3 米，正面贴墙时最多穿进墙里几厘米。
    /// </summary>
    public sealed class Viewmodel : MonoBehaviour
    {
        static readonly Vector3 RestPosition = new Vector3(0.12f, -0.11f, 0.18f);
        const float GunScale = 0.6f;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] PlayerController player;

        Transform gun;
        Transform muzzle;
        GameObject flash;
        float kick;
        float flashTimer;
        float bobPhase;
        float reloadBlend;

        public Vector3 MuzzlePosition => muzzle != null ? muzzle.position : transform.position;

        public void Bind(PlayerController owner) => player = owner;

        void Awake()
        {
            if (player == null) player = GetComponentInParent<PlayerController>();
            Build();
        }

        /// <summary>开一枪：枪模向后顶一下，枪口闪光。</summary>
        public void Kick(float amount)
        {
            kick = Mathf.Min(kick + amount, 0.05f);
            flashTimer = 0.04f;
        }

        /// <summary>换弹进度（0 = 没在换弹，0~1 = 换弹中）。</summary>
        public void SetReloadProgress(float progress, bool reloading) =>
            reloadBlend = reloading ? Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI) : 0f;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            kick = Mathf.MoveTowards(kick, 0f, dt * 0.6f) * Mathf.Exp(-dt * 14f);

            float speed = player != null ? player.HorizontalSpeed : 0f;
            bool grounded = player == null || player.IsGrounded;
            float bobAmount = grounded ? Mathf.Clamp01(speed / 5.4f) : 0f;
            bobPhase += dt * speed * 1.8f;
            var bob = new Vector3(Mathf.Sin(bobPhase) * 0.006f, -Mathf.Abs(Mathf.Cos(bobPhase)) * 0.005f, 0f) * bobAmount;

            gun.localPosition = RestPosition + bob + new Vector3(0f, -reloadBlend * 0.08f, -kick);
            gun.localRotation = Quaternion.Euler(-kick * 60f + reloadBlend * 35f, 0f, reloadBlend * -20f);

            flashTimer -= dt;
            flash.SetActive(flashTimer > 0f);
            if (flashTimer > 0f) flash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        }

        void Build()
        {
            gun = new GameObject("Gun").transform;
            gun.SetParent(transform, false);
            gun.localPosition = RestPosition;
            gun.localScale = Vector3.one * GunScale;

            var dark = new Color(0.16f, 0.17f, 0.2f);
            var mid = new Color(0.27f, 0.29f, 0.33f);
            var accent = new Color(0.85f, 0.25f, 0.3f);
            Part("Receiver", new Vector3(0f, 0f, 0f), new Vector3(0.045f, 0.06f, 0.24f), mid);
            Part("Handguard", new Vector3(0f, 0.004f, 0.16f), new Vector3(0.038f, 0.042f, 0.1f), dark);
            Part("Barrel", new Vector3(0f, 0.01f, 0.24f), new Vector3(0.018f, 0.018f, 0.08f), dark);
            Part("Magazine", new Vector3(0f, -0.065f, 0.03f), new Vector3(0.03f, 0.08f, 0.045f), dark, 12f);
            Part("Grip", new Vector3(0f, -0.055f, -0.07f), new Vector3(0.028f, 0.07f, 0.035f), dark, -15f);
            Part("Stock", new Vector3(0f, -0.005f, -0.17f), new Vector3(0.04f, 0.05f, 0.1f), mid);
            Part("Sight", new Vector3(0f, 0.04f, 0.02f), new Vector3(0.02f, 0.02f, 0.05f), dark);
            Part("Stripe", new Vector3(0f, 0.031f, 0.06f), new Vector3(0.046f, 0.004f, 0.09f), accent);

            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(gun, false);
            muzzle.localPosition = new Vector3(0f, 0.01f, 0.29f);

            flash = GameObject.CreatePrimitive(PrimitiveType.Quad);
            flash.name = "MuzzleFlash";
            Destroy(flash.GetComponent<Collider>());
            flash.transform.SetParent(muzzle, false);
            flash.transform.localScale = Vector3.one * 0.07f;
            var flashRenderer = flash.GetComponent<MeshRenderer>();
            flashRenderer.sharedMaterial = FxPool.UnlitMaterial;
            flashRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor(ColorId, new Color(1f, 0.85f, 0.45f, 0.95f));
            flashRenderer.SetPropertyBlock(block);
            flash.SetActive(false);
        }

        void Part(string name, Vector3 position, Vector3 size, Color color, float tilt = 0f)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(gun, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(tilt, 0f, 0f);
            part.transform.localScale = size;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor(ColorId, color);
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
