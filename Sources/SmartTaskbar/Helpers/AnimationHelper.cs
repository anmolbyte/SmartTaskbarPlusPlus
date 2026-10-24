using Microsoft.Win32;

namespace SmartTaskbar
{
    public static partial class Fun
    {
        private const string ExplorerAdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string TaskbarAnimationsValue = "TaskbarAnimations";

        /// <summary>
        ///     Get taskbar animation status
        /// </summary>
        /// <returns></returns>
        public static bool IsEnableTaskbarAnimation()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(ExplorerAdvancedKey, false);
                return ((int?)key?.GetValue(TaskbarAnimationsValue, 1) ?? 1) == 1;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        ///     Change the animation state of the taskbar
        /// </summary>
        /// <returns></returns>
        public static bool ChangeTaskbarAnimation()
        {
            try
            {
                bool newState = !IsEnableTaskbarAnimation();
                using var key = Registry.CurrentUser.OpenSubKey(ExplorerAdvancedKey, true);
                key?.SetValue(TaskbarAnimationsValue, newState ? 1 : 0, RegistryValueKind.DWord);
                return newState;
            }
            catch
            {
                return true;
            }
        }
    }
}
