using UnityEngine;
using UnityEngine.InputSystem;

namespace AimTrainer.Player
{
    /// <summary>一帧的玩家输入。</summary>
    public struct InputFrame
    {
        /// <summary>前后输入，前进键为 +1，后退键为 −1，同时按下为 0。</summary>
        public float Forward;

        /// <summary>左右输入，右移键为 +1，左移键为 −1，同时按下为 0。</summary>
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
    /// 不经过 Input Action 资源，延迟最低、配置最少。键位来自 Bindings（训练菜单里可以改，默认是端游键位）。
    /// </summary>
    public static class PlayerInputReader
    {
        /// <summary>当前键位。训练系统应用设置时会替换成玩家保存的键位。</summary>
        public static KeyBindings Bindings { get; set; } = new KeyBindings();

        public static InputFrame Read(bool scrollWheelJump)
        {
            KeyBindings keys = Bindings ?? new KeyBindings();
            var frame = new InputFrame();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                frame.Forward = Axis(KeyBindings.IsDown(keyboard, keys.forward), KeyBindings.IsDown(keyboard, keys.back));
                frame.Right = Axis(KeyBindings.IsDown(keyboard, keys.right), KeyBindings.IsDown(keyboard, keys.left));
                frame.Walk = KeyBindings.IsDown(keyboard, keys.walk);
                frame.Crouch = KeyBindings.IsDown(keyboard, keys.crouch) || KeyBindings.IsDown(keyboard, keys.crouchAlt);
                frame.JumpPressed = KeyBindings.WasPressed(keyboard, keys.jump);
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
