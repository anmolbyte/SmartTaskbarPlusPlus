using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SmartTaskbar.Helpers;
using SmartTaskbar.Models;

namespace SmartTaskbar.Worker
{
    public sealed class BrightnessService : IDisposable
    {
        private static readonly Lazy<BrightnessService> _instance = new(() => new BrightnessService());
        public static BrightnessService Instance => _instance.Value;

        private readonly object _lock = new();
        private readonly List<MonitorDevice> _monitors = new();
        private readonly List<NativeMethods.PHYSICAL_MONITOR> _allocatedPhysicalMonitors = new();
        private readonly ConcurrentDictionary<string, int> _targetBrightness = new();
        private readonly ConcurrentDictionary<string, Task> _writeTasks = new();
        private bool _disposed;

        public event EventHandler? MonitorsChanged;

        private BrightnessService()
        {
            RefreshMonitors();
        }

        public IReadOnlyList<MonitorDevice> GetMonitors()
        {
            lock (_lock)
            {
                return _monitors.ToList();
            }
        }

        public void RefreshMonitors()
        {
            lock (_lock)
            {
                if (_disposed) return;

                CleanupPhysicalHandles();
                _monitors.Clear();

                // 1. Detect WMI Monitors (Internal Laptop Displays)
                DetectWmiMonitors();

                // 2. Detect Physical Monitors (External DDC/CI Displays)
                DetectPhysicalMonitors();

                // Apply saved user nicknames
                var nicknames = UserSettings.MonitorNicknames;
                foreach (var monitor in _monitors)
                {
                    if (nicknames.TryGetValue(monitor.HardwareId, out var nickname) && !string.IsNullOrWhiteSpace(nickname))
                    {
                        monitor.Nickname = nickname;
                    }
                }
            }

            MonitorsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void DetectWmiMonitors()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightness");
                using var results = searcher.Get();

                int index = 0;
                foreach (ManagementObject obj in results)
                {
                    using (obj)
                    {
                        var instanceName = obj["InstanceName"]?.ToString() ?? $"InternalDisplay_{index}";
                        var current = Convert.ToInt32(obj["CurrentBrightness"]);
                        var levels = obj["Level"] as byte[] ?? Array.Empty<byte>();

                        var hardwareId = $"WMI_{SanitizeId(instanceName)}";
                        var friendlyName = GetWmiFriendlyName(instanceName) ?? "Built-in Display";

                        var monitor = new MonitorDevice
                        {
                            HardwareId = hardwareId,
                            DeviceName = instanceName,
                            SystemModelName = friendlyName,
                            CurrentBrightness = Math.Clamp(current, 0, 100),
                            MinBrightness = (uint)(levels.Length > 0 ? levels.Min() : 0),
                            MaxBrightness = (uint)(levels.Length > 0 ? levels.Max() : 100),
                            IsInternal = true,
                            PhysicalHandle = IntPtr.Zero,
                            PhysicalIndex = 0
                        };

                        _monitors.Add(monitor);
                        _targetBrightness[hardwareId] = monitor.CurrentBrightness;
                        index++;
                    }
                }
            }
            catch
            {
                // WMI is not supported on all machines (e.g. desktops with no built-in screen)
            }
        }

        private void DetectPhysicalMonitors()
        {
            var hMonitors = new List<IntPtr>();

            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr _, ref TagRect _, IntPtr _) =>
                {
                    hMonitors.Add(hMonitor);
                    return true;
                },
                IntPtr.Zero);

            var friendlyNames = GetAllWmiModelNames();
            int globalPhysicalIndex = 0;

            foreach (var hMonitor in hMonitors)
            {
                if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) || count == 0)
                    continue;

                var physicalMonitors = new NativeMethods.PHYSICAL_MONITOR[count];
                if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physicalMonitors))
                    continue;

                var monitorInfo = new NativeMethods.MonitorInfoEx { Size = Marshal.SizeOf<NativeMethods.MonitorInfoEx>() };
                string deviceName = string.Empty;
                if (NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    deviceName = monitorInfo.DeviceName;
                }

                for (int i = 0; i < physicalMonitors.Length; i++)
                {
                    var pm = physicalMonitors[i];
                    _allocatedPhysicalMonitors.Add(pm);

                    var hardwareId = $"PHY_{SanitizeId(deviceName)}_{i}";

                    // Query initial brightness: try low-level VESA MCCS VCP 0x10 first, then high-level
                    uint min = 0, current = 50, max = 100;
                    uint pvct = 0, vcpCur = 0, vcpMax = 0;
                    if (NativeMethods.GetVCPFeatureAndVCPFeatureReply(pm.hPhysicalMonitor, 0x10, out pvct, out vcpCur, out vcpMax) && vcpMax > 0)
                    {
                        current = vcpCur;
                        max = vcpMax;
                    }
                    else if (NativeMethods.GetMonitorBrightness(pm.hPhysicalMonitor, out min, out current, out max))
                    {
                        // Succeeded via high level
                    }

                    string modelName = pm.szPhysicalMonitorDescription;
                    if (globalPhysicalIndex < friendlyNames.Count && !string.IsNullOrWhiteSpace(friendlyNames[globalPhysicalIndex]))
                    {
                        modelName = friendlyNames[globalPhysicalIndex];
                    }
                    else if (string.IsNullOrWhiteSpace(modelName) || modelName.Contains("Generic", StringComparison.OrdinalIgnoreCase))
                    {
                        modelName = !string.IsNullOrWhiteSpace(deviceName) ? $"Display ({deviceName.TrimStart('\\', '.')})" : "External Monitor";
                    }
                    globalPhysicalIndex++;

                    var monitor = new MonitorDevice
                    {
                        HardwareId = hardwareId,
                        DeviceName = deviceName,
                        SystemModelName = modelName,
                        CurrentBrightness = (int)Math.Clamp(current, 0, 100),
                        MinBrightness = min,
                        MaxBrightness = max,
                        IsInternal = false,
                        PhysicalHandle = pm.hPhysicalMonitor,
                        PhysicalIndex = i
                    };

                    _monitors.Add(monitor);
                    _targetBrightness[hardwareId] = monitor.CurrentBrightness;
                }
            }
        }

        private static List<string> GetAllWmiModelNames()
        {
            var list = new List<string>();
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT UserFriendlyName FROM WmiMonitorID");
                using var results = searcher.Get();
                foreach (ManagementObject obj in results)
                {
                    using (obj)
                    {
                        if (obj["UserFriendlyName"] is ushort[] charCodes && charCodes.Length > 0)
                        {
                            var name = new string(charCodes.TakeWhile(c => c != 0).Select(c => (char)c).ToArray());
                            if (!string.IsNullOrWhiteSpace(name)) list.Add(name.Trim());
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        private static string? GetWmiFriendlyName(string instanceName)
        {
            try
            {
                var baseName = instanceName.Contains('_') ? instanceName[..instanceName.LastIndexOf('_')] : instanceName;
                using var searcher = new ManagementObjectSearcher(
                    @"root\WMI",
                    $"SELECT UserFriendlyName FROM WmiMonitorID WHERE InstanceName LIKE '{baseName.Replace("\\", "\\\\")}%'");

                using var results = searcher.Get();
                foreach (ManagementObject obj in results)
                {
                    using (obj)
                    {
                        if (obj["UserFriendlyName"] is ushort[] charCodes && charCodes.Length > 0)
                        {
                            var name = new string(charCodes.TakeWhile(c => c != 0).Select(c => (char)c).ToArray());
                            if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public void ExecuteShortcut(DisplayShortcutRule rule)
        {
            if (!rule.Enabled) return;

            if (rule.TargetMonitorId == "ALL")
            {
                AdjustAll(rule.Action, rule.Value);
            }
            else
            {
                AdjustSingle(rule.TargetMonitorId, rule.Action, rule.Value);
            }
        }

        public void AdjustAll(ShortcutAction action, int value)
        {
            List<MonitorDevice> snapshot;
            lock (_lock)
            {
                snapshot = _monitors.ToList();
            }

            foreach (var monitor in snapshot)
            {
                int newBrightness = CalculateNewBrightness(monitor.HardwareId, action, value);
                EnqueueWrite(monitor, newBrightness);
            }
        }

        public void AdjustSingle(string hardwareId, ShortcutAction action, int value)
        {
            MonitorDevice? monitor;
            lock (_lock)
            {
                monitor = _monitors.FirstOrDefault(m => m.HardwareId == hardwareId);
            }

            if (monitor == null) return;

            int newBrightness = CalculateNewBrightness(hardwareId, action, value);
            EnqueueWrite(monitor, newBrightness);
        }

        public void SetBrightnessDirect(string hardwareId, int brightness)
        {
            MonitorDevice? monitor;
            lock (_lock)
            {
                monitor = _monitors.FirstOrDefault(m => m.HardwareId == hardwareId);
            }

            if (monitor == null) return;

            int target = Math.Clamp(brightness, 0, 100);
            _targetBrightness[hardwareId] = target;
            monitor.CurrentBrightness = target;
            EnqueueWrite(monitor, target);
        }

        private int CalculateNewBrightness(string hardwareId, ShortcutAction action, int value)
        {
            int current = _targetBrightness.TryGetValue(hardwareId, out var existing) ? existing : 50;

            int next = action switch
            {
                ShortcutAction.Increase => current + value,
                ShortcutAction.Decrease => current - value,
                ShortcutAction.SetTo => value,
                _ => current
            };

            next = Math.Clamp(next, 0, 100);
            _targetBrightness[hardwareId] = next;

            lock (_lock)
            {
                var mon = _monitors.FirstOrDefault(m => m.HardwareId == hardwareId);
                if (mon != null) mon.CurrentBrightness = next;
            }

            return next;
        }

        private void EnqueueWrite(MonitorDevice monitor, int targetBrightness)
        {
            // Coalesce / Leapfrog writes so we never block or queue unnecessary intermediate writes
            _writeTasks.AddOrUpdate(monitor.HardwareId,
                _ => Task.Run(() => ProcessMonitorWrite(monitor)),
                (_, existingTask) =>
                {
                    if (existingTask.IsCompleted)
                    {
                        return Task.Run(() => ProcessMonitorWrite(monitor));
                    }
                    // A write is already in flight. Because _targetBrightness was updated,
                    // the in-flight task or immediate followup will pick up the latest target value automatically.
                    return existingTask.ContinueWith(_ => ProcessMonitorWrite(monitor), TaskScheduler.Default);
                });
        }

        private void ProcessMonitorWrite(MonitorDevice monitor)
        {
            if (!_targetBrightness.TryGetValue(monitor.HardwareId, out int target))
                return;

            try
            {
                if (monitor.IsInternal)
                {
                    SetWmiBrightness(target);
                    return;
                }

                // DDC/CI dock & MST bus safety:
                // 1. Try low-level VESA MCCS VCP 0x10 (Luminance) first, as used by Twinkle Tray & ClickMonitorDDC.
                // 2. Try high-level Windows SetMonitorBrightness as fallback.
                // 3. If both fail or handle was invalidated (common with docks / MST renegotiation),
                //    dynamically re-acquire a fresh physical handle for this display and retry!
                bool written = false;
                if (monitor.PhysicalHandle != IntPtr.Zero)
                {
                    written = NativeMethods.SetVCPFeature(monitor.PhysicalHandle, 0x10, (uint)target);
                    if (!written)
                    {
                        written = NativeMethods.SetMonitorBrightness(monitor.PhysicalHandle, (uint)target);
                    }
                }

                if (!written)
                {
                    var freshHandle = ReacquirePhysicalHandle(monitor);
                    if (freshHandle != IntPtr.Zero)
                    {
                        monitor.PhysicalHandle = freshHandle;
                        _ = NativeMethods.SetVCPFeature(freshHandle, 0x10, (uint)target)
                         || NativeMethods.SetMonitorBrightness(freshHandle, (uint)target);
                    }
                }

                // Brief bus stabilization pause (30ms) to prevent I2C bus collision on MST hubs/docks
                Thread.Sleep(30);
            }
            catch
            {
                // Swallow communication errors on physical I2C disconnection
            }
        }

        private IntPtr ReacquirePhysicalHandle(MonitorDevice monitor)
        {
            IntPtr result = IntPtr.Zero;
            try
            {
                NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                    (IntPtr hMon, IntPtr _, ref TagRect _, IntPtr _) =>
                    {
                        var mi = new NativeMethods.MonitorInfoEx { Size = Marshal.SizeOf<NativeMethods.MonitorInfoEx>() };
                        if (NativeMethods.GetMonitorInfo(hMon, ref mi) && mi.DeviceName == monitor.DeviceName)
                        {
                            if (NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint count) && count > 0)
                            {
                                var pms = new NativeMethods.PHYSICAL_MONITOR[count];
                                if (NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMon, count, pms))
                                {
                                    int idx = Math.Clamp(monitor.PhysicalIndex, 0, (int)count - 1);
                                    result = pms[idx].hPhysicalMonitor;
                                    lock (_lock)
                                    {
                                        _allocatedPhysicalMonitors.AddRange(pms);
                                    }
                                }
                            }
                            return false;
                        }
                        return true;
                    }, IntPtr.Zero);
            }
            catch { }

            return result;
        }

        private static void SetWmiBrightness(int brightness)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
                using var results = searcher.Get();
                foreach (ManagementObject obj in results)
                {
                    using (obj)
                    {
                        obj.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)brightness });
                        return;
                    }
                }
            }
            catch { }
        }

        private static string SanitizeId(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "Unknown";
            return new string(input.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        }

        private void CleanupPhysicalHandles()
        {
            if (_allocatedPhysicalMonitors.Count > 0)
            {
                try
                {
                    NativeMethods.DestroyPhysicalMonitors((uint)_allocatedPhysicalMonitors.Count, _allocatedPhysicalMonitors.ToArray());
                }
                catch { }
                _allocatedPhysicalMonitors.Clear();
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                CleanupPhysicalHandles();
                _monitors.Clear();
            }
        }
    }
}
