using Windows.Storage;
using SmartTaskbar.Helpers;
using SmartTaskbar.Models;
using System.Text.Json;
using System.IO;

namespace SmartTaskbar
{
    public class UserSettings
    {
        private static UserConfiguration _userConfiguration;
        private static readonly bool _isPackaged;

        public static event EventHandler<string> SettingChanged;

        private static void OnSettingChanged(string propertyName) => SettingChanged?.Invoke(null, propertyName);

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
                        ?? false,
                    StartOnLogin = ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.StartOnLogin)] as bool? ?? false,
                    IsNegativeModeEnabled =
                        ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.IsNegativeModeEnabled)] as bool?
                        ?? false,
                    ActiveColorEffect =
                        ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.ActiveColorEffect)] as string
                        ?? "Negative",
                    ClickAction = (TrayClickAction)(ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.ClickAction)] as int? ?? (int)TrayClickAction.ToggleInversion),
                    DoubleClickAction = (TrayClickAction)(ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.DoubleClickAction)] as int? ?? (int)TrayClickAction.ToggleAutoMode),
                    LargeScreenDetectionMode = (LargeScreenDetectionMode)(ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.LargeScreenDetectionMode)] as int? ?? (int)LargeScreenDetectionMode.PrimaryOnly),
                    ClickDelay = ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.ClickDelay)] as int? ?? SystemInformation.DoubleClickTime,
                    CheckForUpdates = ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.CheckForUpdates)] as bool? ?? true,
                    UpdateFrequency = (UpdateFrequency)(ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.UpdateFrequency)] as int? ?? (int)UpdateFrequency.Day),
                    LastUpdateCheck = DateTime.Parse(ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.LastUpdateCheck)] as string ?? DateTime.MinValue.ToString())
                };
                _isPackaged = true;
            }
            catch
            {
                _isPackaged = false;
                _userConfiguration = LoadFromFile();
            }
            
            ApplyEffect();
        }

        private static string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        private static UserConfiguration LoadFromFile()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var options = new JsonSerializerOptions();
                    options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                    var config = JsonSerializer.Deserialize<UserConfiguration>(json, options);
                    if (config.ClickDelay == 0) config.ClickDelay = SystemInformation.DoubleClickTime;
                    return config;
                }
            }
            catch { }

            return new UserConfiguration
            {
                AutoModeType = AutoModeType.Auto,
                ShowTaskbarWhenExit = true,
                StartOnLogin = false,
                LargeScreenThreshold = 20,
                DisableLargeScreenOverride = false,
                IsNegativeModeEnabled = false,
                ActiveColorEffect = "Negative",
                ClickAction = TrayClickAction.ToggleInversion,
                DoubleClickAction = TrayClickAction.ToggleAutoMode,
                LargeScreenDetectionMode = LargeScreenDetectionMode.PrimaryOnly,
                ClickDelay = SystemInformation.DoubleClickTime,
                CheckForUpdates = true,
                UpdateFrequency = UpdateFrequency.Day,
                LastUpdateCheck = DateTime.MinValue
            };
        }

        private static void SaveToFile()
        {
            if (_isPackaged) return;
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                var json = JsonSerializer.Serialize(_userConfiguration, options);
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
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
                else
                    SaveToFile();
                OnSettingChanged(nameof(AutoModeType));
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
                else
                    SaveToFile();
                OnSettingChanged(nameof(ShowTaskbarWhenExit));
            }
        }

        public static bool StartOnLogin
        {
            get => _userConfiguration.StartOnLogin;
            set
            {
                if (value == _userConfiguration.StartOnLogin)
                    return;

                _userConfiguration.StartOnLogin = value;
                if (_isPackaged)
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.StartOnLogin)] = value;
                else
                    SaveToFile();
                
                StartupHelper.SetStartup(value);
                OnSettingChanged(nameof(StartOnLogin));
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
                else
                    SaveToFile();
                OnSettingChanged(nameof(LargeScreenThreshold));
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
                else
                    SaveToFile();
                OnSettingChanged(nameof(DisableLargeScreenOverride));
            }
        }

        public static bool IsNegativeModeEnabled
        {
            get => _userConfiguration.IsNegativeModeEnabled;
            set
            {
                _userConfiguration.IsNegativeModeEnabled = value;
                if (_isPackaged)
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.IsNegativeModeEnabled)] = value;
                else
                    SaveToFile();
                
                ApplyEffect();
                OnSettingChanged(nameof(IsNegativeModeEnabled));
            }
        }

        public static string ActiveColorEffect
        {
            get => _userConfiguration.ActiveColorEffect;
            set
            {
                _userConfiguration.ActiveColorEffect = value;
                if (_isPackaged)
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.ActiveColorEffect)] = value;
                else
                    SaveToFile();
                
                if (_userConfiguration.IsNegativeModeEnabled)
                {
                    ApplyEffect();
                }
                OnSettingChanged(nameof(ActiveColorEffect));
            }
        }

        public static TrayClickAction ClickAction
        {
            get => _userConfiguration.ClickAction;
            set
            {
                _userConfiguration.ClickAction = value;
                if (_isPackaged)
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.ClickAction)] = (int)value;
                else
                    SaveToFile();
                OnSettingChanged(nameof(ClickAction));
            }
        }

        public static TrayClickAction DoubleClickAction
        {
            get => _userConfiguration.DoubleClickAction;
            set
            {
                _userConfiguration.DoubleClickAction = value;
                if (_isPackaged)
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.DoubleClickAction)] = (int)value;
                else
                    SaveToFile();
                OnSettingChanged(nameof(DoubleClickAction));
            }
        }

        public static LargeScreenDetectionMode LargeScreenDetectionMode
        {
            get => _userConfiguration.LargeScreenDetectionMode;
            set
            {
                _userConfiguration.LargeScreenDetectionMode = value;
                if (_isPackaged)
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.LargeScreenDetectionMode)] = (int)value;
                else
                    SaveToFile();
                OnSettingChanged(nameof(LargeScreenDetectionMode));
            }
        }

        public static int ClickDelay
        {
            get => _userConfiguration.ClickDelay;
            set
            {
                if (value == _userConfiguration.ClickDelay)
                    return;

                _userConfiguration.ClickDelay = value;
                if (_isPackaged)
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.ClickDelay)] = value;
                else
                    SaveToFile();
                OnSettingChanged(nameof(ClickDelay));
            }
        }



        public static bool CheckForUpdates
        {
            get => _userConfiguration.CheckForUpdates;
            set
            {
                _userConfiguration.CheckForUpdates = value;
                if (_isPackaged)
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.CheckForUpdates)] = value;
                else
                    SaveToFile();
                OnSettingChanged(nameof(CheckForUpdates));
            }
        }

        public static UpdateFrequency UpdateFrequency
        {
            get => _userConfiguration.UpdateFrequency;
            set
            {
                _userConfiguration.UpdateFrequency = value;
                if (_isPackaged)
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.UpdateFrequency)] = (int)value;
                else
                    SaveToFile();
                OnSettingChanged(nameof(UpdateFrequency));
            }
        }

        public static DateTime LastUpdateCheck
        {
            get => _userConfiguration.LastUpdateCheck;
            set
            {
                _userConfiguration.LastUpdateCheck = value;
                if (_isPackaged)
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.LastUpdateCheck)] = value.ToString();
                else
                    SaveToFile();
                OnSettingChanged(nameof(LastUpdateCheck));
            }
        }

        private static void ApplyEffect()
        {
            if (_userConfiguration.IsNegativeModeEnabled)
            {
                MagnificationManager.SetColorEffect(BuiltinMatrices.GetMatrixByName(_userConfiguration.ActiveColorEffect));
            }
            else
            {
                MagnificationManager.RestoreDefault();
            }
        }
    }
}
