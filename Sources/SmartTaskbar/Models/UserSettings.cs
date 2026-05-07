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
                    IsNegativeModeEnabled =
                        ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.IsNegativeModeEnabled)] as bool?
                        ?? false,
                    ActiveColorEffect =
                        ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.ActiveColorEffect)] as string
                        ?? "Negative",
                    ClickAction = (TrayClickAction)(ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.ClickAction)] as int? ?? (int)TrayClickAction.ToggleInversion),
                    DoubleClickAction = (TrayClickAction)(ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.DoubleClickAction)] as int? ?? (int)TrayClickAction.ToggleAutoMode),
                    LargeScreenDetectionMode = (LargeScreenDetectionMode)(ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.LargeScreenDetectionMode)] as int? ?? (int)LargeScreenDetectionMode.PrimaryOnly),
                    MonitorConfigs = new List<MonitorConfig>(),
                    HotkeyConfigs = new List<HotkeyConfig>()
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
                    if (config.MonitorConfigs == null) config.MonitorConfigs = new List<MonitorConfig>();
                    if (config.HotkeyConfigs == null) config.HotkeyConfigs = new List<HotkeyConfig>();
                    return config;
                }
            }
            catch { }

            return new UserConfiguration
            {
                AutoModeType = AutoModeType.Auto,
                ShowTaskbarWhenExit = true,
                LargeScreenThreshold = 20,
                DisableLargeScreenOverride = false,
                IsNegativeModeEnabled = false,
                ActiveColorEffect = "Negative",
                ClickAction = TrayClickAction.ToggleInversion,
                DoubleClickAction = TrayClickAction.ToggleAutoMode,
                LargeScreenDetectionMode = LargeScreenDetectionMode.PrimaryOnly,
                MonitorConfigs = new List<MonitorConfig>(),
                HotkeyConfigs = new List<HotkeyConfig>
                {
                    new HotkeyConfig { Name = "Brightness Up", Modifiers = Fun.MOD_CONTROL | Fun.MOD_ALT, Key = 0x26, Action = HotkeyAction.BrightnessUp, TargetMonitor = "All", Value = 10 },
                    new HotkeyConfig { Name = "Brightness Down", Modifiers = Fun.MOD_CONTROL | Fun.MOD_ALT, Key = 0x28, Action = HotkeyAction.BrightnessDown, TargetMonitor = "All", Value = 10 },
                    new HotkeyConfig { Name = "Toggle Inversion", Modifiers = Fun.MOD_CONTROL | Fun.MOD_ALT, Key = 0x49, Action = HotkeyAction.ToggleInversion }
                }
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
            }
        }

        public static List<MonitorConfig> MonitorConfigs
        {
            get => _userConfiguration.MonitorConfigs;
            set
            {
                _userConfiguration.MonitorConfigs = value;
                SaveToFile();
            }
        }

        public static List<HotkeyConfig> HotkeyConfigs
        {
            get => _userConfiguration.HotkeyConfigs;
            set
            {
                _userConfiguration.HotkeyConfigs = value;
                SaveToFile();
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
