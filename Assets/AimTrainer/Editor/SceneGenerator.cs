using System.IO;
using AimTrainer.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AimTrainer.EditorTools
{
    /// <summary>
    /// 菜单 AimTrainer：生成 / 打开训练场景。
    /// 场景全部由代码生成，修改了生成代码后用“重新生成全部场景”刷新即可。
    /// </summary>
    public static class SceneGenerator
    {
        const string Menu = "AimTrainer/";

        /// <summary>全部由代码生成的场景及其生成方法、中文名。生成方法返回是否真的生成了（真实地图在模型没导入时会跳过）。</summary>
        static readonly (string Scene, string Title, System.Func<bool> Build)[] Scenes =
        {
            (SceneCatalog.RangeScene, "靶场", Always(RangeSceneBuilder.Build)),
            (SceneCatalog.AscentRealScene, "亚海悬城（真实地图）", RealMapBuilder.BuildAscentReal),
            (SceneCatalog.SiteScene, "A 点（原创）", Always(SiteSceneBuilder.Build)),
            (SceneCatalog.AscentScene, "亚海悬城（整图）", Always(ValorantMapBuilders.BuildAscent)),
            (SceneCatalog.AscentAScene, "亚海悬城 A", Always(ValorantMapBuilders.BuildAscentA)),
            (SceneCatalog.BindBScene, "源工重镇 B", Always(ValorantMapBuilders.BuildBindB)),
            (SceneCatalog.HavenCScene, "隐世修所 C", Always(ValorantMapBuilders.BuildHavenC)),
            (SceneCatalog.SplitAScene, "霓虹町 A", Always(ValorantMapBuilders.BuildSplitA)),
            (SceneCatalog.MovementScene, "移动测试", Always(MovementTestSceneBuilder.Build)),
        };

        static System.Func<bool> Always(System.Action build) => () =>
        {
            build();
            return true;
        };

        public static int SceneCount => Scenes.Length;

        /// <summary>任意一个训练场景已经存在（说明不是第一次打开，或者用户改过场景）。</summary>
        public static bool AnySceneExists()
        {
            foreach (var entry in Scenes)
            {
                if (File.Exists(SceneKit.ScenePath(entry.Scene))) return true;
            }
            return false;
        }

        [MenuItem(Menu + "重新生成全部场景", true)]
        [MenuItem(Menu + "生成缺少的场景", true)]
        [MenuItem(Menu + "创建移动测试场景（第一阶段）", true)]
        static bool CanGenerate() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(Menu + "重新生成全部场景", false, 0)]
        static void GenerateAllFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AnySceneExists() && !EditorUtility.DisplayDialog("重新生成全部场景",
                    "所有训练场景（靶场、各张地图、移动测试）都会被覆盖，你在这些场景里手动做的修改会丢失。移动参数和枪械参数资源不会被覆盖。",
                    "覆盖", "取消"))
            {
                return;
            }
            GenerateAll();
        }

        [MenuItem(Menu + "生成缺少的场景", false, 1)]
        static void GenerateMissingFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int built = GenerateMissing();
            Debug.Log(built > 0 ? $"[AimTrainer] 已生成 {built} 个缺少的场景。" : "[AimTrainer] 所有场景都已存在。");
        }

        [MenuItem(Menu + "创建移动测试场景（第一阶段）", false, 2)]
        static void GenerateMovementFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            MovementTestSceneBuilder.Build();
            Debug.Log("[AimTrainer] 已生成移动测试场景。点 Play，再点一下 Game 视图锁定鼠标即可开始。");
        }

        /// <summary>生成全部场景（覆盖已有的），并打开靶场。</summary>
        public static void GenerateAll()
        {
            foreach (var entry in Scenes) entry.Build();
            AssetDatabase.SaveAssets();
            OpenScene(SceneCatalog.RangeScene);
            Debug.Log("[AimTrainer] 已生成全部训练场景，当前打开的是靶场。点 Play 开始。");
        }

        /// <summary>只生成还不存在的场景，不覆盖任何已有场景。生成后回到原来打开的场景。返回生成的数量。</summary>
        public static int GenerateMissing()
        {
            string previous = EditorSceneManager.GetActiveScene().path;
            int built = 0;
            foreach (var entry in Scenes)
            {
                if (File.Exists(SceneKit.ScenePath(entry.Scene))) continue;
                if (entry.Build()) built++;
            }
            if (built == 0) return 0;
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            else OpenScene(SceneCatalog.RangeScene);
            return built;
        }

        [MenuItem(Menu + "打开场景/靶场", false, 20)]
        static void OpenRange() => OpenFromMenu(SceneCatalog.RangeScene);

        [MenuItem(Menu + "打开场景/A 点（原创）", false, 21)]
        static void OpenSite() => OpenFromMenu(SceneCatalog.SiteScene);

        [MenuItem(Menu + "打开场景/亚海悬城（真实地图）", false, 21)]
        static void OpenAscentReal() => OpenFromMenu(SceneCatalog.AscentRealScene);

        [MenuItem(Menu + "打开场景/亚海悬城（整图）", false, 22)]
        static void OpenAscentFull() => OpenFromMenu(SceneCatalog.AscentScene);

        [MenuItem(Menu + "打开场景/亚海悬城 A", false, 22)]
        static void OpenAscent() => OpenFromMenu(SceneCatalog.AscentAScene);

        [MenuItem(Menu + "打开场景/源工重镇 B", false, 23)]
        static void OpenBind() => OpenFromMenu(SceneCatalog.BindBScene);

        [MenuItem(Menu + "打开场景/隐世修所 C", false, 24)]
        static void OpenHaven() => OpenFromMenu(SceneCatalog.HavenCScene);

        [MenuItem(Menu + "打开场景/霓虹町 A", false, 25)]
        static void OpenSplit() => OpenFromMenu(SceneCatalog.SplitAScene);

        [MenuItem(Menu + "打开场景/移动测试", false, 26)]
        static void OpenMovement() => OpenFromMenu(SceneCatalog.MovementScene);

        static void OpenFromMenu(string sceneName)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // 只补上缺少的那一个场景，不覆盖其他已经存在（可能被改过）的场景。
            if (!File.Exists(SceneKit.ScenePath(sceneName)))
            {
                foreach (var entry in Scenes)
                {
                    if (entry.Scene == sceneName) entry.Build();
                }
                AssetDatabase.SaveAssets();
            }
            OpenScene(sceneName);
        }

        public static void OpenScene(string sceneName)
        {
            string path = SceneKit.ScenePath(sceneName);
            if (File.Exists(path)) EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }

    /// <summary>
    /// 打开项目时自动补上缺少的训练场景（第一次打开时就是全部生成），这样直接点 Play 就能玩；
    /// 更新代码后新增的地图也会自动生成。只生成不存在的场景，绝不覆盖已有场景。
    /// 每次启动编辑器（以及场景列表变化后）只检查一次。
    /// </summary>
    [InitializeOnLoad]
    static class AutoSetup
    {
        static string SessionKey => "AimTrainer.AutoSetupChecked." + SceneGenerator.SceneCount;

        static AutoSetup() => EditorApplication.delayCall += TryRun;

        static void TryRun()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRun;
                return;
            }
            SessionState.SetBool(SessionKey, true);

            bool firstOpen = !SceneGenerator.AnySceneExists();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int built = SceneGenerator.GenerateMissing();
            if (built == 0) return;
            if (firstOpen)
            {
                SceneGenerator.OpenScene(SceneCatalog.RangeScene);
                Debug.Log("[AimTrainer] 第一次打开项目，已生成全部训练场景并打开靶场。点 Play 开始。");
            }
            else
            {
                Debug.Log($"[AimTrainer] 已自动生成 {built} 个新场景（新增的地图）。在训练菜单的“地图”里可以切换。");
            }
        }
    }
}
