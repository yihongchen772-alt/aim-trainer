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

        static readonly string[] AllScenes = { SceneCatalog.RangeScene, SceneCatalog.SiteScene, SceneCatalog.MovementScene };

        /// <summary>任意一个训练场景已经存在（说明不是第一次打开，或者用户改过场景）。</summary>
        public static bool AnySceneExists()
        {
            foreach (string scene in AllScenes)
            {
                if (File.Exists(SceneKit.ScenePath(scene))) return true;
            }
            return false;
        }

        [MenuItem(Menu + "重新生成全部场景", true)]
        [MenuItem(Menu + "创建移动测试场景（第一阶段）", true)]
        static bool CanGenerate() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(Menu + "重新生成全部场景", false, 0)]
        static void GenerateAllFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AnySceneExists() && !EditorUtility.DisplayDialog("重新生成全部场景",
                    "靶场、A 点和移动测试场景会被覆盖（移动参数和枪械参数资源不会被覆盖）。", "覆盖", "取消"))
            {
                return;
            }
            GenerateAll();
        }

        [MenuItem(Menu + "创建移动测试场景（第一阶段）", false, 1)]
        static void GenerateMovementFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            MovementTestSceneBuilder.Build();
            Debug.Log("[AimTrainer] 已生成移动测试场景。点 Play，再点一下 Game 视图锁定鼠标即可开始。");
        }

        /// <summary>生成三个场景，并打开靶场。</summary>
        public static void GenerateAll()
        {
            MovementTestSceneBuilder.Build();
            SiteSceneBuilder.Build();
            RangeSceneBuilder.Build();
            AssetDatabase.SaveAssets();
            OpenScene(SceneCatalog.RangeScene);
            Debug.Log("[AimTrainer] 已生成靶场、A 点和移动测试场景，当前打开的是靶场。点 Play 开始。");
        }

        [MenuItem(Menu + "打开场景/靶场", false, 20)]
        static void OpenRange() => OpenFromMenu(SceneCatalog.RangeScene);

        [MenuItem(Menu + "打开场景/A 点", false, 21)]
        static void OpenSite() => OpenFromMenu(SceneCatalog.SiteScene);

        [MenuItem(Menu + "打开场景/移动测试", false, 22)]
        static void OpenMovement() => OpenFromMenu(SceneCatalog.MovementScene);

        static void OpenFromMenu(string sceneName)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // 只补上缺少的那一个场景，不覆盖其他已经存在（可能被改过）的场景。
            if (!File.Exists(SceneKit.ScenePath(sceneName)))
            {
                BuildScene(sceneName);
                AssetDatabase.SaveAssets();
            }
            OpenScene(sceneName);
        }

        static void BuildScene(string sceneName)
        {
            if (sceneName == SceneCatalog.SiteScene) SiteSceneBuilder.Build();
            else if (sceneName == SceneCatalog.MovementScene) MovementTestSceneBuilder.Build();
            else RangeSceneBuilder.Build();
        }

        public static void OpenScene(string sceneName)
        {
            string path = SceneKit.ScenePath(sceneName);
            if (File.Exists(path)) EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }

    /// <summary>
    /// 第一次打开项目时自动生成全部场景并打开靶场，这样打开项目后直接点 Play 就能玩。
    /// 每次启动编辑器只检查一次；只要有任意一个训练场景已经存在就不做任何事，绝不覆盖已有场景。
    /// </summary>
    [InitializeOnLoad]
    static class AutoSetup
    {
        const string SessionKey = "AimTrainer.AutoSetupChecked";

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
            if (SceneGenerator.AnySceneExists()) return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log("[AimTrainer] 第一次打开项目，正在生成训练场景……");
            SceneGenerator.GenerateAll();
        }
    }
}
