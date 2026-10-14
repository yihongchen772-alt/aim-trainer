using System.Text;
using UnityEditor;
using UnityEngine;

namespace AimTrainer.EditorTools
{
    /// <summary>
    /// 菜单 AimTrainer → 应用高帧率项目设置：一键改成适合练枪的项目设置（第五阶段）。
    /// 改了什么、为什么改，见 docs/phase5-performance.md。
    /// </summary>
    public static class ProjectSettingsTool
    {
        [MenuItem("AimTrainer/应用高帧率项目设置", false, 40)]
        static void ApplyFromMenu()
        {
            if (!EditorUtility.DisplayDialog("应用高帧率项目设置",
                    "将修改：全屏方式、后台运行、垂直同步（所有画质档位）、阴影距离、反射探针等。\n\n具体内容见 docs/phase5-performance.md。",
                    "应用", "取消"))
            {
                return;
            }
            string report = Apply();
            EditorUtility.DisplayDialog("已应用", report, "好");
        }

        /// <summary>应用设置并返回修改说明。</summary>
        public static string Apply()
        {
            var log = new StringBuilder();

            PlayerSettings.productName = "战术练枪场";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.visibleInBackground = false;
            PlayerSettings.useFlipModelSwapchain = true;
            log.AppendLine("· 全屏方式：无边框全屏（配合 DXGI 翻转模型，延迟与独占全屏相当，切窗口更快）");
            log.AppendLine("· 原生分辨率启动，失去焦点时不在后台运行");

            int levels = ApplyQualityLevels();
            log.AppendLine($"· {levels} 个画质档位：关闭垂直同步、阴影距离 60 米 2 级级联、关闭实时反射探针和软粒子");

            AssetDatabase.SaveAssets();
            log.AppendLine();
            log.AppendLine("运行时还会自动：关闭垂直同步、取消帧率上限、CPU 最多领先 GPU 一帧（见 PerformanceSettings 组件）。");
            return log.ToString();
        }

        /// <summary>直接改 ProjectSettings/QualitySettings.asset 里每个画质档位的设置。</summary>
        static int ApplyQualityLevels()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (assets == null || assets.Length == 0) return 0;
            var so = new SerializedObject(assets[0]);
            SerializedProperty list = so.FindProperty("m_QualitySettings");
            if (list == null || !list.isArray) return 0;

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty level = list.GetArrayElementAtIndex(i);
                SetInt(level, "vSyncCount", 0);
                SetFloat(level, "shadowDistance", 60f);
                SetInt(level, "shadowCascades", 2);
                SetBool(level, "realtimeReflectionProbes", false);
                SetBool(level, "softParticles", false);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return list.arraySize;
        }

        static void SetInt(SerializedProperty parent, string name, int value)
        {
            SerializedProperty p = parent.FindPropertyRelative(name);
            if (p != null) p.intValue = value;
        }

        static void SetFloat(SerializedProperty parent, string name, float value)
        {
            SerializedProperty p = parent.FindPropertyRelative(name);
            if (p != null) p.floatValue = value;
        }

        static void SetBool(SerializedProperty parent, string name, bool value)
        {
            SerializedProperty p = parent.FindPropertyRelative(name);
            if (p == null) return;
            if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value;
            else p.intValue = value ? 1 : 0;
        }
    }
}
