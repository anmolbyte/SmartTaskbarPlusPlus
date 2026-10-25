using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SmartTaskbar.Views.Controls
{
    public class HotkeyInputBox : TextBox
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_CHAR = 0x0102;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WM_SYSCHAR = 0x0106;

        public Keys RecordedKey { get; private set; } = Keys.None;
        public Keys RecordedModifiers { get; private set; } = Keys.None;

        public event EventHandler? HotkeyChanged;

        public HotkeyInputBox()
        {
            this.ReadOnly = true;
            this.BackColor = Color.White;
            this.Cursor = Cursors.Hand;
            this.TextAlign = HorizontalAlignment.Center;
            UpdateDisplay();
        }

        public void SetHotkey(Keys modifiers, Keys key)
        {
            RecordedModifiers = modifiers;
            RecordedKey = key;
            UpdateDisplay();
            HotkeyChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearHotkey()
        {
            RecordedModifiers = Keys.None;
            RecordedKey = Keys.None;
            UpdateDisplay();
            HotkeyChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            // Inform Windows Forms that all keys (arrows, Tab, Enter, Escape, Function keys)
            // belong to this input box and must not trigger dialog navigation or be swallowed
            return true;
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            this.BackColor = Color.FromArgb(240, 248, 255); // AliceBlue highlight
            if (RecordedKey == Keys.None)
            {
                this.Text = "Press shortcut...";
                this.ForeColor = Color.DimGray;
            }
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            this.BackColor = Color.White;
            UpdateDisplay();
        }

        private static Keys GetActiveModifiers()
        {
            Keys mod = Keys.None;
            if ((GetAsyncKeyState(0x11) & 0x8000) != 0) mod |= Keys.Control; // VK_CONTROL
            if ((GetAsyncKeyState(0x12) & 0x8000) != 0) mod |= Keys.Alt;     // VK_MENU
            if ((GetAsyncKeyState(0x10) & 0x8000) != 0) mod |= Keys.Shift;   // VK_SHIFT
            return mod;
        }

        protected override void WndProc(ref Message m)
        {
            // Suppress character messages to avoid Windows typing ding/beep sounds
            if (m.Msg == WM_CHAR || m.Msg == WM_SYSCHAR)
            {
                return;
            }

            if (m.Msg == WM_KEYDOWN || m.Msg == WM_SYSKEYDOWN)
            {
                Keys keyCode = (Keys)m.WParam.ToInt32();
                Keys modifiers = GetActiveModifiers();

                // Clear on Backspace or Delete
                if (keyCode == Keys.Back || keyCode == Keys.Delete)
                {
                    ClearHotkey();
                    return;
                }

                // If only modifier is pressed, show live preview (e.g. "Ctrl + ...", "Ctrl + Alt + ...")
                if (keyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
                {
                    var previewSb = new StringBuilder();
                    if ((modifiers & Keys.Control) == Keys.Control) previewSb.Append("Ctrl + ");
                    if ((modifiers & Keys.Alt) == Keys.Alt) previewSb.Append("Alt + ");
                    if ((modifiers & Keys.Shift) == Keys.Shift) previewSb.Append("Shift + ");
                    previewSb.Append("...");
                    this.Text = previewSb.ToString();
                    this.ForeColor = Color.DarkBlue;
                    return;
                }

                // Normal key pressed with or without modifiers
                RecordedModifiers = modifiers;
                RecordedKey = keyCode;
                UpdateDisplay();
                HotkeyChanged?.Invoke(this, EventArgs.Empty);
                return; // suppress further Windows handling
            }

            if (m.Msg == WM_KEYUP || m.Msg == WM_SYSKEYUP)
            {
                // If user released modifiers without pressing a key, restore prompt
                if (RecordedKey == Keys.None)
                {
                    Keys modifiers = GetActiveModifiers();
                    if (modifiers == Keys.None)
                    {
                        this.Text = "Press shortcut...";
                        this.ForeColor = Color.DimGray;
                    }
                    else
                    {
                        var previewSb = new StringBuilder();
                        if ((modifiers & Keys.Control) == Keys.Control) previewSb.Append("Ctrl + ");
                        if ((modifiers & Keys.Alt) == Keys.Alt) previewSb.Append("Alt + ");
                        if ((modifiers & Keys.Shift) == Keys.Shift) previewSb.Append("Shift + ");
                        previewSb.Append("...");
                        this.Text = previewSb.ToString();
                    }
                }
                return;
            }

            base.WndProc(ref m);
        }

        private void UpdateDisplay()
        {
            this.ForeColor = SystemColors.WindowText;
            if (RecordedKey == Keys.None)
            {
                this.Text = this.Focused ? "Press shortcut..." : "None (Click to record)";
                if (!this.Focused) this.ForeColor = Color.Gray;
                return;
            }

            var sb = new StringBuilder();
            if ((RecordedModifiers & Keys.Control) == Keys.Control) sb.Append("Ctrl + ");
            if ((RecordedModifiers & Keys.Alt) == Keys.Alt) sb.Append("Alt + ");
            if ((RecordedModifiers & Keys.Shift) == Keys.Shift) sb.Append("Shift + ");

            sb.Append(FormatKey(RecordedKey));
            this.Text = sb.ToString();
        }

        public static string FormatKey(Keys key)
        {
            return key switch
            {
                Keys.Right => "Right",
                Keys.Left => "Left",
                Keys.Up => "Up",
                Keys.Down => "Down",
                Keys.Next => "PageDown",
                Keys.PageUp => "PageUp",
                Keys.Oemplus => "+",
                Keys.OemMinus => "-",
                Keys.OemPeriod => ".",
                Keys.Oemcomma => ",",
                Keys.Oem1 => ";",
                Keys.OemQuestion => "/",
                Keys.Oemtilde => "~",
                Keys.OemOpenBrackets => "[",
                Keys.OemCloseBrackets => "]",
                Keys.OemPipe => "\\",
                Keys.VolumeUp => "Volume Up",
                Keys.VolumeDown => "Volume Down",
                Keys.VolumeMute => "Volume Mute",
                _ => key.ToString()
            };
        }
    }
}
