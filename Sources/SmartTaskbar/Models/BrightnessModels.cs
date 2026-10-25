using System;
using System.Text;
using System.Windows.Forms;

namespace SmartTaskbar.Models
{
    public enum ShortcutAction
    {
        Increase,
        Decrease,
        SetTo
    }

    public class DisplayShortcutRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TargetMonitorId { get; set; } = "ALL"; // "ALL" or specific HardwareId
        public ShortcutAction Action { get; set; } = ShortcutAction.Increase;
        public int Value { get; set; } = 5;
        public Keys Modifiers { get; set; } = Keys.Control;
        public Keys Key { get; set; } = Keys.None;
        public bool Enabled { get; set; } = true;

        public string GetFormattedShortcut()
        {
            if (Key == Keys.None) return "None";
            var sb = new StringBuilder();
            if ((Modifiers & Keys.Control) == Keys.Control) sb.Append("Ctrl + ");
            if ((Modifiers & Keys.Alt) == Keys.Alt) sb.Append("Alt + ");
            if ((Modifiers & Keys.Shift) == Keys.Shift) sb.Append("Shift + ");
            sb.Append(Key.ToString());
            return sb.ToString();
        }

        public string GetActionDescription()
        {
            return Action switch
            {
                ShortcutAction.Increase => $"Increase (+{Value}%)",
                ShortcutAction.Decrease => $"Decrease (-{Value}%)",
                ShortcutAction.SetTo => $"Set to {Value}%",
                _ => Action.ToString()
            };
        }
    }

    public class MonitorDevice
    {
        public string HardwareId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty; // e.g. \\.\DISPLAY1
        public string SystemModelName { get; set; } = "Monitor";
        public string Nickname { get; set; } = string.Empty;
        public int CurrentBrightness { get; set; } = 50;
        public uint MinBrightness { get; set; } = 0;
        public uint MaxBrightness { get; set; } = 100;
        public bool IsInternal { get; set; }
        public IntPtr PhysicalHandle { get; set; } = IntPtr.Zero;

        public string DisplayName => !string.IsNullOrWhiteSpace(Nickname) ? Nickname : SystemModelName;
    }
}
