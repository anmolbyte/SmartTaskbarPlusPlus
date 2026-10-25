using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace SmartTaskbar.Views.Controls
{
    public class HotkeyInputBox : TextBox
    {
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

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            if (RecordedKey == Keys.None)
            {
                this.Text = "Press shortcut...";
                this.ForeColor = Color.Gray;
            }
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            UpdateDisplay();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys keyCode = keyData & Keys.KeyCode;
            Keys modifiers = keyData & Keys.Modifiers;

            // Handle Backspace or Delete to clear
            if (keyCode == Keys.Back || keyCode == Keys.Delete)
            {
                ClearHotkey();
                return true;
            }

            // Ignore standalone modifiers
            if (keyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin or Keys.None)
            {
                return true;
            }

            // Require at least one modifier (Ctrl, Alt, Shift) or function key to prevent capturing lone normal letters
            bool isFunctionKey = keyCode >= Keys.F1 && keyCode <= Keys.F24;
            if (modifiers == Keys.None && !isFunctionKey)
            {
                return true;
            }

            RecordedModifiers = modifiers;
            RecordedKey = keyCode;
            UpdateDisplay();
            HotkeyChanged?.Invoke(this, EventArgs.Empty);

            return true;
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
            sb.Append(RecordedKey.ToString());

            this.Text = sb.ToString();
        }
    }
}
