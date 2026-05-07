using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SmartTaskbar.Helpers;

namespace SmartTaskbar.Views
{
    public class HotkeyCaptureForm : Form
    {
        public uint SelectedModifiers { get; private set; }
        public uint SelectedKey { get; private set; }
        public bool Success { get; private set; }

        public HotkeyCaptureForm()
        {
            this.Text = "Assign Hotkey";
            this.Size = new Size(350, 150);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.KeyPreview = true;
            this.Font = SystemFonts.MessageBoxFont;

            var label = new Label
            {
                Text = "Press your desired key combination...\n(e.g. Ctrl + Alt + B)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(label);

            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu)
                    return;

                SelectedModifiers = 0;
                if (e.Control) SelectedModifiers |= Fun.MOD_CONTROL;
                if (e.Alt) SelectedModifiers |= Fun.MOD_ALT;
                if (e.Shift) SelectedModifiers |= Fun.MOD_SHIFT;

                SelectedKey = (uint)e.KeyCode;
                Success = true;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
        }
    }
}
