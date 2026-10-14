using UnityEngine;
using UnityEngine.InputSystem;

namespace AimTrainer.Core
{
    /// <summary>
    /// 运行时的低延迟设置，场景加载时自动应用：
    /// - 关闭垂直同步：画面不再等待显示器刷新，鼠标到画面的延迟最低（代价是可能出现画面撕裂）。
    /// - 取消帧率上限（或按设置限制）。
    /// - 最多只让 CPU 领先 GPU 一帧（maxQueuedFrames = 1），避免显卡排队带来的额外延迟。
    /// - Input System 在每帧 Update 之前处理输入，读到的是这一帧最新的鼠标位移。
    /// 项目级的设置（全屏模式、图形 API、阴影等）见菜单 AimTrainer → 应用高帧率项目设置。
    /// </summary>
    public sealed class PerformanceSettings : MonoBehaviour
    {
        [Tooltip("关闭垂直同步。")]
        [SerializeField] bool disableVSync = true;

        [Tooltip("帧率上限，0 表示不限制。")]
        [SerializeField, Min(0)] int targetFrameRate;

        [Tooltip("CPU 最多领先 GPU 几帧。1 延迟最低；如果帧率不稳可以改成 2。")]
        [SerializeField, Range(1, 3)] int maxQueuedFrames = 1;

        public int TargetFrameRate => targetFrameRate;

        void Awake() => Apply();

        void OnValidate()
        {
            if (Application.isPlaying) Apply();
        }

        /// <summary>修改帧率上限（0 = 不限制）并立即生效。</summary>
        public void SetFrameRateCap(int cap)
        {
            targetFrameRate = Mathf.Max(0, cap);
            Apply();
        }

        public void Apply()
        {
            QualitySettings.vSyncCount = disableVSync ? 0 : 1;
            Application.targetFrameRate = targetFrameRate > 0 ? targetFrameRate : -1;
            QualitySettings.maxQueuedFrames = maxQueuedFrames;

            if (InputSystem.settings != null &&
                InputSystem.settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
            {
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            }
        }
    }
}
