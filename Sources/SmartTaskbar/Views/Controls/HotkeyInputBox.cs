using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SmartTaskbar.Views.Controls
{
    public class HotkeyInputBox : TextBox
    {
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_CHAR = 0x0102;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WM_SYSCHAR = 0x0106;

        private readonly List<string> _downKeys = new();
        private string _accelerator = string.Empty;

        public event EventHandler? AcceleratorChanged;

        public string Accelerator
        {
            get => _accelerator;
            set
            {
                _accelerator = value ?? string.Empty;
                UpdateDisplay();
            }
        }

        public HotkeyInputBox()
        {
            this.ReadOnly = true;
            this.BackColor = Color.White;
            this.Cursor = Cursors.Hand;
            this.TextAlign = HorizontalAlignment.Center;
            this.BorderStyle = BorderStyle.FixedSingle;
            UpdateDisplay();
        }

        public void SetAccelerator(string accelerator)
        {
            Accelerator = accelerator;
            AcceleratorChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearHotkey()
        {
            _downKeys.Clear();
            Accelerator = string.Empty;
            UpdateDisplay();
            AcceleratorChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateDisplay()
        {
            if (string.IsNullOrWhiteSpace(_accelerator))
            {
                this.Text = this.Focused ? "Press shortcut..." : "Press shortcut...";
                this.ForeColor = Color.Gray;
            }
            else
            {
                this.Text = _accelerator;
                this.ForeColor = HasPrimaryKey(_accelerator) ? Color.Black : Color.DarkSlateBlue;
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            // Inform Windows Forms that all keys belong to this control and must not be consumed by dialog manager
            return true;
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            // Prevent container from using Tab or Arrow keys for dialog focus navigation
            return false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Prevent parent form command accelerator handling while focused
            return false;
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            this.BackColor = Color.FromArgb(240, 248, 255); // AliceBlue focus highlight
            _downKeys.Clear();
            UpdateDisplay();
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            this.BackColor = Color.White;
            _downKeys.Clear();
            if (!HasPrimaryKey(_accelerator))
            {
                _accelerator = string.Empty;
                AcceleratorChanged?.Invoke(this, EventArgs.Empty);
            }
            UpdateDisplay();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            this.Select(0, 0); // Deselect any text selection highlight
        }

        public override bool PreProcessMessage(ref Message msg)
        {
            // Intercept keyboard messages at the message pump BEFORE Form / ContainerControl dialog key filtering
            if (msg.Msg == WM_CHAR || msg.Msg == WM_SYSCHAR)
            {
                // Suppress character beep
                return true;
            }

            if (msg.Msg == WM_KEYDOWN || msg.Msg == WM_SYSKEYDOWN)
            {
                int vkCode = msg.WParam.ToInt32();
                HandleKeyDown(vkCode);
                return true; // Consumed: prevents WinForms ProcessDialogKey (arrow keys, tab, alt navigation)
            }

            if (msg.Msg == WM_KEYUP || msg.Msg == WM_SYSKEYUP)
            {
                int vkCode = msg.WParam.ToInt32();
                HandleKeyUp(vkCode);
                return true;
            }

            return base.PreProcessMessage(ref msg);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_CHAR || m.Msg == WM_SYSCHAR)
            {
                return;
            }

            if (m.Msg == WM_KEYDOWN || m.Msg == WM_SYSKEYDOWN)
            {
                HandleKeyDown(m.WParam.ToInt32());
                return;
            }

            if (m.Msg == WM_KEYUP || m.Msg == WM_SYSKEYUP)
            {
                HandleKeyUp(m.WParam.ToInt32());
                return;
            }

            base.WndProc(ref m);
        }

        private void HandleKeyDown(int vkCode)
        {
            // Backspace, Delete, or Escape clears the hotkey if pressed alone
            if ((vkCode == 0x08 || vkCode == 0x2E || vkCode == 0x1B) && _downKeys.Count == 0)
            {
                ClearHotkey();
                return;
            }

            string keyName = CleanUpKeyboardKey(vkCode);
            if (string.IsNullOrEmpty(keyName)) return;

            if (!_downKeys.Contains(keyName))
            {
                _downKeys.Add(keyName);
                _accelerator = BuildAcceleratorString(_downKeys);
                UpdateDisplay();

                if (HasPrimaryKey(_accelerator))
                {
                    AcceleratorChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void HandleKeyUp(int vkCode)
        {
            string keyName = CleanUpKeyboardKey(vkCode);
            _downKeys.Remove(keyName);

            // If all keys were released without a primary key (e.g. user just tapped Ctrl or Alt), clear incomplete modifier
            if (_downKeys.Count == 0 && !HasPrimaryKey(_accelerator))
            {
                _accelerator = string.Empty;
                UpdateDisplay();
                AcceleratorChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public static bool HasPrimaryKey(string accelerator)
        {
            if (string.IsNullOrWhiteSpace(accelerator)) return false;
            var parts = accelerator.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                if (!IsModifier(p))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsModifier(string key)
        {
            return key is "Ctrl" or "Control" or "Alt" or "Shift" or "Super" or "Win" or "CommandOrControl";
        }

        private static string BuildAcceleratorString(List<string> keys)
        {
            var modifiers = new List<string>();
            var primaries = new List<string>();

            foreach (var k in keys)
            {
                if (IsModifier(k))
                {
                    string standardMod = k switch
                    {
                        "Control" => "Ctrl",
                        "Win" => "Super",
                        _ => k
                    };
                    if (!modifiers.Contains(standardMod)) modifiers.Add(standardMod);
                }
                else
                {
                    if (!primaries.Contains(k)) primaries.Add(k);
                }
            }

            // Consistent ordering: Ctrl + Alt + Shift + Super + MainKey
            var ordered = new List<string>();
            if (modifiers.Contains("Ctrl")) ordered.Add("Ctrl");
            if (modifiers.Contains("Alt")) ordered.Add("Alt");
            if (modifiers.Contains("Shift")) ordered.Add("Shift");
            if (modifiers.Contains("Super")) ordered.Add("Super");
            ordered.AddRange(primaries);

            return string.Join("+", ordered);
        }

        public static string CleanUpKeyboardKey(int vkCode)
        {
            switch (vkCode)
            {
                // Modifiers
                case 0x11: // VK_CONTROL
                case 0xA2: // VK_LCONTROL
                case 0xA3: // VK_RCONTROL
                    return "Ctrl";

                case 0x12: // VK_MENU
                case 0xA4: // VK_LMENU
                case 0xA5: // VK_RMENU
                    return "Alt";

                case 0x10: // VK_SHIFT
                case 0xA0: // VK_LSHIFT
                case 0xA1: // VK_RSHIFT
                    return "Shift";

                case 0x5B: // VK_LWIN
                case 0x5C: // VK_RWIN
                    return "Super";

                // Navigation & Arrows
                case 0x25: return "Left";
                case 0x26: return "Up";
                case 0x27: return "Right";
                case 0x28: return "Down";
                case 0x21: return "PageUp";
                case 0x22: return "PageDown";
                case 0x23: return "End";
                case 0x24: return "Home";
                case 0x2D: return "Insert";
                case 0x2E: return "Delete";

                // Controls
                case 0x20: return "Space";
                case 0x08: return "Backspace";
                case 0x09: return "Tab";
                case 0x0D: return "Enter";
                case 0x1B: return "Escape";

                // Numpad
                case 0x60: return "num0";
                case 0x61: return "num1";
                case 0x62: return "num2";
                case 0x63: return "num3";
                case 0x64: return "num4";
                case 0x65: return "num5";
                case 0x66: return "num6";
                case 0x67: return "num7";
                case 0x68: return "num8";
                case 0x69: return "num9";
                case 0x6A: return "nummult";
                case 0x6B: return "numadd";
                case 0x6D: return "numsub";
                case 0x6E: return "numdec";
                case 0x6F: return "numdiv";

                // OEM keys
                case 0xBA: return ";";
                case 0xBB: return "Plus";
                case 0xBC: return ",";
                case 0xBD: return "Minus";
                case 0xBE: return ".";
                case 0xBF: return "/";
                case 0xC0: return "`";
                case 0xDB: return "[";
                case 0xDC: return "\\";
                case 0xDD: return "]";
                case 0xDE: return "'";

                default:
                    // Function keys F1 - F24
                    if (vkCode >= 0x70 && vkCode <= 0x87)
                    {
                        return "F" + (vkCode - 0x70 + 1);
                    }
                    // Standard A-Z
                    if (vkCode >= 0x41 && vkCode <= 0x5A)
                    {
                        return ((char)vkCode).ToString();
                    }
                    // Standard 0-9
                    if (vkCode >= 0x30 && vkCode <= 0x39)
                    {
                        return ((char)vkCode).ToString();
                    }
                    return ((Keys)vkCode).ToString();
            }
        }
    }
}
