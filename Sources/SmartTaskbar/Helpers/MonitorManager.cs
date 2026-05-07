using System.Collections.Generic;
using SmartTaskbar.Helpers;
using static SmartTaskbar.Fun;

namespace SmartTaskbar.Helpers
{
    public class MonitorManager
    {
        public static List<IntPtr> GetMonitorHandles()
        {
            var handles = new List<IntPtr>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref TagRect lprcMonitor, IntPtr dwData) =>
            {
                handles.Add(hMonitor);
                return true;
            }, IntPtr.Zero);
            return handles;
        }

        public static string GetMonitorName(IntPtr hMonitor)
        {
            var mi = new MonitorInfoEx();
            mi.Size = System.Runtime.InteropServices.Marshal.SizeOf(mi);
            if (GetMonitorInfo(hMonitor, ref mi))
                return mi.DeviceName;
            return "Unknown";
        }

        private static void DoAction(IntPtr hMonitor, Action<IntPtr> action)
        {
            if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count))
            {
                var physicalMonitors = new PHYSICAL_MONITOR[count];
                if (GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physicalMonitors))
                {
                    try
                    {
                        foreach (var pm in physicalMonitors)
                        {
                            action(pm.hPhysicalMonitor);
                        }
                    }
                    finally
                    {
                        DestroyPhysicalMonitors(count, physicalMonitors);
                    }
                }
            }
        }

        public static void SetBrightness(IntPtr hMonitor, uint brightness)
        {
            DoAction(hMonitor, (pm) => SetMonitorBrightness(pm, brightness));
        }

        public static uint GetBrightness(IntPtr hMonitor)
        {
            uint result = 0;
            DoAction(hMonitor, (pm) =>
            {
                if (GetMonitorBrightness(pm, out _, out uint current, out _))
                    result = current;
            });
            return result;
        }

        public static void SetContrast(IntPtr hMonitor, uint contrast)
        {
            DoAction(hMonitor, (pm) => SetMonitorContrast(pm, contrast));
        }

        public static uint GetContrast(IntPtr hMonitor)
        {
            uint result = 0;
            DoAction(hMonitor, (pm) =>
            {
                if (GetMonitorContrast(pm, out _, out uint current, out _))
                    result = current;
            });
            return result;
        }
    }
}
