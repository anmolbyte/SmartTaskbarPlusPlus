namespace SmartTaskbar.Models
{
    public struct MonitorConfig
    {
        public string DeviceName { get; set; }
        public uint Brightness { get; set; }
        public uint Contrast { get; set; }
        public uint Volume { get; set; }
    }

    public struct HotkeyConfig
    {
        public string Name { get; set; }
        public uint Modifiers { get; set; }
        public uint Key { get; set; }
        public HotkeyAction Action { get; set; }
        public string TargetMonitor { get; set; } // DeviceName or "All" or "Primary"
        public int Value { get; set; } // e.g. +10, -10, or absolute 50
    }

    public enum HotkeyAction
    {
        BrightnessUp,
        BrightnessDown,
        BrightnessSet,
        ContrastUp,
        ContrastDown,
        ContrastSet,
        VolumeUp,
        VolumeDown,
        VolumeSet,
        ToggleInversion
    }
}
