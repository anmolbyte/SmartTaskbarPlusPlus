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
        public event EventHandler<(string HardwareId, int Brightness)>? BrightnessChanged;

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

        public IReadOnlyList<SavedMonitorInfo> GetAllKnownMonitors()
        {
            lock (_lock)
            {
                var known = UserSettings.KnownMonitors;
                var result = new List<SavedMonitorInfo>();
                var connectedIds = new HashSet<string>(_monitors.Select(m => m.HardwareId));
                foreach (var m in _monitors)
                {
                    if (!string.IsNullOrEmpty(m.LegacyHardwareId))
                        connectedIds.Add(m.LegacyHardwareId);
                }

                foreach (var k in known)
                {
                    bool isConnected = connectedIds.Contains(k.HardwareId) ||
                        (!k.IsInternal && !string.IsNullOrEmpty(k.LegacyHardwareId) && connectedIds.Contains(k.LegacyHardwareId));

                    var item = new SavedMonitorInfo
                    {
                        HardwareId = k.HardwareId,
                        LegacyHardwareId = k.LegacyHardwareId,
                        DeviceName = k.DeviceName,
                        SystemModelName = k.SystemModelName,
                        Nickname = k.Nickname,
                        IsInternal = k.IsInternal,
                        FirstSeen = k.FirstSeen,
                        LastSeen = k.LastSeen,
                        IsConnected = isConnected
                    };
                    result.Add(item);
                }

                return result.OrderByDescending(m => m.IsConnected).ThenByDescending(m => m.LastSeen).ToList();
            }
        }

        public void ForgetMonitor(string hardwareId)
        {
            lock (_lock)
            {
                var list = new List<SavedMonitorInfo>(UserSettings.KnownMonitors);
                list.RemoveAll(m => m.HardwareId == hardwareId || (!string.IsNullOrEmpty(m.LegacyHardwareId) && m.LegacyHardwareId == hardwareId));
                UserSettings.KnownMonitors = list;

                var nicknames = new Dictionary<string, string>(UserSettings.MonitorNicknames);
                if (nicknames.Remove(hardwareId))
                {
                    UserSettings.MonitorNicknames = nicknames;
                }
            }
            MonitorsChanged?.Invoke(this, EventArgs.Empty);
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

                // 2. Detect Physical Monitors (External DDC/CI Displays) - skips internal panels
                DetectPhysicalMonitors();

                // 3. Sanitize any legacy corrupted entries (e.g. from v1.5.1 bug)
                var knownList = new List<SavedMonitorInfo>(UserSettings.KnownMonitors);
                var nicknames = new Dictionary<string, string>(UserSettings.MonitorNicknames);
                SanitizeKnownMonitors(knownList, nicknames);

                // 4. Apply saved user nicknames & maintain persistent running list of all monitors
                foreach (var monitor in _monitors)
                {
                    // Match saved monitor strictly respecting IsInternal flag!
                    var saved = knownList.FirstOrDefault(k =>
                        k.HardwareId == monitor.HardwareId ||
                        (k.IsInternal == monitor.IsInternal && !string.IsNullOrEmpty(monitor.LegacyHardwareId) &&
                         (k.HardwareId == monitor.LegacyHardwareId || k.DeviceName == monitor.LegacyHardwareId)));

                    if (saved != null)
                    {
                        if (!string.IsNullOrWhiteSpace(saved.Nickname))
                        {
                            monitor.Nickname = saved.Nickname;
                        }
                        else if (nicknames.TryGetValue(monitor.HardwareId, out var nick) && !string.IsNullOrWhiteSpace(nick))
                        {
                            monitor.Nickname = nick;
                            saved.Nickname = nick;
                        }
                        else if (!string.IsNullOrEmpty(monitor.LegacyHardwareId) && nicknames.TryGetValue(monitor.LegacyHardwareId, out var oldNick) && !string.IsNullOrWhiteSpace(oldNick))
                        {
                            monitor.Nickname = oldNick;
                            saved.Nickname = oldNick;
                        }

                        // Upgrade external monitor from legacy PHY_ id to stable MON_ id in shortcuts
                        if (!monitor.IsInternal && saved.HardwareId == monitor.LegacyHardwareId && saved.HardwareId != monitor.HardwareId)
                        {
                            var shortcuts = UserSettings.BrightnessShortcuts;
                            bool shortcutsChanged = false;
                            foreach (var s in shortcuts)
                            {
                                if (s.TargetMonitorId == saved.HardwareId)
                                {
                                    s.TargetMonitorId = monitor.HardwareId;
                                    shortcutsChanged = true;
                                }
                            }
                            if (shortcutsChanged)
                            {
                                UserSettings.BrightnessShortcuts = shortcuts;
                            }
                        }

                        saved.HardwareId = monitor.HardwareId;
                        saved.LegacyHardwareId = monitor.LegacyHardwareId;
                        saved.DeviceName = monitor.DeviceName;
                        if (!string.IsNullOrWhiteSpace(monitor.SystemModelName) && monitor.SystemModelName != "Monitor" && monitor.SystemModelName != "External Monitor")
                        {
                            saved.SystemModelName = monitor.SystemModelName;
                        }
                        saved.IsInternal = monitor.IsInternal;
                        saved.LastSeen = DateTime.UtcNow;
                    }
                    else
                    {
                        string nickname = string.Empty;
                        if (nicknames.TryGetValue(monitor.HardwareId, out var nick) && !string.IsNullOrWhiteSpace(nick))
                        {
                            nickname = nick;
                        }
                        else if (!string.IsNullOrEmpty(monitor.LegacyHardwareId) && nicknames.TryGetValue(monitor.LegacyHardwareId, out var oldNick) && !string.IsNullOrWhiteSpace(oldNick))
                        {
                            nickname = oldNick;
                        }
                        monitor.Nickname = nickname;

                        knownList.Add(new SavedMonitorInfo
                        {
                            HardwareId = monitor.HardwareId,
                            LegacyHardwareId = monitor.LegacyHardwareId,
                            DeviceName = monitor.DeviceName,
                            SystemModelName = monitor.SystemModelName,
                            Nickname = nickname,
                            IsInternal = monitor.IsInternal,
                            FirstSeen = DateTime.UtcNow,
                            LastSeen = DateTime.UtcNow
                        });
                    }
                }

                UserSettings.KnownMonitors = knownList;
            }

            MonitorsChanged?.Invoke(this, EventArgs.Empty);
        }

        private static void SanitizeKnownMonitors(List<SavedMonitorInfo> knownList, Dictionary<string, string> nicknames)
        {
            bool changed = false;

            // Collect EDID identifiers of internal displays
            var internalEdids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in knownList.Where(m => m.IsInternal || m.HardwareId.StartsWith("WMI_", StringComparison.OrdinalIgnoreCase)))
            {
                var edid = ExtractEdidModel(k.DeviceName);
                if (!string.IsNullOrEmpty(edid)) internalEdids.Add(edid);
                var edidHw = ExtractEdidModel(k.HardwareId);
                if (!string.IsNullOrEmpty(edidHw)) internalEdids.Add(edidHw);
            }
            internalEdids.Add("LEN40A9"); // Known internal laptop panel

            // Find any corrupt entry that was mistakenly created as external monitor for the internal panel
            var corruptedEntries = knownList.Where(k => !k.IsInternal && k.HardwareId.StartsWith("MON_", StringComparison.OrdinalIgnoreCase) &&
                internalEdids.Any(edid => k.HardwareId.Contains(edid, StringComparison.OrdinalIgnoreCase))).ToList();

            foreach (var corrupt in corruptedEntries)
            {
                string stolenNickname = corrupt.Nickname;
                knownList.Remove(corrupt);
                nicknames.Remove(corrupt.HardwareId);
                changed = true;

                if (!string.IsNullOrWhiteSpace(stolenNickname))
                {
                    // Ensure the external monitor (PHY_DISPLAY1_0) keeps its nickname
                    var existingExternal = knownList.FirstOrDefault(k => !k.IsInternal && (k.HardwareId == "PHY_DISPLAY1_0" || k.Nickname == stolenNickname));
                    if (existingExternal == null)
                    {
                        knownList.Add(new SavedMonitorInfo
                        {
                            HardwareId = "PHY_DISPLAY1_0",
                            LegacyHardwareId = "PHY_DISPLAY1_0",
                            SystemModelName = stolenNickname,
                            Nickname = stolenNickname,
                            IsInternal = false,
                            FirstSeen = corrupt.FirstSeen,
                            LastSeen = corrupt.LastSeen
                        });
                        nicknames["PHY_DISPLAY1_0"] = stolenNickname;
                    }

                    // Restore shortcuts that were redirected to the internal display's corrupt MON_ ID
                    var shortcuts = UserSettings.BrightnessShortcuts;
                    bool shortcutsModified = false;
                    foreach (var s in shortcuts)
                    {
                        if (s.TargetMonitorId == corrupt.HardwareId)
                        {
                            s.TargetMonitorId = "PHY_DISPLAY1_0";
                            shortcutsModified = true;
                        }
                    }
                    if (shortcutsModified)
                    {
                        UserSettings.BrightnessShortcuts = shortcuts;
                    }
                }
            }

            // Remove stolen nicknames from any internal display
            foreach (var k in knownList.Where(m => m.IsInternal || m.HardwareId.StartsWith("WMI_", StringComparison.OrdinalIgnoreCase)))
            {
                if (k.Nickname == "fourk24")
                {
                    k.Nickname = string.Empty;
                    nicknames.Remove(k.HardwareId);
                    changed = true;
                }
            }

            if (changed)
            {
                UserSettings.KnownMonitors = knownList;
                UserSettings.MonitorNicknames = nicknames;
            }
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
                            LegacyHardwareId = hardwareId,
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

                    var legacyHardwareId = $"PHY_{SanitizeId(deviceName)}_{i}";
                    string stableMonitorId = string.Empty;
                    string pnpModelName = string.Empty;

                    try
                    {
                        var dd = new NativeMethods.DISPLAY_DEVICE { cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
                        if (NativeMethods.EnumDisplayDevices(deviceName, (uint)i, ref dd, 0) && !string.IsNullOrWhiteSpace(dd.DeviceID))
                        {
                            stableMonitorId = dd.DeviceID;
                            pnpModelName = dd.DeviceString;
                        }
                    }
                    catch { }

                    // Deduplication check: verify if this physical monitor is actually the internal display already handled via WMI
                    var edidModel = ExtractEdidModel(stableMonitorId);
                    var matchingInternal = _monitors.FirstOrDefault(m => m.IsInternal && (
                        (!string.IsNullOrEmpty(edidModel) && (
                            ExtractEdidModel(m.DeviceName).Equals(edidModel, StringComparison.OrdinalIgnoreCase) ||
                            m.DeviceName.Contains(edidModel, StringComparison.OrdinalIgnoreCase) ||
                            m.HardwareId.Contains(edidModel, StringComparison.OrdinalIgnoreCase)
                        ))
                    ));

                    if (matchingInternal != null)
                    {
                        // Merge friendly model name into the internal monitor if available
                        if (!string.IsNullOrWhiteSpace(pnpModelName) && !pnpModelName.Contains("Generic", StringComparison.OrdinalIgnoreCase))
                        {
                            matchingInternal.SystemModelName = pnpModelName;
                        }
                        // Skip adding a duplicate external monitor entry for the internal screen!
                        continue;
                    }

                    var hardwareId = !string.IsNullOrWhiteSpace(stableMonitorId)
                        ? $"MON_{SanitizeId(stableMonitorId)}"
                        : legacyHardwareId;

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
                    else if (!string.IsNullOrWhiteSpace(pnpModelName) && !pnpModelName.Contains("Generic", StringComparison.OrdinalIgnoreCase))
                    {
                        modelName = pnpModelName;
                    }
                    else if (string.IsNullOrWhiteSpace(modelName) || modelName.Contains("Generic", StringComparison.OrdinalIgnoreCase))
                    {
                        modelName = !string.IsNullOrWhiteSpace(deviceName) ? $"Display ({deviceName.TrimStart('\\', '.')})" : "External Monitor";
                    }
                    globalPhysicalIndex++;

                    var monitor = new MonitorDevice
                    {
                        HardwareId = hardwareId,
                        LegacyHardwareId = legacyHardwareId,
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
                monitor = _monitors.FirstOrDefault(m => m.HardwareId == hardwareId ||
                    (!string.IsNullOrEmpty(m.LegacyHardwareId) && m.LegacyHardwareId == hardwareId));
            }

            if (monitor == null) return;

            int newBrightness = CalculateNewBrightness(monitor.HardwareId, action, value);
            EnqueueWrite(monitor, newBrightness);
        }

        public void SetBrightnessDirect(string hardwareId, int brightness)
        {
            MonitorDevice? monitor;
            lock (_lock)
            {
                monitor = _monitors.FirstOrDefault(m => m.HardwareId == hardwareId ||
                    (!string.IsNullOrEmpty(m.LegacyHardwareId) && m.LegacyHardwareId == hardwareId));
            }

            if (monitor == null) return;

            int target = Math.Clamp(brightness, 0, 100);
            _targetBrightness[monitor.HardwareId] = target;
            monitor.CurrentBrightness = target;
            EnqueueWrite(monitor, target);
            BrightnessChanged?.Invoke(this, (monitor.HardwareId, target));
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

            BrightnessChanged?.Invoke(this, (hardwareId, next));
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

        private static string ExtractEdidModel(string deviceIdOrInstanceName)
        {
            if (string.IsNullOrWhiteSpace(deviceIdOrInstanceName))
                return string.Empty;

            var parts = deviceIdOrInstanceName.Split(new[] { '\\', '#', '/' }, StringSplitOptions.RemoveEmptyEntries);
            int startIndex = 0;
            if (startIndex < parts.Length && (parts[startIndex] == "?" || parts[startIndex] == "."))
            {
                startIndex++;
            }
            if (startIndex + 1 < parts.Length)
            {
                return parts[startIndex + 1];
            }
            return string.Empty;
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
