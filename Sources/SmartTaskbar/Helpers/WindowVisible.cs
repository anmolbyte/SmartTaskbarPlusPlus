namespace SmartTaskbar
{
    public static partial class Fun
    {
        private const int TrayDwmWaCloaked = 14;

        public static bool IsWindowInvisible(this IntPtr handle)
        {
            if (IsWindowVisible(handle) == false) return true;

            int hr = DwmGetWindowAttribute(handle, TrayDwmWaCloaked, out bool cloaked, sizeof(int));
            return hr == 0 && cloaked;
        }
    }
}
