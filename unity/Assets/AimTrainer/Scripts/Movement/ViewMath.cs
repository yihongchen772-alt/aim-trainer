using System;

namespace AimTrainer.Movement
{
    /// <summary>
    /// 鼠标灵敏度与视野的换算，和《无畏契约》一致：
    /// 每个鼠标计数（count）转 0.07° × 游戏内灵敏度；视野固定为水平 103°。
    /// </summary>
    public static class ViewMath
    {
        /// <summary>灵敏度为 1 时每个鼠标计数转过的角度（度）。</summary>
        public const float DegreesPerCountAtSensitivity1 = 0.07f;

        /// <summary>端游的水平视野（度）。</summary>
        public const float DefaultHorizontalFov = 103f;

        public static float DegreesPerCount(float sensitivity) => DegreesPerCountAtSensitivity1 * sensitivity;

        /// <summary>转一整圈需要的鼠标计数。</summary>
        public static float CountsPer360(float sensitivity) => 360f / DegreesPerCount(sensitivity);

        /// <summary>转一整圈需要在鼠标垫上移动的厘米数。</summary>
        public static float CentimetersPer360(float sensitivity, float dpi) => CountsPer360(sensitivity) / dpi * 2.54f;

        /// <summary>
        /// 水平视野换算成 Unity 摄像机使用的竖直视野。
        /// Unity 的 Camera.fieldOfView 是竖直方向的，屏幕宽高比变化时必须重新换算，水平视野才能保持 103°。
        /// </summary>
        public static float HorizontalToVerticalFov(float horizontalFovDegrees, float aspect)
        {
            const float deg2Rad = MathF.PI / 180f;
            float halfH = horizontalFovDegrees * 0.5f * deg2Rad;
            return 2f * MathF.Atan(MathF.Tan(halfH) / aspect) / deg2Rad;
        }
    }
}
