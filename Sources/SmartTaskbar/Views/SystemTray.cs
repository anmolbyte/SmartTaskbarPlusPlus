using System.ComponentModel;
using System.Diagnostics;
using Windows.System;
using Windows.UI.ViewManagement;
using SmartTaskbar.Languages;
using SmartTaskbar.Helpers;
using SmartTaskbar.Models;
using SmartTaskbar.Views;

namespace SmartTaskbar
{
    internal class SystemTray : ApplicationContext
    {
        private const int TrayTolerance = 4;
        private readonly ToolStripMenuItem _animationInBar;
        private readonly ToolStripMenuItem _autoMode;

        private readonly Container _container = new();
        private readonly ToolStripMenuItem _settingsItem;
        private SettingsForm _settingsForm;
        private readonly ContextMenuStrip _contextMenuStrip;

        private readonly Engine _engine;
        private readonly ToolStripMenuItem _exit;
        private readonly NotifyIcon _notifyIcon;
        private readonly ResourceCulture _resourceCulture = new();
        private readonly ToolStripMenuItem _showBarOnExit;
        private readonly ToolStripMenuItem _hideTaskbarWhenFullscreen;
        private readonly ToolStripMenuItem _largeScreen;
        private readonly ToolStripMenuItem _dodgeStandard;
        private readonly ToolStripMenuItem _threshold20;
        private readonly ToolStripMenuItem _threshold27;
        private readonly ToolStripMenuItem _detectionMode;
        
        private readonly ToolStripMenuItem _clickAction;
        private readonly ToolStripMenuItem _doubleClickAction;

        private System.Windows.Forms.Timer _updateTimer;

        private readonly ToolStripMenuItem _screenEffects;
        private readonly ToolStripMenuItem _toggleInversion;
        private readonly ToolStripMenuItem _effectNegative;
        private readonly ToolStripMenuItem _effectGrayScale;
        private readonly ToolStripMenuItem _effectSepia;
        private readonly ToolStripMenuItem _effectRed;
        private readonly ToolStripMenuItem _effectHueShift;
        private readonly ToolStripMenuItem _effectNegativeGrayScale;
        private readonly ToolStripMenuItem _effectNegativeSepia;
        private readonly ToolStripMenuItem _effectNegativeRed;
        private readonly ToolStripMenuItem _effectSmartVariation1;
        private readonly ToolStripMenuItem _effectSmartVariation2;
        private readonly ToolStripMenuItem _effectSmartVariation3;
        private readonly ToolStripMenuItem _effectSmartVariation4;

        public SystemTray()
        {
            #region Initialization

            _engine = new Engine(_container);

            var font = new Font("Segoe UI", 10.5F);

            var about = new ToolStripMenuItem(_resourceCulture.GetString(LangName.About))
            {
                Font = font
            };
            _animationInBar = new ToolStripMenuItem(_resourceCulture.GetString(LangName.Animation))
            {
                Font = font
            };
            _showBarOnExit = new ToolStripMenuItem(_resourceCulture.GetString(LangName.ShowBarOnExit))
            {
                Font = font
            };
            _hideTaskbarWhenFullscreen = new ToolStripMenuItem("Hide taskbar when fullscreen") // Using string directly to avoid adding to LangName/Resource for now
            {
                Font = font
            };
            _autoMode = new ToolStripMenuItem(_resourceCulture.GetString(LangName.Auto))
            {
                Font = font
            };
            _exit = new ToolStripMenuItem(_resourceCulture.GetString(LangName.Exit))
            {
                Font = font
            };

            _dodgeStandard = new ToolStripMenuItem(_resourceCulture.GetString(LangName.DodgeStandard))
            {
                Font = font
            };
            _threshold20 = new ToolStripMenuItem(_resourceCulture.GetString(LangName.Threshold20))
            {
                Font = font
            };
            _threshold27 = new ToolStripMenuItem(_resourceCulture.GetString(LangName.Threshold27))
            {
                Font = font
            };

            _largeScreen = new ToolStripMenuItem(_resourceCulture.GetString(LangName.LargeScreen))
            {
                Font = font
            };

            _detectionMode = new ToolStripMenuItem(_resourceCulture.GetString(LangName.DetectionMode))
            {
                Font = font
            };
            InitializeDetectionMenu(_detectionMode);
            _largeScreen.DropDownItems.AddRange(new ToolStripItem[]
            {
                _dodgeStandard,
                _threshold20,
                _threshold27,
                new ToolStripSeparator(),
                _detectionMode
            });

            _toggleInversion = new ToolStripMenuItem(_resourceCulture.GetString(LangName.ToggleInversion))
            {
                Font = font
            };
            _effectNegative = new ToolStripMenuItem(_resourceCulture.GetString(LangName.EffectNegative))
            {
                Font = font,
                Tag = "Negative"
            };
            _effectGrayScale = new ToolStripMenuItem(_resourceCulture.GetString(LangName.EffectGrayScale))
            {
                Font = font,
                Tag = "GrayScale"
            };
            _effectSepia = new ToolStripMenuItem(_resourceCulture.GetString(LangName.EffectSepia))
            {
                Font = font,
                Tag = "Sepia"
            };
            _effectRed = new ToolStripMenuItem(_resourceCulture.GetString(LangName.EffectRed))
            {
                Font = font,
                Tag = "Red"
            };
            _effectHueShift = new ToolStripMenuItem(_resourceCulture.GetString(LangName.EffectHueShift))
            {
                Font = font,
                Tag = "NegativeHueShift180"
            };
            _effectNegativeGrayScale = new ToolStripMenuItem(_resourceCulture.GetString(LangName.EffectNegativeGrayScale))
            {
                Font = font,
                Tag = "NegativeGrayScale"
            };
            _effectNegativeSepia = new ToolStripMenuItem(_resourceCulture.GetString(LangName.EffectNegativeSepia))
            {
                Font = font,
                Tag = "NegativeSepia"
            };
            _effectNegativeRed = new ToolStripMenuItem(_resourceCulture.GetString(LangName.EffectNegativeRed))
            {
                Font = font,
                Tag = "NegativeRed"
            };
            _effectSmartVariation1 = new ToolStripMenuItem(_resourceCulture.GetString(LangName.EffectSmartVariation1))
            {
                Font = font,
                Tag = "NegativeHueShift180Variation1"
            };
            _effectSmartVariation2 = new ToolStripMenuItem(_resourceCulture.GetString(LangName.EffectSmartVariation2))
            {
                Font = font,
                Tag = "NegativeHueShift180Variation2"
            };
            _effectSmartVariation3 = new ToolStripMenuItem(_resourceCulture.GetString(LangName.EffectSmartVariation3))
            {
                Font = font,
                Tag = "NegativeHueShift180Variation3"
            };
            _effectSmartVariation4 = new ToolStripMenuItem(_resourceCulture.GetString(LangName.EffectSmartVariation4))
            {
                Font = font,
                Tag = "NegativeHueShift180Variation4"
            };

            _clickAction = new ToolStripMenuItem(_resourceCulture.GetString(LangName.ClickAction))
            {
                Font = font
            };
            _doubleClickAction = new ToolStripMenuItem(_resourceCulture.GetString(LangName.DoubleClickAction))
            {
                Font = font
            };

            InitializeActionMenu(_clickAction, true);
            InitializeActionMenu(_doubleClickAction, false);


            _settingsItem = new ToolStripMenuItem("Settings...") // TODO: Localize
            {
                Font = new Font(font, FontStyle.Bold)
            };
            _settingsItem.Click += (s, e) => ShowSettings();

            _screenEffects = new ToolStripMenuItem(_resourceCulture.GetString(LangName.ScreenEffects))
            {
                Font = font
            };
            _screenEffects.DropDownItems.AddRange(new ToolStripItem[]
            {
                _effectNegative,
                _effectGrayScale,
                _effectSepia,
                _effectRed,
                new ToolStripSeparator(),
                _effectNegativeGrayScale,
                _effectNegativeSepia,
                _effectNegativeRed,
                new ToolStripSeparator(),
                _effectHueShift,
                _effectSmartVariation1,
                _effectSmartVariation2,
                _effectSmartVariation3,
                _effectSmartVariation4
            });

            _contextMenuStrip = new ContextMenuStrip(_container)
            {
                Renderer = new Win11Renderer()
            };

            _contextMenuStrip.Items.AddRange(new ToolStripItem[]
            {
                _settingsItem,
                new ToolStripSeparator(),
                _autoMode,
                _toggleInversion,
                new ToolStripSeparator(),
                _hideTaskbarWhenFullscreen,
                new ToolStripSeparator(),
                about,
                _exit
            });

            _notifyIcon = new NotifyIcon(_container)
            {
                Text = Application.ProductName,
                Icon = Fun.IsLightTheme() ? IconResource.Logo_Black : IconResource.Logo_White,
                Visible = true
            };

            #endregion

            #region Load Event

            about.Click += AboutOnClick;

            _animationInBar.Click += AnimationInBarOnClick;

            _showBarOnExit.Click += ShowBarOnExitOnClick;
            _hideTaskbarWhenFullscreen.Click += HideTaskbarWhenFullscreenOnClick;

            _autoMode.Click += AutoModeOnClick;

            _exit.Click += ExitOnClick;

            _dodgeStandard.Click += DodgeStandardOnClick;
            _threshold20.Click += Threshold20OnClick;
            _threshold27.Click += Threshold27OnClick;

            _toggleInversion.Click += (s, e) => UserSettings.IsNegativeModeEnabled = !UserSettings.IsNegativeModeEnabled;
            _effectNegative.Click += EffectOnClick;
            _effectGrayScale.Click += EffectOnClick;
            _effectSepia.Click += EffectOnClick;
            _effectRed.Click += EffectOnClick;
            _effectHueShift.Click += EffectOnClick;
            _effectNegativeGrayScale.Click += EffectOnClick;
            _effectNegativeSepia.Click += EffectOnClick;
            _effectNegativeRed.Click += EffectOnClick;
            _effectSmartVariation1.Click += EffectOnClick;
            _effectSmartVariation2.Click += EffectOnClick;
            _effectSmartVariation3.Click += EffectOnClick;
            _effectSmartVariation4.Click += EffectOnClick;

            _notifyIcon.MouseClick += NotifyIconOnMouseClick;
            _notifyIcon.MouseDoubleClick += NotifyIconOnMouseDoubleClick;

            Fun.UiSettings.ColorValuesChanged += UISettingsOnColorValuesChanged;

            Application.ApplicationExit += Application_ApplicationExit;

            InitializeUpdateTimer();
            CheckForUpdatesAtStartup();

            #endregion
        }

        private void InitializeUpdateTimer()
        {
            _updateTimer = new System.Windows.Forms.Timer { Interval = 600000 }; // Check internal state every 10 mins
            _updateTimer.Tick += async (s, e) => {
                if (UpdateHelper.ShouldCheckNow()) {
                    await UpdateHelper.CheckForUpdatesAsync();
                }
            };
            _updateTimer.Start();
        }

        private async void CheckForUpdatesAtStartup()
        {
            await Task.Delay(5000); // Wait for app to settle
            if (UpdateHelper.ShouldCheckNow()) {
                await UpdateHelper.CheckForUpdatesAsync();
            }
        }

        private void AboutOnClick(object? sender, EventArgs e)
            => _ = Launcher.LaunchUriAsync(new Uri("https://github.com/ChanpleCai/SmartTaskbar"));

        private void UISettingsOnColorValuesChanged(UISettings s, object e)
            => _notifyIcon.Icon = Fun.IsLightTheme() ? IconResource.Logo_Black : IconResource.Logo_White;


        private System.Windows.Forms.Timer _clickTimer;

        private void NotifyIconOnMouseDoubleClick(object? s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _clickTimer?.Stop();
            ExecuteAction(UserSettings.DoubleClickAction);
        }

        private void NotifyIconOnMouseClick(object? s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // If no double-click action is set, fire immediately for maximum snappiness
                if (UserSettings.DoubleClickAction == TrayClickAction.None)
                {
                    ExecuteAction(UserSettings.ClickAction);
                    return;
                }

                if (_clickTimer == null)
                {
                    _clickTimer = new System.Windows.Forms.Timer();
                    _clickTimer.Tick += (sender, args) =>
                    {
                        _clickTimer.Stop();
                        ExecuteAction(UserSettings.ClickAction);
                    };
                }
                
                _clickTimer.Stop();
                _clickTimer.Interval = UserSettings.ClickDelay;
                _clickTimer.Start();
                return;
            }

            if (e.Button != MouseButtons.Right) return;

            _animationInBar.Checked = Fun.IsEnableTaskbarAnimation();
            _showBarOnExit.Checked = UserSettings.ShowTaskbarWhenExit;
            _hideTaskbarWhenFullscreen.Checked = UserSettings.HideTaskbarWhenFullscreen;
            _autoMode.Checked = UserSettings.AutoModeType == AutoModeType.Auto;

            _dodgeStandard.Checked = UserSettings.DisableLargeScreenOverride;
            _threshold20.Checked = !UserSettings.DisableLargeScreenOverride && UserSettings.LargeScreenThreshold == 20;
            _threshold27.Checked = !UserSettings.DisableLargeScreenOverride && UserSettings.LargeScreenThreshold == 27;

            UpdateDetectionMenuCheck(_detectionMode, UserSettings.LargeScreenDetectionMode);

            _toggleInversion.Checked = UserSettings.IsNegativeModeEnabled;
            UpdateActionMenuCheck(_clickAction, UserSettings.ClickAction);
            UpdateActionMenuCheck(_doubleClickAction, UserSettings.DoubleClickAction);
            

            _effectNegative.Checked = UserSettings.ActiveColorEffect == "Negative";
            _effectGrayScale.Checked = UserSettings.ActiveColorEffect == "GrayScale";
            _effectSepia.Checked = UserSettings.ActiveColorEffect == "Sepia";
            _effectRed.Checked = UserSettings.ActiveColorEffect == "Red";
            _effectHueShift.Checked = UserSettings.ActiveColorEffect == "NegativeHueShift180";
            _effectNegativeGrayScale.Checked = UserSettings.ActiveColorEffect == "NegativeGrayScale";
            _effectNegativeSepia.Checked = UserSettings.ActiveColorEffect == "NegativeSepia";
            _effectNegativeRed.Checked = UserSettings.ActiveColorEffect == "NegativeRed";
            _effectSmartVariation1.Checked = UserSettings.ActiveColorEffect == "NegativeHueShift180Variation1";
            _effectSmartVariation2.Checked = UserSettings.ActiveColorEffect == "NegativeHueShift180Variation2";
            _effectSmartVariation3.Checked = UserSettings.ActiveColorEffect == "NegativeHueShift180Variation3";
            _effectSmartVariation4.Checked = UserSettings.ActiveColorEffect == "NegativeHueShift180Variation4";

            ShowMenu();

            Fun.SetForegroundWindow(_contextMenuStrip.Handle);
        }

        private void ShowMenu()
        {
            var taskbar = TaskbarHelper.InitTaskbar();

            if (taskbar.Handle == IntPtr.Zero)
                return;

            switch (taskbar.Position)
            {
                case TaskbarPosition.Bottom:
                    if (Cursor.Position.X + _contextMenuStrip.Width > Screen.PrimaryScreen.Bounds.Right)
                        _contextMenuStrip.Show(
                            Screen.PrimaryScreen.Bounds.Right - _contextMenuStrip.Width - TrayTolerance,
                            taskbar.Rect.top - _contextMenuStrip.Height - TrayTolerance);
                    else
                        _contextMenuStrip.Show(Cursor.Position.X - TrayTolerance,
                                               taskbar.Rect.top - _contextMenuStrip.Height - TrayTolerance);
                    break;
                case TaskbarPosition.Left:
                    if (Cursor.Position.Y + _contextMenuStrip.Height > Screen.PrimaryScreen.Bounds.Bottom)
                        _contextMenuStrip.Show(taskbar.Rect.right + TrayTolerance,
                                               Screen.PrimaryScreen.Bounds.Bottom
                                               - _contextMenuStrip.Height
                                               - TrayTolerance);
                    else
                        _contextMenuStrip.Show(taskbar.Rect.right + TrayTolerance,
                                               Cursor.Position.Y - TrayTolerance);
                    break;
                case TaskbarPosition.Right:
                    if (Cursor.Position.Y + _contextMenuStrip.Height > Screen.PrimaryScreen.Bounds.Bottom)
                        _contextMenuStrip.Show(taskbar.Rect.left - TrayTolerance - _contextMenuStrip.Width,
                                               Screen.PrimaryScreen.Bounds.Bottom
                                               - _contextMenuStrip.Height
                                               - TrayTolerance);
                    else
                        _contextMenuStrip.Show(taskbar.Rect.left - TrayTolerance - _contextMenuStrip.Width,
                                               Cursor.Position.Y - TrayTolerance);
                    break;
                case TaskbarPosition.Top:
                    if (Cursor.Position.X + _contextMenuStrip.Width > Screen.PrimaryScreen.Bounds.Right)
                        _contextMenuStrip.Show(
                            Screen.PrimaryScreen.Bounds.Right - _contextMenuStrip.Width - TrayTolerance,
                            taskbar.Rect.bottom + TrayTolerance);
                    else
                        _contextMenuStrip.Show(Cursor.Position.X - TrayTolerance,
                                               taskbar.Rect.bottom + TrayTolerance);
                    break;
            }
        }

        private static void HideBar()
        {
            if (Fun.IsNotAutoHide())
                return;

            var taskbar = TaskbarHelper.InitTaskbar();

            if (taskbar.Handle != IntPtr.Zero)
                taskbar.HideTaskbar();
        }
        private void ShowSettings()
        {
            if (_settingsForm == null || _settingsForm.IsDisposed)
            {
                _settingsForm = new SettingsForm();
            }
            _settingsForm.Show();
            _settingsForm.Activate();
        }

        private void ExitOnClick(object? s, EventArgs e)
        {
            if (UserSettings.ShowTaskbarWhenExit)
                Fun.CancelAutoHide();
            else
                HideBar();
            
            MagnificationManager.RestoreDefault();
            MagnificationManager.Uninitialize();

            _container?.Dispose();
            Application.Exit();
        }

        private void ShowBarOnExitOnClick(object? s, EventArgs e)
            => UserSettings.ShowTaskbarWhenExit = !_showBarOnExit.Checked;

        private void HideTaskbarWhenFullscreenOnClick(object? s, EventArgs e)
            => UserSettings.HideTaskbarWhenFullscreen = !_hideTaskbarWhenFullscreen.Checked;

        private void AutoModeOnClick(object? s, EventArgs e)
        {
            if (_autoMode.Checked)
            {
                UserSettings.AutoModeType = AutoModeType.None;
                HideBar();
            }
            else { UserSettings.AutoModeType = AutoModeType.Auto; }
        }

        private void DodgeStandardOnClick(object? s, EventArgs e)
        {
            UserSettings.DisableLargeScreenOverride = true;
        }

        private void Threshold20OnClick(object? s, EventArgs e)
        {
            UserSettings.DisableLargeScreenOverride = false;
            UserSettings.LargeScreenThreshold = 20;
        }

        private void Threshold27OnClick(object? s, EventArgs e)
        {
            UserSettings.DisableLargeScreenOverride = false;
            UserSettings.LargeScreenThreshold = 27;
        }

        private void AnimationInBarOnClick(object? s, EventArgs e)
            => _animationInBar.Checked = Fun.ChangeTaskbarAnimation();

        private void EffectOnClick(object? s, EventArgs e)
        {
            if (s is ToolStripMenuItem item && item.Tag is string effectName)
            {
                UserSettings.ActiveColorEffect = effectName;
                UserSettings.IsNegativeModeEnabled = true;
            }
        }



        private void ExecuteAction(TrayClickAction action)
        {
            switch (action)
            {
                case TrayClickAction.ToggleInversion:
                    UserSettings.IsNegativeModeEnabled = !UserSettings.IsNegativeModeEnabled;
                    _toggleInversion.Checked = UserSettings.IsNegativeModeEnabled;
                    break;
                case TrayClickAction.ToggleAutoMode:
                    UserSettings.AutoModeType = UserSettings.AutoModeType == AutoModeType.Auto ? AutoModeType.None : AutoModeType.Auto;
                    _autoMode.Checked = UserSettings.AutoModeType == AutoModeType.Auto;
                    if (UserSettings.AutoModeType == AutoModeType.None)
                    {
                        var taskbar = TaskbarHelper.InitTaskbar();
                        if (taskbar.Handle != IntPtr.Zero)
                            taskbar.ShowTaskar();
                    }
                    break;
                case TrayClickAction.SetShowMode:
                    UserSettings.AutoModeType = AutoModeType.None;
                    _autoMode.Checked = false;
                    var taskbarShow = TaskbarHelper.InitTaskbar();
                    if (taskbarShow.Handle != IntPtr.Zero)
                        taskbarShow.ShowTaskar();
                    break;
                case TrayClickAction.SetHideMode:
                    UserSettings.AutoModeType = AutoModeType.None;
                    _autoMode.Checked = false;
                    var taskbarHide = TaskbarHelper.InitTaskbar();
                    if (taskbarHide.Handle != IntPtr.Zero)
                        taskbarHide.HideTaskbar();
                    break;
            }
        }

        private void InitializeActionMenu(ToolStripMenuItem menu, bool isSingleClick)
        {
            var actions = new[]
            {
                TrayClickAction.ToggleInversion,
                TrayClickAction.ToggleAutoMode,
                TrayClickAction.SetShowMode,
                TrayClickAction.SetHideMode,
                TrayClickAction.None
            };

            foreach (var action in actions)
            {
                var name = action switch
                {
                    TrayClickAction.ToggleInversion => LangName.ActionToggleInversion,
                    TrayClickAction.ToggleAutoMode => LangName.ActionToggleAutoMode,
                    TrayClickAction.SetShowMode => LangName.ActionSetShowMode,
                    TrayClickAction.SetHideMode => LangName.ActionSetHideMode,
                    _ => LangName.ActionNone
                };

                var item = new ToolStripMenuItem(_resourceCulture.GetString(name))
                {
                    Tag = action,
                    Font = menu.Font
                };

                item.Click += (s, e) =>
                {
                    if (isSingleClick)
                        UserSettings.ClickAction = (TrayClickAction)item.Tag;
                    else
                        UserSettings.DoubleClickAction = (TrayClickAction)item.Tag;

                    UpdateActionMenuCheck(menu, (TrayClickAction)item.Tag);
                };

                menu.DropDownItems.Add(item);
            }
        }

        private void UpdateActionMenuCheck(ToolStripMenuItem menu, TrayClickAction currentAction)
        {
            foreach (ToolStripMenuItem item in menu.DropDownItems)
            {
                item.Checked = (TrayClickAction)item.Tag == currentAction;
            }
        }


        private void InitializeDetectionMenu(ToolStripMenuItem menu)
        {
            var modes = new[]
            {
                LargeScreenDetectionMode.PrimaryOnly,
                LargeScreenDetectionMode.AnyMonitor
            };

            foreach (var mode in modes)
            {
                var name = mode == LargeScreenDetectionMode.PrimaryOnly ? LangName.DetectionPrimaryOnly : LangName.DetectionAnyMonitor;
                var item = new ToolStripMenuItem(_resourceCulture.GetString(name))
                {
                    Tag = mode,
                    Font = menu.Font
                };

                item.Click += (s, e) =>
                {
                    UserSettings.LargeScreenDetectionMode = (LargeScreenDetectionMode)item.Tag;
                    UpdateDetectionMenuCheck(menu, (LargeScreenDetectionMode)item.Tag);
                };

                menu.DropDownItems.Add(item);
            }
        }

        private void UpdateDetectionMenuCheck(ToolStripMenuItem menu, LargeScreenDetectionMode currentMode)
        {
            foreach (ToolStripMenuItem item in menu.DropDownItems)
            {
                item.Checked = (LargeScreenDetectionMode)item.Tag == currentMode;
            }
        }

        private static async void Application_ApplicationExit(object? sender, EventArgs e)
        {
            // Weird bug.
            await Task.Delay(500);
            Process.GetCurrentProcess().Kill();
        }
    }
}
