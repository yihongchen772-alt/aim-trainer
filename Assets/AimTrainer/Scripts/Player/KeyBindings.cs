using System;
using UnityEngine.InputSystem;

namespace AimTrainer.Player
{
    /// <summary>
    /// 可以修改的键位。默认是端游默认键位；下蹲有两个键（左 Ctrl 和 C）。
    /// Esc（暂停）、F1 / F2（调试面板）和鼠标按键固定不变。
    /// </summary>
    [Serializable]
    public sealed class KeyBindings
    {
        public const int Count = 9;

        static readonly string[] Names = { "前进", "后退", "左移", "右移", "静步", "下蹲", "下蹲（第二个键）", "跳跃", "换弹" };

        public Key forward = Key.W;
        public Key back = Key.S;
        public Key left = Key.A;
        public Key right = Key.D;
        public Key walk = Key.LeftShift;
        public Key crouch = Key.LeftCtrl;
        public Key crouchAlt = Key.C;
        public Key jump = Key.Space;
        public Key reload = Key.R;

        public static string NameOf(int index) => index >= 0 && index < Count ? Names[index] : string.Empty;

        public Key Get(int index)
        {
            switch (index)
            {
                case 0: return forward;
                case 1: return back;
                case 2: return left;
                case 3: return right;
                case 4: return walk;
                case 5: return crouch;
                case 6: return crouchAlt;
                case 7: return jump;
                case 8: return reload;
                default: return Key.None;
            }
        }

        public void Set(int index, Key key)
        {
            switch (index)
            {
                case 0: forward = key; break;
                case 1: back = key; break;
                case 2: left = key; break;
                case 3: right = key; break;
                case 4: walk = key; break;
                case 5: crouch = key; break;
                case 6: crouchAlt = key; break;
                case 7: jump = key; break;
                case 8: reload = key; break;
            }
        }

        /// <summary>
        /// 把 index 对应的动作改成 key。如果别的动作正在用这个键，两者交换按键，保证一个键只对应一个动作。
        /// 返回被交换的动作序号，没有交换时返回 -1。
        /// </summary>
        public int Assign(int index, Key key)
        {
            if (index < 0 || index >= Count || IsReserved(key)) return -1;
            Key previous = Get(index);
            int swapped = -1;
            for (int i = 0; i < Count; i++)
            {
                if (i == index || Get(i) != key) continue;
                Set(i, previous);
                swapped = i;
            }
            Set(index, key);
            return swapped;
        }

        public void ResetToDefaults()
        {
            var defaults = new KeyBindings();
            for (int i = 0; i < Count; i++) Set(i, defaults.Get(i));
        }

        /// <summary>保留给暂停和调试面板的键，不能绑定到动作上。</summary>
        public static bool IsReserved(Key key) =>
            key == Key.None || key == Key.Escape || key == Key.F1 || key == Key.F2;

        /// <summary>存档里出现了无效或保留的按键时，恢复成默认键位。</summary>
        public void Sanitize()
        {
            var defaults = new KeyBindings();
            for (int i = 0; i < Count; i++)
            {
                Key key = Get(i);
                if (!Enum.IsDefined(typeof(Key), key) || IsReserved(key)) Set(i, defaults.Get(i));
            }
        }

        /// <summary>按键在界面上显示的名称（例如 LeftShift → 左 Shift）。</summary>
        public static string DisplayName(Key key)
        {
            switch (key)
            {
                case Key.LeftShift: return "左 Shift";
                case Key.RightShift: return "右 Shift";
                case Key.LeftCtrl: return "左 Ctrl";
                case Key.RightCtrl: return "右 Ctrl";
                case Key.LeftAlt: return "左 Alt";
                case Key.RightAlt: return "右 Alt";
                case Key.Space: return "空格";
                case Key.Tab: return "Tab";
                case Key.CapsLock: return "大写锁定";
                case Key.Enter: return "回车";
                case Key.Backspace: return "退格";
                case Key.None: return "（无）";
                default:
                    // Key 是按美式键盘的物理位置命名的；字母键优先用当前键盘布局下的名称（例如法式键盘上 W 位置显示 Z）。
                    Keyboard keyboard = Keyboard.current;
                    if (keyboard != null && key >= Key.A && key <= Key.Z)
                    {
                        string local = keyboard[key].displayName;
                        if (!string.IsNullOrEmpty(local)) return local.ToUpperInvariant();
                    }
                    string text = key.ToString();
                    if (text.StartsWith("Digit", StringComparison.Ordinal)) return text.Substring(5);
                    if (text.StartsWith("Numpad", StringComparison.Ordinal)) return "小键盘 " + text.Substring(6);
                    return text;
            }
        }

        public static bool IsDown(Keyboard keyboard, Key key) =>
            keyboard != null && key != Key.None && !IsReserved(key) && keyboard[key].isPressed;

        public static bool WasPressed(Keyboard keyboard, Key key) =>
            keyboard != null && key != Key.None && !IsReserved(key) && keyboard[key].wasPressedThisFrame;
    }
}
