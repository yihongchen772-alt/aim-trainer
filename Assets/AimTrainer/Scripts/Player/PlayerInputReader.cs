using UnityEngine;
using UnityEngine.InputSystem;

namespace AimTrainer.Player
{
    /// <summary>一帧的玩家输入。</summary>
    public struct InputFrame
    {
        /// <summary>前后输入，W 为 +1，S 为 −1，同时按下为 0。</summary>
        public float Forward;

        /// <summary>左右输入，D 为 +1，A 为 −1，同时按下为 0。</summary>
        public float Right;

        public bool Walk;
        public bool Crouch;
        public bool JumpPressed;
        public bool AttackPressed;
        public bool EscapePressed;

        /// <summary>本帧鼠标移动量，单位是鼠标计数（Windows 上来自原始输入，不受系统鼠标加速影响）。</summary>
        public Vector2 LookDelta;
    }

    /// <summary>
    /// 直接轮询 Input System 的键盘和鼠标设备。
    /// 不经过 Input Action 资源，按键写死为端游默认键位，延迟最低、配置最少。
    /// </summary>
    public static class PlayerInputReader
    {
        public static InputFrame Read(bool scrollWheelJump)
        {
            var frame = new InputFrame();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                frame.Forward = Axis(keyboard.wKey.isPressed, keyboard.sKey.isPressed);
                frame.Right = Axis(keyboard.dKey.isPressed, keyboard.aKey.isPressed);
                frame.Walk = keyboard.leftShiftKey.isPressed;
                frame.Crouch = keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed;
                frame.JumpPressed = keyboard.spaceKey.wasPressedThisFrame;
                frame.EscapePressed = keyboard.escapeKey.wasPressedThisFrame;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                frame.LookDelta = mouse.delta.ReadValue();
                frame.AttackPressed = mouse.leftButton.wasPressedThisFrame;
                if (scrollWheelJump && mouse.scroll.ReadValue().y != 0f) frame.JumpPressed = true;
            }

            return frame;
        }

        static float Axis(bool positive, bool negative) => (positive ? 1f : 0f) - (negative ? 1f : 0f);
    }
}
