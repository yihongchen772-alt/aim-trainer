using UnityEngine;

namespace AimTrainer.Weapons
{
    /// <summary>
    /// 弹孔和弹道线的对象池。全部在运行时创建，不需要预制体或贴图。
    /// 使用内置的 Sprites/Default 着色器（不受光照影响，内置管线和 URP 都能渲染，打包时也总会包含）。
    /// </summary>
    public sealed class FxPool : MonoBehaviour
    {
        const int HoleCount = 96;
        const int TracerCount = 24;
        const float TracerLife = 0.06f;

        static FxPool instance;
        static Material unlitMaterial;

        readonly Transform[] holes = new Transform[HoleCount];
        readonly LineRenderer[] tracers = new LineRenderer[TracerCount];
        readonly float[] tracerLife = new float[TracerCount];
        readonly Color[] tracerColor = new Color[TracerCount];
        int nextHole;
        int nextTracer;

        static FxPool Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("FxPool").AddComponent<FxPool>();
                return instance;
            }
        }

        /// <summary>不受光照影响的半透明材质，弹孔、弹道线、枪口火焰共用。</summary>
        public static Material UnlitMaterial
        {
            get
            {
                if (unlitMaterial != null) return unlitMaterial;
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
                unlitMaterial = new Material(shader) { name = "AT_Unlit", hideFlags = HideFlags.DontSave };
                return unlitMaterial;
            }
        }

        public static void BulletHole(Vector3 point, Vector3 normal) => Instance.PlaceHole(point, normal);

        public static void Tracer(Vector3 from, Vector3 to, Color color) => Instance.ShowTracer(from, to, color);

        /// <summary>隐藏所有弹孔（换场景或重新开始时调用）。</summary>
        public static void ClearHoles()
        {
            if (instance == null) return;
            foreach (Transform hole in instance.holes)
            {
                if (hole != null) hole.gameObject.SetActive(false);
            }
        }

        void Awake()
        {
            var holeColor = new Color(0.08f, 0.08f, 0.09f, 0.9f);
            for (int i = 0; i < HoleCount; i++)
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "BulletHole";
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(transform, false);
                quad.transform.localScale = Vector3.one * 0.06f;
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = UnlitMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var block = new MaterialPropertyBlock();
                block.SetColor("_Color", holeColor);
                renderer.SetPropertyBlock(block);
                quad.SetActive(false);
                holes[i] = quad.transform;
            }

            for (int i = 0; i < TracerCount; i++)
            {
                var line = new GameObject("Tracer").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.sharedMaterial = UnlitMaterial;
                line.positionCount = 2;
                line.useWorldSpace = true;
                line.startWidth = 0.012f;
                line.endWidth = 0.006f;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                tracers[i] = line;
            }
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < TracerCount; i++)
            {
                if (tracerLife[i] <= 0f) continue;
                tracerLife[i] -= dt;
                LineRenderer line = tracers[i];
                if (tracerLife[i] <= 0f)
                {
                    line.enabled = false;
                    continue;
                }
                Color c = tracerColor[i];
                c.a *= tracerLife[i] / TracerLife;
                line.startColor = c;
                line.endColor = new Color(c.r, c.g, c.b, c.a * 0.4f);
            }
        }

        void PlaceHole(Vector3 point, Vector3 normal)
        {
            Transform hole = holes[nextHole];
            nextHole = (nextHole + 1) % HoleCount;
            hole.position = point + normal * 0.004f;
            hole.rotation = Quaternion.LookRotation(-normal);
            hole.gameObject.SetActive(true);
        }

        void ShowTracer(Vector3 from, Vector3 to, Color color)
        {
            int i = nextTracer;
            nextTracer = (nextTracer + 1) % TracerCount;
            LineRenderer line = tracers[i];
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startColor = color;
            line.endColor = color;
            line.enabled = true;
            tracerLife[i] = TracerLife;
            tracerColor[i] = color;
        }
    }
}
