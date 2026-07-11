using System;
using System.Runtime.InteropServices;

namespace SmartTaskbar
{
    public static partial class Fun
    {
        private const int TrayAbsAutoHide = 1;

        private const int TrayAbsAlwaysOnTop = 2;

        private const uint TrayAbmSetState = 10;

        private const uint TrayAbmGetState = 4;
        private static AppbarData _msg;

        /// <summary>
        ///     Set taskbar to Auto-Hide
        /// </summary>
        public static void SetAutoHide()
        {
            if (!IsNotAutoHide())
                return;

            _msg.lParam = TrayAbsAutoHide;

            _ = SHAppBarMessage(TrayAbmSetState, ref _msg);
        }

        public static bool IsNotAutoHide()
            => SHAppBarMessage(TrayAbmGetState, ref _msg) == IntPtr.Zero;

        /// <summary>
        ///     Change Auto-Hide status
        /// </summary>
        public static void ChangeAutoHide()
        {
            _msg.lParam = IsNotAutoHide() ? TrayAbsAutoHide : TrayAbsAlwaysOnTop;
            _ = SHAppBarMessage(TrayAbmSetState, ref _msg);
        }

        /// <summary>
        ///     Set taskbar to Always-On-Top
        /// </summary>
        public static void CancelAutoHide()
        {
            if (IsNotAutoHide())
                return;

            _msg.lParam = TrayAbsAlwaysOnTop;

            _ = SHAppBarMessage(TrayAbmSetState, ref _msg);
        }

        private static double? _primaryDiagonalCache;
        private static double? _maxDiagonalCache;

        static Fun()
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (s, e) =>
            {
                _primaryDiagonalCache = null;
                _maxDiagonalCache = null;
            };
        }

        public static double GetPrimaryDisplayDiagonalInches()
        {
            if (_primaryDiagonalCache.HasValue) return _primaryDiagonalCache.Value;

            var hdc = GetDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero) return 0;

            try
            {
                var widthMm = GetDeviceCaps(hdc, HORZSIZE);
                var heightMm = GetDeviceCaps(hdc, VERTSIZE);

                var diagonalMm = Math.Sqrt(Math.Pow(widthMm, 2) + Math.Pow(heightMm, 2));
                _primaryDiagonalCache = diagonalMm / 25.4;
                return _primaryDiagonalCache.Value;
            }
            finally
            {
                _ = ReleaseDC(IntPtr.Zero, hdc);
            }
        }
        public static double GetMaxDisplayDiagonalInches()
        {
            if (_maxDiagonalCache.HasValue) return _maxDiagonalCache.Value;

            var maxDiagonal = 0.0;
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref TagRect lprcMonitor, IntPtr dwData) =>
            {
                var hdc = CreateDC("DISPLAY", DeviceName(hMonitor), null, IntPtr.Zero);
                if (hdc != IntPtr.Zero)
                {
                    try
                    {
                        var widthMm = GetDeviceCaps(hdc, HORZSIZE);
                        var heightMm = GetDeviceCaps(hdc, VERTSIZE);
                        var diagonalInches = Math.Sqrt(Math.Pow(widthMm, 2) + Math.Pow(heightMm, 2)) / 25.4;
                        if (diagonalInches > maxDiagonal) maxDiagonal = diagonalInches;
                    }
                    finally
                    {
                        DeleteDC(hdc);
                    }
                }
                return true;
            }, IntPtr.Zero);
            
            _maxDiagonalCache = maxDiagonal;
            return _maxDiagonalCache.Value;
        }

        private static string DeviceName(IntPtr hMonitor)
        {
            var mi = new MonitorInfoEx();
            mi.Size = Marshal.SizeOf(mi);
            if (GetMonitorInfo(hMonitor, ref mi))
                return mi.DeviceName;
            return null;
        }
    }
}
