using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SmartTaskbar.Models;
using SmartTaskbar.Worker;

namespace SmartTaskbar.Helpers
{
    public sealed class HotkeyHookWindow : NativeWindow, IDisposable
    {
        public const int WM_SHOWSETTINGS = 0x8001; // WM_USER + 1
        public event EventHandler? ShowSettingsRequested;

        private readonly Dictionary<int, DisplayShortcutRule> _registeredRules = new();
        private int _currentHotkeyId = 0x8000;
        private bool _disposed;

        public HotkeyHookWindow()
        {
            CreateHandle(new CreateParams());
            ReloadHotkeys();
        }

        public void ReloadHotkeys()
        {
            UnregisterAll();

            var rules = UserSettings.BrightnessShortcuts;
            if (rules == null) return;

            foreach (var rule in rules)
            {
                if (!rule.Enabled || rule.Key == Keys.None) continue;

                uint fsModifiers = NativeMethods.MOD_NOREPEAT;
                if ((rule.Modifiers & Keys.Control) == Keys.Control) fsModifiers |= NativeMethods.MOD_CONTROL;
                if ((rule.Modifiers & Keys.Alt) == Keys.Alt) fsModifiers |= NativeMethods.MOD_ALT;
                if ((rule.Modifiers & Keys.Shift) == Keys.Shift) fsModifiers |= NativeMethods.MOD_SHIFT;

                uint vk = (uint)rule.Key;
                int id = ++_currentHotkeyId;

                if (NativeMethods.RegisterHotKey(this.Handle, id, fsModifiers, vk))
                {
                    _registeredRules[id] = rule;
                }
            }
        }

        private void UnregisterAll()
        {
            foreach (var id in _registeredRules.Keys)
            {
                NativeMethods.UnregisterHotKey(this.Handle, id);
            }
            _registeredRules.Clear();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SHOWSETTINGS)
            {
                ShowSettingsRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (_registeredRules.TryGetValue(id, out var rule))
                {
                    BrightnessService.Instance.ExecuteShortcut(rule);
                }
            }
            else if (m.Msg == NativeMethods.WM_DISPLAYCHANGE)
            {
                // Display topology, resolution, or sleep/wake changed
                BrightnessService.Instance.RefreshMonitors();
                ReloadHotkeys();
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            UnregisterAll();
            DestroyHandle();
        }
    }
}
