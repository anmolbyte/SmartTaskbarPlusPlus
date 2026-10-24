using Windows.UI.ViewManagement;
using Microsoft.Win32;

namespace SmartTaskbar
{
    public static partial class Fun
    {
        public static readonly UISettings UiSettings = new();

        /// <summary>
        ///     Determine whether it is a light theme
        /// </summary>
        /// <returns></returns>
        public static bool IsLightTheme()
        {
            try
            {
                using var personalizeKey =
                    Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);

                return ((int?)personalizeKey?.GetValue("SystemUsesLightTheme", 0) ?? 0) == 1;
            }
            catch
            {
                return false;
            }
        }
    }
}
