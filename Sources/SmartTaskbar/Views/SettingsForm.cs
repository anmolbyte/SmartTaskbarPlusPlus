using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SmartTaskbar.Models;
using SmartTaskbar.Helpers;
using System.Linq;
using SmartTaskbar.Worker;
using SmartTaskbar.Views.Controls;

namespace SmartTaskbar.Views
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();
            SetupUI();
            SubscribeToSettings();
            this.FormClosed += SettingsForm_FormClosed;
        }

        private EventHandler? _hotkeysReloadedHandler;

        private void SettingsForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            UserSettings.SettingChanged -= OnSettingChanged;
            UserSettings.SettingChanged -= OnUpdateSettingChanged;
            if (_hotkeysReloadedHandler != null)
            {
                HotkeyHookWindow.HotkeysReloaded -= _hotkeysReloadedHandler;
            }
        }

        private void InitializeComponent()
        {
            this.Text = "SmartTaskbar++ Settings";
            // Set a large enough default size that will scale with DPI
            this.Size = new Size(1000, 800);
            this.MinimumSize = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = SystemFonts.MessageBoxFont;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Padding = new Padding(5);
        }

        private void SetupUI()
        {
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(12, 4)
            };
            this.Controls.Add(_tabControl);

            _tabControl.TabPages.Add(CreateGeneralPage());
            _tabControl.TabPages.Add(CreateColorPage());
            _tabControl.TabPages.Add(CreateBrightnessPage());
            _tabControl.TabPages.Add(CreateUpdatesPage());
        }
        
        private TabControl _tabControl;

        private CheckBox _autoModeCheck;
        private CheckBox _inversionCheck;
        private CheckBox _disableLargeScreenCheck;
        private ComboBox _clickActionCombo;

        private CheckBox _startOnLoginCheck;
        private TrackBar _thresholdSlider;
        private Label _thresholdLabel;
        private ComboBox _detectionModeCombo;
        private ListBox _themeList;

        private void SubscribeToSettings()
        {
            UserSettings.SettingChanged += OnSettingChanged;
        }

        private void OnSettingChanged(object s, string propertyName)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            this.Invoke(new Action(() =>
            {
                switch (propertyName)
                {
                    case nameof(UserSettings.IsNegativeModeEnabled):
                        if (_inversionCheck != null) _inversionCheck.Checked = UserSettings.IsNegativeModeEnabled;
                        break;
                    case nameof(UserSettings.AutoModeType):
                        if (_autoModeCheck != null) _autoModeCheck.Checked = UserSettings.AutoModeType == AutoModeType.Auto;
                        break;
                    case nameof(UserSettings.DisableLargeScreenOverride):
                        if (_disableLargeScreenCheck != null) _disableLargeScreenCheck.Checked = UserSettings.DisableLargeScreenOverride;
                        break;
                    case nameof(UserSettings.StartOnLogin):
                        if (_startOnLoginCheck != null) _startOnLoginCheck.Checked = UserSettings.StartOnLogin;
                        break;
                    case nameof(UserSettings.LargeScreenThreshold):
                        if (_thresholdSlider != null)
                        {
                            var val = Math.Clamp((int)UserSettings.LargeScreenThreshold, _thresholdSlider.Minimum, _thresholdSlider.Maximum);
                            if (_thresholdSlider.Value != val) _thresholdSlider.Value = val;
                        }
                        if (_thresholdLabel != null) _thresholdLabel.Text = $"Threshold: {(int)UserSettings.LargeScreenThreshold}\"";
                        break;
                    case nameof(UserSettings.ClickAction):
                        if (_clickActionCombo != null && (TrayClickAction)_clickActionCombo.SelectedItem != UserSettings.ClickAction)
                            _clickActionCombo.SelectedItem = UserSettings.ClickAction;
                        break;
                    case nameof(UserSettings.LargeScreenDetectionMode):
                        if (_detectionModeCombo != null && _detectionModeCombo.SelectedIndex != (int)UserSettings.LargeScreenDetectionMode)
                            _detectionModeCombo.SelectedIndex = (int)UserSettings.LargeScreenDetectionMode;
                        break;
                    case nameof(UserSettings.ActiveColorEffect):
                        if (_themeList != null && (string)_themeList.SelectedItem != UserSettings.ActiveColorEffect)
                            _themeList.SelectedItem = UserSettings.ActiveColorEffect;
                        break;
                }
            }));
        }

        private TabPage CreateGeneralPage()
        {
            var page = new TabPage("General");
            var layout = CreateFlowLayout();
            page.Controls.Add(layout);

            layout.Controls.Add(CreateHeader("Application Behavior"));
            
            _autoModeCheck = CreateCheckBox("Enable Smart Auto-Hide", UserSettings.AutoModeType == AutoModeType.Auto);
            _autoModeCheck.CheckedChanged += (s, e) => UserSettings.AutoModeType = _autoModeCheck.Checked ? AutoModeType.Auto : AutoModeType.None;
            layout.Controls.Add(_autoModeCheck);

            _startOnLoginCheck = CreateCheckBox("Launch SmartTaskbar++ on Windows Startup", UserSettings.StartOnLogin);
            _startOnLoginCheck.CheckedChanged += (s, e) => UserSettings.StartOnLogin = _startOnLoginCheck.Checked;
            layout.Controls.Add(_startOnLoginCheck);

            _disableLargeScreenCheck = CreateCheckBox("Disable Large Screen Override", UserSettings.DisableLargeScreenOverride);
            _disableLargeScreenCheck.CheckedChanged += (s, e) => UserSettings.DisableLargeScreenOverride = _disableLargeScreenCheck.Checked;
            layout.Controls.Add(_disableLargeScreenCheck);
            layout.Controls.Add(new Label { Text = "Large screen detection forces the taskbar to stay visible on displays larger than the threshold (useful for presentations).", Font = new Font(this.Font.FontFamily, 9F), AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(0, 0, 0, 15) });

            layout.Controls.Add(CreateHeader("Icon Click Behavior"));
            
            _clickActionCombo = new ComboBox { Width = 350, DropDownStyle = ComboBoxStyle.DropDownList };
            
            foreach (var action in Enum.GetValues(typeof(TrayClickAction)))
            {
                _clickActionCombo.Items.Add(action);
            }
            
            _clickActionCombo.SelectedItem = UserSettings.ClickAction;
            
            _clickActionCombo.SelectedIndexChanged += (s, e) => UserSettings.ClickAction = (TrayClickAction)_clickActionCombo.SelectedItem;
            
            layout.Controls.Add(new Label { Text = "Single Click:", AutoSize = true });
            layout.Controls.Add(_clickActionCombo);

            layout.Controls.Add(CreateHeader("Large Screen Threshold"));
            
            _thresholdLabel = new Label { Text = $"Threshold: {UserSettings.LargeScreenThreshold}\"", AutoSize = true };
            var clampedVal = Math.Clamp((int)UserSettings.LargeScreenThreshold, 10, 85);
            _thresholdSlider = new TrackBar { Minimum = 10, Maximum = 85, Value = clampedVal, Width = 350 };
            
            var debounceTimer = new System.Windows.Forms.Timer { Interval = 250 };
            debounceTimer.Tick += (s, e) =>
            {
                debounceTimer.Stop();
                UserSettings.LargeScreenThreshold = _thresholdSlider.Value;
            };

            _thresholdSlider.ValueChanged += (s, e) => {
                _thresholdLabel.Text = $"Threshold: {_thresholdSlider.Value}\"";
                debounceTimer.Stop();
                debounceTimer.Start();
            };
            layout.Controls.Add(_thresholdLabel);
            layout.Controls.Add(_thresholdSlider);

            _detectionModeCombo = new ComboBox { Width = 350, DropDownStyle = ComboBoxStyle.DropDownList };
            _detectionModeCombo.Items.Add("Primary Monitor Only");
            _detectionModeCombo.Items.Add("Any Monitor (Highest)");
            _detectionModeCombo.SelectedIndex = (int)UserSettings.LargeScreenDetectionMode;
            _detectionModeCombo.SelectedIndexChanged += (s, e) => UserSettings.LargeScreenDetectionMode = (LargeScreenDetectionMode)_detectionModeCombo.SelectedIndex;
            layout.Controls.Add(new Label { Text = "Detection Mode:", AutoSize = true, Margin = new Padding(0, 15, 0, 0) });
            layout.Controls.Add(_detectionModeCombo);
            return page;
        }

        private TabPage CreateColorPage()
        {
            var page = new TabPage("Color Effects");
            var layout = CreateFlowLayout();
            page.Controls.Add(layout);

            layout.Controls.Add(CreateHeader("Screen Inversion"));
            
            _inversionCheck = CreateCheckBox("Enable Color Transformation", UserSettings.IsNegativeModeEnabled);
            _inversionCheck.CheckedChanged += (s, e) => UserSettings.IsNegativeModeEnabled = _inversionCheck.Checked;
            layout.Controls.Add(_inversionCheck);

            layout.Controls.Add(CreateHeader("Active Theme"));
            
            var themes = new[] { 
                "Negative", 
                "GrayScale", 
                "Sepia", 
                "Red",
                "HueShift180", 
                "NegativeGrayScale", 
                "NegativeSepia", 
                "NegativeRed", 
                "NegativeHueShift180",
                "NegativeHueShift180Variation1",
                "NegativeHueShift180Variation2",
                "NegativeHueShift180Variation3",
                "NegativeHueShift180Variation4"
            };
            _themeList = new ListBox { Width = 400, Height = 300, Font = new Font(this.Font.FontFamily, 10F) }; 
            foreach (var t in themes) _themeList.Items.Add(t);
            _themeList.SelectedItem = UserSettings.ActiveColorEffect;
            _themeList.SelectedIndexChanged += (s, e) => { if (_themeList.SelectedItem != null) UserSettings.ActiveColorEffect = _themeList.SelectedItem.ToString(); };
            layout.Controls.Add(_themeList);

            return page;
        }

        private CheckBox _checkUpdatesCheck;
        private ComboBox _updateFreqCombo;

        private TabPage CreateUpdatesPage()
        {
            var page = new TabPage("Updates");
            var layout = CreateFlowLayout();
            page.Controls.Add(layout);

            layout.Controls.Add(CreateHeader("Automatic Updates"));

            var version = Application.ProductVersion.Split('+')[0];
            layout.Controls.Add(new Label { Text = $"Current Version: v{version}", AutoSize = true, Font = new Font(this.Font.FontFamily, 10F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 15) });
            
            _checkUpdatesCheck = CreateCheckBox("Check for updates automatically", UserSettings.CheckForUpdates);
            _checkUpdatesCheck.CheckedChanged += (s, e) => {
                UserSettings.CheckForUpdates = _checkUpdatesCheck.Checked;
                _updateFreqCombo.Enabled = _checkUpdatesCheck.Checked;
            };
            layout.Controls.Add(_checkUpdatesCheck);

            layout.Controls.Add(new Label { Text = "Check Frequency:", AutoSize = true, Margin = new Padding(0, 10, 0, 0) });
            _updateFreqCombo = new ComboBox { Width = 350, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var freq in Enum.GetValues(typeof(UpdateFrequency)))
            {
                _updateFreqCombo.Items.Add(freq);
            }
            _updateFreqCombo.SelectedItem = UserSettings.UpdateFrequency;
            _updateFreqCombo.Enabled = UserSettings.CheckForUpdates;
            _updateFreqCombo.SelectedIndexChanged += (s, e) => UserSettings.UpdateFrequency = (UpdateFrequency)_updateFreqCombo.SelectedItem;
            layout.Controls.Add(_updateFreqCombo);

            var checkBtn = new Button { Text = "Check for Updates Now", Width = 280, Height = 40, Margin = new Padding(0, 30, 0, 0) };
            checkBtn.Click += async (s, e) => {
                checkBtn.Enabled = false;
                checkBtn.Text = "Checking...";
                await UpdateHelper.CheckForUpdatesAsync(true); // Manual check
                checkBtn.Enabled = true;
                checkBtn.Text = "Check for Updates Now";
            };
            layout.Controls.Add(checkBtn);

            var lastCheckLabel = new Label { 
                Text = $"Last checked: {UserSettings.LastUpdateCheck:g}", 
                AutoSize = true, 
                ForeColor = Color.Gray, 
                Margin = new Padding(0, 10, 0, 0) 
            };
            layout.Controls.Add(lastCheckLabel);
            _lastCheckLabel = lastCheckLabel;

            UserSettings.SettingChanged += OnUpdateSettingChanged;

            return page;
        }

        private Label _lastCheckLabel;

        private void OnUpdateSettingChanged(object s, string prop)
        {
            if (prop == nameof(UserSettings.LastUpdateCheck)) {
                if (this.IsDisposed || !this.IsHandleCreated) return;
                this.Invoke(new Action(() => {
                    if (_lastCheckLabel != null && !_lastCheckLabel.IsDisposed)
                        _lastCheckLabel.Text = $"Last checked: {UserSettings.LastUpdateCheck:g}";
                }));
            }
        }

        private TabPage CreateBrightnessPage()
        {
            var page = new TabPage("Brightness");
            var layout = CreateFlowLayout();
            page.Controls.Add(layout);

            layout.Controls.Add(CreateHeader("Connected Monitors & Nicknames"));
            layout.Controls.Add(new Label
            {
                Text = "Custom nicknames are saved automatically and can be targeted in your shortcut rules.",
                Font = new Font(this.Font.FontFamily, 9F),
                AutoSize = true,
                ForeColor = Color.Gray,
                Margin = new Padding(0, 0, 0, 10)
            });

            var monitors = BrightnessService.Instance.GetMonitors();

            var hotkeysCardsPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(750, 0),
                Width = 750,
                Margin = new Padding(0, 0, 0, 10)
            };

            Action renderHotkeys = null!;
            var statusUpdaters = new List<Action>();

            if (monitors.Count == 0)
            {
                layout.Controls.Add(new Label
                {
                    Text = "No DDC/CI or internal displays detected. If using an external monitor, ensure DDC/CI is enabled in its physical OSD menu.",
                    AutoSize = true,
                    ForeColor = Color.DarkOrange,
                    Margin = new Padding(0, 5, 0, 15)
                });
            }
            else
            {
                var monitorsPanel = new TableLayoutPanel
                {
                    ColumnCount = 3,
                    AutoSize = true,
                    Width = 750,
                    Margin = new Padding(0, 0, 0, 20)
                };
                monitorsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
                monitorsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
                monitorsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));

                monitorsPanel.Controls.Add(new Label { Text = "Detected Display", UseMnemonic = false, Font = new Font(this.Font, FontStyle.Bold), AutoSize = true }, 0, 0);
                monitorsPanel.Controls.Add(new Label { Text = "Nickname", UseMnemonic = false, Font = new Font(this.Font, FontStyle.Bold), AutoSize = true }, 1, 0);
                monitorsPanel.Controls.Add(new Label { Text = "Test Brightness", UseMnemonic = false, Font = new Font(this.Font, FontStyle.Bold), AutoSize = true }, 2, 0);

                int row = 1;
                foreach (var mon in monitors)
                {
                    var monCapture = mon;
                    var nameLabel = new Label
                    {
                        Text = $"{mon.SystemModelName}\n({(mon.IsInternal ? "Internal Screen" : mon.DeviceName)})",
                        AutoSize = true,
                        Margin = new Padding(0, 6, 10, 6)
                    };

                    var nickBox = new TextBox
                    {
                        Text = mon.Nickname,
                        Width = 210,
                        Margin = new Padding(0, 6, 10, 6)
                    };
                    nickBox.TextChanged += (s, e) =>
                    {
                        monCapture.Nickname = nickBox.Text.Trim();
                        var dict = new Dictionary<string, string>(UserSettings.MonitorNicknames);
                        dict[monCapture.HardwareId] = monCapture.Nickname;
                        UserSettings.MonitorNicknames = dict;
                        renderHotkeys?.Invoke();
                    };

                    var sliderPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
                    var slider = new TrackBar
                    {
                        Minimum = 0,
                        Maximum = 100,
                        Value = Math.Clamp(mon.CurrentBrightness, 0, 100),
                        TickFrequency = 10,
                        Width = 180,
                        Margin = new Padding(0)
                    };
                    var valLabel = new Label { Text = $"{slider.Value}%", AutoSize = true, MinimumSize = new Size(50, 0), Margin = new Padding(5, 6, 0, 0) };

                    slider.Scroll += (s, e) =>
                    {
                        valLabel.Text = $"{slider.Value}%";
                        BrightnessService.Instance.SetBrightnessDirect(monCapture.HardwareId, slider.Value);
                    };

                    sliderPanel.Controls.Add(slider);
                    sliderPanel.Controls.Add(valLabel);

                    monitorsPanel.Controls.Add(nameLabel, 0, row);
                    monitorsPanel.Controls.Add(nickBox, 1, row);
                    monitorsPanel.Controls.Add(sliderPanel, 2, row);
                    row++;
                }

                layout.Controls.Add(monitorsPanel);
            }

            layout.Controls.Add(CreateHeader("Hotkeys"));
            layout.Controls.Add(new Label
            {
                Text = "Adjust brightness and other display settings using global keyboard shortcuts.",
                Font = new Font(this.Font.FontFamily, 9F),
                AutoSize = true,
                ForeColor = Color.Gray,
                Margin = new Padding(0, 0, 0, 10)
            });

            void SaveShortcutsAndReload()
            {
                UserSettings.BrightnessShortcuts = new List<DisplayShortcutRule>(UserSettings.BrightnessShortcuts);
                HotkeyHookWindow.Instance?.ReloadHotkeys();
            }

            renderHotkeys = () =>
            {
                hotkeysCardsPanel.SuspendLayout();
                hotkeysCardsPanel.Controls.Clear();
                statusUpdaters.Clear();

                var currentMonitors = BrightnessService.Instance.GetMonitors();
                var rules = UserSettings.BrightnessShortcuts;

                if (rules == null || rules.Count == 0)
                {
                    var emptyLabel = new Label
                    {
                        Text = "No hotkeys configured yet. Click '+ Add Hotkey' below to set up a shortcut.",
                        AutoSize = true,
                        ForeColor = Color.DimGray,
                        Margin = new Padding(0, 5, 0, 15)
                    };
                    hotkeysCardsPanel.Controls.Add(emptyLabel);
                }
                else
                {
                    for (int i = 0; i < rules.Count; i++)
                    {
                        var rule = rules[i];
                        var card = new Panel
                        {
                            Size = new Size(750, 88),
                            MinimumSize = new Size(750, 88),
                            MaximumSize = new Size(750, 88),
                            Margin = new Padding(0, 0, 0, 10),
                            BackColor = Color.FromArgb(248, 249, 250),
                            BorderStyle = BorderStyle.FixedSingle
                        };

                        // Row 1: Shortcut input, Clear, Status, Delete
                        var topRow = new TableLayoutPanel
                        {
                            Location = new Point(12, 10),
                            Size = new Size(726, 32),
                            ColumnCount = 4,
                            RowCount = 1,
                            Margin = new Padding(0),
                            Padding = new Padding(0)
                        };
                        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220f));
                        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70f));
                        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80f));
                        topRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

                        var hotkeyBox = new HotkeyInputBox
                        {
                            Dock = DockStyle.Fill,
                            Margin = new Padding(0, 2, 8, 2)
                        };
                        hotkeyBox.Accelerator = rule.GetFormattedShortcut() == "None" ? "" : rule.GetFormattedShortcut();

                        var clearBtn = new Button
                        {
                            Text = "Clear",
                            Dock = DockStyle.Fill,
                            Margin = new Padding(0, 2, 8, 2)
                        };

                        var statusLabel = new Label
                        {
                            Dock = DockStyle.Fill,
                            TextAlign = ContentAlignment.MiddleLeft,
                            Margin = new Padding(4, 2, 8, 2)
                        };

                        void UpdateStatus()
                        {
                            if (rule.IsRegistered)
                            {
                                statusLabel.Text = "✔ Active";
                                statusLabel.ForeColor = Color.FromArgb(16, 124, 16);
                            }
                            else if (string.IsNullOrWhiteSpace(rule.Accelerator) && rule.Key == Keys.None)
                            {
                                statusLabel.Text = "⚪ Not set";
                                statusLabel.ForeColor = Color.Gray;
                            }
                            else
                            {
                                statusLabel.Text = "⚠ Inactive / Conflict";
                                statusLabel.ForeColor = Color.FromArgb(196, 43, 28);
                            }
                        }

                        UpdateStatus();
                        statusUpdaters.Add(UpdateStatus);

                        hotkeyBox.AcceleratorChanged += (s, e) =>
                        {
                            rule.Accelerator = hotkeyBox.Accelerator;
                            SaveShortcutsAndReload();
                            UpdateStatus();
                        };

                        clearBtn.Click += (s, e) =>
                        {
                            hotkeyBox.ClearHotkey();
                            rule.Accelerator = string.Empty;
                            SaveShortcutsAndReload();
                            UpdateStatus();
                        };

                        var deleteBtn = new Button
                        {
                            Text = "Delete",
                            Dock = DockStyle.Fill,
                            ForeColor = Color.FromArgb(196, 43, 28),
                            FlatStyle = FlatStyle.Flat,
                            Margin = new Padding(0, 2, 0, 2)
                        };
                        deleteBtn.FlatAppearance.BorderColor = Color.FromArgb(220, 180, 180);

                        deleteBtn.Click += (s, e) =>
                        {
                            var list = new List<DisplayShortcutRule>(UserSettings.BrightnessShortcuts);
                            list.RemoveAll(r => r.Id == rule.Id);
                            UserSettings.BrightnessShortcuts = list;
                            SaveShortcutsAndReload();
                            renderHotkeys();
                        };

                        topRow.Controls.Add(hotkeyBox, 0, 0);
                        topRow.Controls.Add(clearBtn, 1, 0);
                        topRow.Controls.Add(statusLabel, 2, 0);
                        topRow.Controls.Add(deleteBtn, 3, 0);

                        // Row 2: Target Display, Action, Amount %
                        var bottomRow = new FlowLayoutPanel
                        {
                            Location = new Point(12, 48),
                            Size = new Size(726, 32),
                            FlowDirection = FlowDirection.LeftToRight,
                            WrapContents = false,
                            Margin = new Padding(0)
                        };

                        var targetLbl = new Label { Text = "Target:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) };
                        var targetCombo = new ComboBox
                        {
                            Size = new Size(190, 26),
                            DropDownStyle = ComboBoxStyle.DropDownList,
                            Margin = new Padding(0, 2, 16, 0)
                        };
                        targetCombo.Items.Add(new ComboBoxItem("All Displays", "ALL"));
                        foreach (var m in currentMonitors)
                        {
                            targetCombo.Items.Add(new ComboBoxItem(m.DisplayName, m.HardwareId));
                        }

                        int selectedTargetIndex = 0;
                        for (int t = 0; t < targetCombo.Items.Count; t++)
                        {
                            if (targetCombo.Items[t] is ComboBoxItem item && (string)item.Value == rule.TargetMonitorId)
                            {
                                selectedTargetIndex = t;
                                break;
                            }
                        }
                        targetCombo.SelectedIndex = selectedTargetIndex;
                        targetCombo.SelectedIndexChanged += (s, e) =>
                        {
                            if (targetCombo.SelectedItem is ComboBoxItem selectedItem)
                            {
                                rule.TargetMonitorId = selectedItem.Value.ToString() ?? "ALL";
                                SaveShortcutsAndReload();
                            }
                        };

                        var actionLbl = new Label { Text = "Action:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) };
                        var actionCombo = new ComboBox
                        {
                            Size = new Size(150, 26),
                            DropDownStyle = ComboBoxStyle.DropDownList,
                            Margin = new Padding(0, 2, 16, 0)
                        };
                        actionCombo.Items.Add(new ComboBoxItem("Increase (+)", ShortcutAction.Increase));
                        actionCombo.Items.Add(new ComboBoxItem("Decrease (-)", ShortcutAction.Decrease));
                        actionCombo.Items.Add(new ComboBoxItem("Set to (=)", ShortcutAction.SetTo));

                        int selectedActionIndex = 0;
                        for (int a = 0; a < actionCombo.Items.Count; a++)
                        {
                            if (actionCombo.Items[a] is ComboBoxItem item && (ShortcutAction)item.Value == rule.Action)
                            {
                                selectedActionIndex = a;
                                break;
                            }
                        }
                        actionCombo.SelectedIndex = selectedActionIndex;
                        actionCombo.SelectedIndexChanged += (s, e) =>
                        {
                            if (actionCombo.SelectedItem is ComboBoxItem selectedItem)
                            {
                                rule.Action = (ShortcutAction)selectedItem.Value;
                                SaveShortcutsAndReload();
                            }
                        };

                        var valLbl = new Label { Text = "Amount:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) };
                        var valBox = new NumericUpDown
                        {
                            Minimum = 1,
                            Maximum = 100,
                            Value = Math.Clamp(rule.Value, 1, 100),
                            Size = new Size(55, 26),
                            Margin = new Padding(0, 2, 4, 0)
                        };
                        valBox.ValueChanged += (s, e) =>
                        {
                            rule.Value = (int)valBox.Value;
                            SaveShortcutsAndReload();
                        };

                        var pctLbl = new Label { Text = "%", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };

                        bottomRow.Controls.Add(targetLbl);
                        bottomRow.Controls.Add(targetCombo);
                        bottomRow.Controls.Add(actionLbl);
                        bottomRow.Controls.Add(actionCombo);
                        bottomRow.Controls.Add(valLbl);
                        bottomRow.Controls.Add(valBox);
                        bottomRow.Controls.Add(pctLbl);

                        card.Controls.Add(topRow);
                        card.Controls.Add(bottomRow);

                        hotkeysCardsPanel.Controls.Add(card);
                    }
                }

                hotkeysCardsPanel.ResumeLayout(true);
            };

            layout.Controls.Add(hotkeysCardsPanel);

            var addBtn = new Button
            {
                Text = "+ Add Hotkey",
                Size = new Size(160, 34),
                MinimumSize = new Size(160, 34),
                Font = new Font(this.Font, FontStyle.Bold),
                Margin = new Padding(0, 5, 0, 25)
            };
            addBtn.Click += (s, e) =>
            {
                var newRule = new DisplayShortcutRule
                {
                    Id = Guid.NewGuid().ToString(),
                    Accelerator = string.Empty,
                    TargetMonitorId = "ALL",
                    Action = ShortcutAction.Increase,
                    Value = 10,
                    Enabled = true
                };
                var list = new List<DisplayShortcutRule>(UserSettings.BrightnessShortcuts) { newRule };
                UserSettings.BrightnessShortcuts = list;
                SaveShortcutsAndReload();
                renderHotkeys();
            };
            layout.Controls.Add(addBtn);

            _hotkeysReloadedHandler = (s, e) =>
            {
                if (!this.IsDisposed && this.IsHandleCreated)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        foreach (var update in statusUpdaters) update();
                    }));
                }
            };
            HotkeyHookWindow.HotkeysReloaded += _hotkeysReloadedHandler;

            renderHotkeys();
            return page;
        }

        private class ComboBoxItem
        {
            public string Text { get; }
            public object Value { get; }

            public ComboBoxItem(string text, object value)
            {
                Text = text;
                Value = value;
            }

            public override string ToString() => Text;
        }

        private FlowLayoutPanel CreateFlowLayout() => new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(15), AutoScroll = true, WrapContents = false };

        private Label CreateHeader(string text) => new Label { Text = text, UseMnemonic = false, Font = new Font(this.Font.FontFamily, 12F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 15, 0, 10) };

        private CheckBox CreateCheckBox(string text, bool @checked) => new CheckBox { Text = text, Checked = @checked, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (this.ActiveControl is HotkeyInputBox)
            {
                return false;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

    }
}
