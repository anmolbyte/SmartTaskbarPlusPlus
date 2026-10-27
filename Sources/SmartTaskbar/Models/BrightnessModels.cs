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
        public string Accelerator { get; set; } = string.Empty; // e.g. "Ctrl+Alt+Right"
        public string TargetMonitorId { get; set; } = "ALL"; // "ALL" or specific HardwareId
        public ShortcutAction Action { get; set; } = ShortcutAction.Increase;
        public int Value { get; set; } = 5;
        public string Unit { get; set; } = "%"; // "%" or "pts"
        public Keys Modifiers { get; set; } = Keys.Control;
        public Keys Key { get; set; } = Keys.None;
        public bool Enabled { get; set; } = true;

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsRegistered { get; set; } = false;

        public string GetFormattedShortcut()
        {
            if (!string.IsNullOrWhiteSpace(Accelerator)) return Accelerator;
            if (Key == Keys.None) return "None";
            var sb = new StringBuilder();
            if ((Modifiers & Keys.Control) == Keys.Control) sb.Append("Ctrl+");
            if ((Modifiers & Keys.Alt) == Keys.Alt) sb.Append("Alt+");
            if ((Modifiers & Keys.Shift) == Keys.Shift) sb.Append("Shift+");
            sb.Append(FormatKey(Key));
            return sb.ToString();
        }

        private static string FormatKey(Keys key)
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
                _ => key.ToString()
            };
        }

        public string GetActionDescription()
        {
            string unitStr = Unit == "pts" ? "pts" : "%";
            return Action switch
            {
                ShortcutAction.Increase => $"Increase (+{Value}{unitStr})",
                ShortcutAction.Decrease => $"Decrease (-{Value}{unitStr})",
                ShortcutAction.SetTo => $"Set to {Value}{unitStr}",
                _ => Action.ToString()
            };
        }
    }

    public class MonitorDevice
    {
        public string HardwareId { get; set; } = string.Empty;
        public string LegacyHardwareId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty; // e.g. \\.\DISPLAY1
        public string SystemModelName { get; set; } = "Monitor";
        public string Nickname { get; set; } = string.Empty;
        public int CurrentBrightness { get; set; } = 50;
        public uint MinBrightness { get; set; } = 0;
        public uint MaxBrightness { get; set; } = 100;
        public bool IsInternal { get; set; }
        public IntPtr PhysicalHandle { get; set; } = IntPtr.Zero;
        public int PhysicalIndex { get; set; } = 0;

        public string DisplayName => !string.IsNullOrWhiteSpace(Nickname) ? Nickname : SystemModelName;
    }

    public class SavedMonitorInfo
    {
        public string HardwareId { get; set; } = string.Empty;
        public string LegacyHardwareId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string SystemModelName { get; set; } = "Monitor";
        public string Nickname { get; set; } = string.Empty;
        public bool IsInternal { get; set; }
        public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsConnected { get; set; }

        public string DisplayName => !string.IsNullOrWhiteSpace(Nickname) ? Nickname : SystemModelName;
    }
}
