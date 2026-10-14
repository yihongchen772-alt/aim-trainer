using UnityEngine;

namespace AimTrainer.Core
{
    /// <summary>
    /// 启动时关闭垂直同步并取消帧率上限，降低输入延迟。第五阶段会在这里加更多优化项。
    /// 注意：在编辑器的 Game 视图里运行时，帧率还会受编辑器本身影响，最终手感以打包后的程序为准。
    /// </summary>
    public sealed class FrameRateSettings : MonoBehaviour
    {
        [Tooltip("关闭垂直同步。垂直同步会让画面等待显示器刷新，增加输入延迟。")]
        [SerializeField] bool disableVSync = true;

        [Tooltip("帧率上限，0 表示不限制。")]
        [SerializeField, Min(0)] int targetFrameRate;

        void Awake() => Apply();

        void OnValidate()
        {
            if (Application.isPlaying) Apply();
        }

        public void Apply()
        {
            QualitySettings.vSyncCount = disableVSync ? 0 : 1;
            Application.targetFrameRate = targetFrameRate > 0 ? targetFrameRate : -1;
        }
    }
}
