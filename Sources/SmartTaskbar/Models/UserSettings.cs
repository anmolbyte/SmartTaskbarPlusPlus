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
                    HideTaskbarWhenFullscreen =
                        ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.HideTaskbarWhenFullscreen)] as bool?
                        ?? true,
                    LargeScreenThreshold =
                        ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.LargeScreenThreshold)] as double?
                        ?? 42,
                    DisableLargeScreenOverride =
                        ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.DisableLargeScreenOverride)] as bool?
                        ?? true,
                    StartOnLogin = ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.StartOnLogin)] as bool? ?? false,
                    IsNegativeModeEnabled = false,
                    ActiveColorEffect =
                        ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.ActiveColorEffect)] as string
                        ?? "Negative",
                    ClickAction = (TrayClickAction)(ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.ClickAction)] as int? ?? (int)TrayClickAction.ToggleInversion),
                    LargeScreenDetectionMode = (LargeScreenDetectionMode)(ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.LargeScreenDetectionMode)] as int? ?? (int)LargeScreenDetectionMode.PrimaryOnly),
                    CheckForUpdates = ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.CheckForUpdates)] as bool? ?? true,
                    UpdateFrequency = (UpdateFrequency)(ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.UpdateFrequency)] as int? ?? (int)UpdateFrequency.Day),
                    LastUpdateCheck = DateTime.Parse(ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.LastUpdateCheck)] as string ?? DateTime.MinValue.ToString()),
                    BrightnessShortcuts = ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.BrightnessShortcuts)] is string shortcutsJson
                        ? JsonSerializer.Deserialize<List<DisplayShortcutRule>>(shortcutsJson) ?? new List<DisplayShortcutRule>()
                        : new List<DisplayShortcutRule>(),
                    MonitorNicknames = ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.MonitorNicknames)] is string nicknamesJson
                        ? JsonSerializer.Deserialize<Dictionary<string, string>>(nicknamesJson) ?? new Dictionary<string, string>()
                        : new Dictionary<string, string>(),
                    KnownMonitors = ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.KnownMonitors)] is string monitorsJson
                        ? JsonSerializer.Deserialize<List<SavedMonitorInfo>>(monitorsJson) ?? new List<SavedMonitorInfo>()
                        : new List<SavedMonitorInfo>(),
                    UsePointsForBrightness = ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.UsePointsForBrightness)] as bool? ?? false
                };
                _isPackaged = true;
            }
            catch
            {
                _isPackaged = false;
                _userConfiguration = LoadFromFile();
            }
        }

        private static string SettingsPath 
        {
            get
            {
                var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartTaskbar");
                var appDataFile = Path.Combine(appDataPath, "settings.json");
                var baseDirFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                if (File.Exists(baseDirFile))
                {
                    try
                    {
                        using (var fs = File.Open(baseDirFile, FileMode.Open, FileAccess.ReadWrite))
                        {
                            return baseDirFile;
                        }
                    }
                    catch
                    {
                        if (!File.Exists(appDataFile))
                        {
                            try
                            {
                                if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);
                                File.Copy(baseDirFile, appDataFile, true);
                            }
                            catch { }
                        }
                    }
                }

                if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);
                return appDataFile;
            }
        }

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
                    if (string.IsNullOrEmpty(config.ActiveColorEffect)) config.ActiveColorEffect = "Negative";
                    config.IsNegativeModeEnabled = false;
                    config.BrightnessShortcuts ??= new List<DisplayShortcutRule>();
                    config.MonitorNicknames ??= new Dictionary<string, string>();
                    config.KnownMonitors ??= new List<SavedMonitorInfo>();
                    if (config.KnownMonitors.Count == 0 && config.MonitorNicknames.Count > 0)
                    {
                        foreach (var kvp in config.MonitorNicknames)
                        {
                            config.KnownMonitors.Add(new SavedMonitorInfo
                            {
                                HardwareId = kvp.Key,
                                Nickname = kvp.Value,
                                SystemModelName = string.IsNullOrWhiteSpace(kvp.Value) ? "Monitor" : kvp.Value
                            });
                        }
                    }
                    return config;
                }
            }
            catch { }

            return new UserConfiguration
            {
                AutoModeType = AutoModeType.Auto,
                ShowTaskbarWhenExit = true,
                HideTaskbarWhenFullscreen = true,
                StartOnLogin = false,
                LargeScreenThreshold = 42,
                DisableLargeScreenOverride = true,
                IsNegativeModeEnabled = false,
                ActiveColorEffect = "Negative",
                ClickAction = TrayClickAction.ToggleInversion,
                LargeScreenDetectionMode = LargeScreenDetectionMode.PrimaryOnly,
                CheckForUpdates = true,
                UpdateFrequency = UpdateFrequency.Day,
                LastUpdateCheck = DateTime.MinValue,
                BrightnessShortcuts = new List<DisplayShortcutRule>(),
                MonitorNicknames = new Dictionary<string, string>(),
                KnownMonitors = new List<SavedMonitorInfo>(),
                UsePointsForBrightness = false
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
                var tempPath = SettingsPath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, SettingsPath, true);
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

        public static bool HideTaskbarWhenFullscreen
        {
            get => _userConfiguration.HideTaskbarWhenFullscreen;
            set
            {
                if (value == _userConfiguration.HideTaskbarWhenFullscreen)
                    return;

                _userConfiguration.HideTaskbarWhenFullscreen = value;
                if (_isPackaged)
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.HideTaskbarWhenFullscreen)] = value;
                else
                    SaveToFile();
                OnSettingChanged(nameof(HideTaskbarWhenFullscreen));
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
                
                ReapplyEffect();
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
                    ReapplyEffect();
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

        public static List<DisplayShortcutRule> BrightnessShortcuts
        {
            get => _userConfiguration.BrightnessShortcuts ??= new List<DisplayShortcutRule>();
            set
            {
                _userConfiguration.BrightnessShortcuts = value;
                if (_isPackaged)
                {
                    try
                    {
                        ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.BrightnessShortcuts)] = JsonSerializer.Serialize(value);
                    }
                    catch { }
                }
                else
                    SaveToFile();
                OnSettingChanged(nameof(BrightnessShortcuts));
            }
        }

        public static Dictionary<string, string> MonitorNicknames
        {
            get => _userConfiguration.MonitorNicknames ??= new Dictionary<string, string>();
            set
            {
                _userConfiguration.MonitorNicknames = value;
                if (_isPackaged)
                {
                    try
                    {
                        ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.MonitorNicknames)] = JsonSerializer.Serialize(value);
                    }
                    catch { }
                }
                else
                    SaveToFile();
                OnSettingChanged(nameof(MonitorNicknames));
            }
        }

        public static List<SavedMonitorInfo> KnownMonitors
        {
            get => _userConfiguration.KnownMonitors ??= new List<SavedMonitorInfo>();
            set
            {
                _userConfiguration.KnownMonitors = value;
                var dict = new Dictionary<string, string>();
                foreach (var m in value)
                {
                    if (!string.IsNullOrWhiteSpace(m.Nickname))
                    {
                        dict[m.HardwareId] = m.Nickname;
                    }
                }
                _userConfiguration.MonitorNicknames = dict;

                if (_isPackaged)
                {
                    try
                    {
                        ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.KnownMonitors)] = JsonSerializer.Serialize(value);
                        ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.MonitorNicknames)] = JsonSerializer.Serialize(dict);
                    }
                    catch { }
                }
                else
                    SaveToFile();
                OnSettingChanged(nameof(KnownMonitors));
                OnSettingChanged(nameof(MonitorNicknames));
            }
        }

        public static bool UsePointsForBrightness
        {
            get => _userConfiguration.UsePointsForBrightness;
            set
            {
                if (_userConfiguration.UsePointsForBrightness == value) return;
                _userConfiguration.UsePointsForBrightness = value;
                if (_isPackaged)
                    ApplicationData.Current.LocalSettings.Values[nameof(UserConfiguration.UsePointsForBrightness)] = value;
                else
                    SaveToFile();
                OnSettingChanged(nameof(UsePointsForBrightness));
            }
        }

        public static string FormatBrightness(int value)
        {
            return UsePointsForBrightness ? $"{value} pts" : $"{value}%";
        }

        public static void ReapplyEffect()
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
