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
        public static event EventHandler? HotkeysReloaded;

        public static HotkeyHookWindow? Instance { get; private set; }

        private readonly Dictionary<int, DisplayShortcutRule> _registeredRules = new();
        private int _currentHotkeyId = 0x8000;
        private bool _disposed;

        public HotkeyHookWindow()
        {
            Instance = this;
            CreateHandle(new CreateParams());
            ReloadHotkeys();
        }

        public void ReloadHotkeys()
        {
            UnregisterAll();

            var rules = UserSettings.BrightnessShortcuts;
            if (rules != null)
            {
                foreach (var rule in rules)
                {
                    rule.IsRegistered = false;
                    if (!rule.Enabled) continue;

                    uint fsModifiers = 0;
                    uint vk = 0;

                    if (!string.IsNullOrWhiteSpace(rule.Accelerator))
                    {
                        if (!TryParseAccelerator(rule.Accelerator, out fsModifiers, out vk))
                        {
                            continue;
                        }
                    }
                    else if (rule.Key != Keys.None)
                    {
                        fsModifiers = NativeMethods.MOD_NOREPEAT;
                        if ((rule.Modifiers & Keys.Control) == Keys.Control) fsModifiers |= NativeMethods.MOD_CONTROL;
                        if ((rule.Modifiers & Keys.Alt) == Keys.Alt) fsModifiers |= NativeMethods.MOD_ALT;
                        if ((rule.Modifiers & Keys.Shift) == Keys.Shift) fsModifiers |= NativeMethods.MOD_SHIFT;
                        vk = (uint)rule.Key;
                    }
                    else
                    {
                        continue;
                    }

                    int id = ++_currentHotkeyId;
                    if (NativeMethods.RegisterHotKey(this.Handle, id, fsModifiers, vk))
                    {
                        rule.IsRegistered = true;
                        _registeredRules[id] = rule;
                    }
                }
            }

            HotkeysReloaded?.Invoke(this, EventArgs.Empty);
        }

        public static bool TryParseAccelerator(string accelerator, out uint fsModifiers, out uint vk)
        {
            fsModifiers = NativeMethods.MOD_NOREPEAT;
            vk = 0;

            if (string.IsNullOrWhiteSpace(accelerator)) return false;

            var parts = accelerator.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var p = part.Trim();
                if (string.Equals(p, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p, "Control", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p, "CommandOrControl", StringComparison.OrdinalIgnoreCase))
                {
                    fsModifiers |= NativeMethods.MOD_CONTROL;
                }
                else if (string.Equals(p, "Alt", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(p, "Menu", StringComparison.OrdinalIgnoreCase))
                {
                    fsModifiers |= NativeMethods.MOD_ALT;
                }
                else if (string.Equals(p, "Shift", StringComparison.OrdinalIgnoreCase))
                {
                    fsModifiers |= NativeMethods.MOD_SHIFT;
                }
                else if (string.Equals(p, "Super", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(p, "Win", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(p, "Meta", StringComparison.OrdinalIgnoreCase))
                {
                    fsModifiers |= NativeMethods.MOD_WIN;
                }
                else
                {
                    // Primary Key
                    vk = ParseVirtualKey(p);
                }
            }

            // Must have a valid non-modifier virtual key
            return vk != 0;
        }

        private static uint ParseVirtualKey(string keyName)
        {
            return keyName.ToUpperInvariant() switch
            {
                "RIGHT" => 0x27, // VK_RIGHT
                "LEFT" => 0x25,  // VK_LEFT
                "UP" => 0x26,    // VK_UP
                "DOWN" => 0x28,  // VK_DOWN
                "SPACE" => 0x20,
                "ENTER" or "RETURN" => 0x0D,
                "ESC" or "ESCAPE" => 0x1B,
                "TAB" => 0x09,
                "BACK" or "BACKSPACE" => 0x08,
                "PAGEUP" => 0x21,
                "PAGEDOWN" => 0x22,
                "END" => 0x23,
                "HOME" => 0x24,
                "INSERT" => 0x2D,
                "DELETE" => 0x2E,
                "PLUS" or "=" => 0xBB,
                "MINUS" or "-" => 0xBD,
                ";" => 0xBA,
                "," => 0xBC,
                "." => 0xBE,
                "/" => 0xBF,
                "`" => 0xC0,
                "[" => 0xDB,
                "\\" => 0xDC,
                "]" => 0xDD,
                "'" => 0xDE,
                "NUM0" => 0x60,
                "NUM1" => 0x61,
                "NUM2" => 0x62,
                "NUM3" => 0x63,
                "NUM4" => 0x64,
                "NUM5" => 0x65,
                "NUM6" => 0x66,
                "NUM7" => 0x67,
                "NUM8" => 0x68,
                "NUM9" => 0x69,
                "NUMMULT" => 0x6A,
                "NUMADD" => 0x6B,
                "NUMSUB" => 0x6D,
                "NUMDEC" => 0x6E,
                "NUMDIV" => 0x6F,
                _ => Enum.TryParse<Keys>(keyName, true, out var k) ? (uint)k : 0
            };
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
            if (Instance == this) Instance = null;
            UnregisterAll();
            DestroyHandle();
        }
    }
}
