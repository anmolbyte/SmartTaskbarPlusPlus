using Windows.Storage;

namespace SmartTaskbar
{
    public class UserSettings
    {
        private static UserConfiguration _userConfiguration;
        private static readonly bool _isPackaged;

        /// <summary>
        ///     ctor
        /// </summary>
        static UserSettings()
        {
            try
            {
                var autoMode =
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.AutoModeType)] as string;

                _userConfiguration = new UserConfiguration
                {
                    AutoModeType = autoMode == nameof(AutoModeType.None) ? AutoModeType.None : AutoModeType.Auto,
                    ShowTaskbarWhenExit =
                        ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.ShowTaskbarWhenExit)] as bool?
                        ?? true,
                    LargeScreenThreshold =
                        ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.LargeScreenThreshold)] as double?
                        ?? 20,
                    DisableLargeScreenOverride =
                        ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.DisableLargeScreenOverride)] as bool?
                        ?? false
                };
                _isPackaged = true;
            }
            catch
            {
                _isPackaged = false;
                _userConfiguration = new UserConfiguration
                {
                    AutoModeType = AutoModeType.Auto,
                    ShowTaskbarWhenExit = true,
                    LargeScreenThreshold = 20,
                    DisableLargeScreenOverride = false
                };
            }
        }

        public static AutoModeType AutoModeType
        {
            get => _userConfiguration.AutoModeType;
            set
            {
                if (value == _userConfiguration.AutoModeType)
                    return;

                _userConfiguration.AutoModeType = value;
                if (_isPackaged)
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.AutoModeType)] = value.ToString();
            }
        }

        public static bool ShowTaskbarWhenExit
        {
            get => _userConfiguration.ShowTaskbarWhenExit;
            set
            {
                if (value == _userConfiguration.ShowTaskbarWhenExit)
                    return;

                _userConfiguration.ShowTaskbarWhenExit = value;
                if (_isPackaged)
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.ShowTaskbarWhenExit)] = value;
            }
        }

        public static double LargeScreenThreshold
        {
            get => _userConfiguration.LargeScreenThreshold;
            set
            {
                if (value == _userConfiguration.LargeScreenThreshold)
                    return;

                _userConfiguration.LargeScreenThreshold = value;
                if (_isPackaged)
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.LargeScreenThreshold)] = value;
            }
        }

        public static bool DisableLargeScreenOverride
        {
            get => _userConfiguration.DisableLargeScreenOverride;
            set
            {
                if (value == _userConfiguration.DisableLargeScreenOverride)
                    return;

                _userConfiguration.DisableLargeScreenOverride = value;
                if (_isPackaged)
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.DisableLargeScreenOverride)] = value;
            }
        }
    }
}
