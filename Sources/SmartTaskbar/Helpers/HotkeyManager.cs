using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SmartTaskbar.Models;
using SmartTaskbar.Helpers;

namespace SmartTaskbar.Helpers
{
    public class HotkeyManager : IDisposable
    {
        private class HotkeyWindow : NativeWindow
        {
            private const int WM_HOTKEY = 0x0312;
            private Action<int> _onHotkey;

            public HotkeyWindow(Action<int> onHotkey)
            {
                CreateHandle(new CreateParams());
                _onHotkey = onHotkey;
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    _onHotkey?.Invoke((int)m.WParam);
                }
                base.WndProc(ref m);
            }
        }

        private HotkeyWindow _window;
        private List<HotkeyConfig> _configs;

        public HotkeyManager(List<HotkeyConfig> configs)
        {
            _configs = configs;
            _window = new HotkeyWindow(OnHotkeyTriggered);
            RegisterAll();
        }

        private void RegisterAll()
        {
            if (_configs == null) return;
            for (int i = 0; i < _configs.Count; i++)
            {
                Fun.RegisterHotKey(_window.Handle, i, _configs[i].Modifiers, _configs[i].Key);
            }
        }

        private void OnHotkeyTriggered(int id)
        {
            if (id >= 0 && id < _configs.Count)
            {
                ExecuteAction(_configs[id]);
            }
        }

        private void ExecuteAction(HotkeyConfig config)
        {
            var monitors = MonitorManager.GetMonitorHandles();
            foreach (var hMonitor in monitors)
            {
                var deviceName = MonitorManager.GetMonitorName(hMonitor);
                if (config.TargetMonitor != "All" && deviceName != config.TargetMonitor)
                {
                    // Check if target is "Primary"
                    var mi = new Fun.MonitorInfoEx();
                    mi.Size = System.Runtime.InteropServices.Marshal.SizeOf(mi);
                    if (Fun.GetMonitorInfo(hMonitor, ref mi))
                    {
                        if (config.TargetMonitor == "Primary" && (mi.Flags & 1) == 0) continue; // 1 = MONITORINFOF_PRIMARY
                        if (config.TargetMonitor != "Primary" && deviceName != config.TargetMonitor) continue;
                    }
                }

                switch (config.Action)
                {
                    case HotkeyAction.BrightnessUp:
                        MonitorManager.SetBrightness(hMonitor, Math.Min(100, MonitorManager.GetBrightness(hMonitor) + (uint)config.Value));
                        break;
                    case HotkeyAction.BrightnessDown:
                        MonitorManager.SetBrightness(hMonitor, (uint)Math.Max(0, (int)MonitorManager.GetBrightness(hMonitor) - config.Value));
                        break;
                    case HotkeyAction.BrightnessSet:
                        MonitorManager.SetBrightness(hMonitor, (uint)config.Value);
                        break;
                    case HotkeyAction.ContrastUp:
                        MonitorManager.SetContrast(hMonitor, Math.Min(100, MonitorManager.GetContrast(hMonitor) + (uint)config.Value));
                        break;
                    case HotkeyAction.ContrastDown:
                        MonitorManager.SetContrast(hMonitor, (uint)Math.Max(0, (int)MonitorManager.GetContrast(hMonitor) - config.Value));
                        break;
                    case HotkeyAction.ContrastSet:
                        MonitorManager.SetContrast(hMonitor, (uint)config.Value);
                        break;
                    case HotkeyAction.ToggleInversion:
                        UserSettings.IsNegativeModeEnabled = !UserSettings.IsNegativeModeEnabled;
                        break;
                }
            }
        }

        public void Dispose()
        {
            if (_window != null)
            {
                for (int i = 0; i < _configs.Count; i++)
                {
                    Fun.UnregisterHotKey(_window.Handle, i);
                }
                _window.DestroyHandle();
            }
        }
    }
}
