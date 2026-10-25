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

        private void SettingsForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            UserSettings.SettingChanged -= OnSettingChanged;
            UserSettings.SettingChanged -= OnUpdateSettingChanged;
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

            var targetCombo = new ComboBox
            {
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, 10, 0)
            };

            void PopulateTargetCombo()
            {
                targetCombo.Items.Clear();
                targetCombo.Items.Add(new ComboBoxItem("All Displays", "ALL"));
                foreach (var m in BrightnessService.Instance.GetMonitors())
                {
                    targetCombo.Items.Add(new ComboBoxItem(m.DisplayName, m.HardwareId));
                }
                if (targetCombo.Items.Count > 0) targetCombo.SelectedIndex = 0;
            }

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

                monitorsPanel.Controls.Add(new Label { Text = "Detected Display", Font = new Font(this.Font, FontStyle.Bold), AutoSize = true }, 0, 0);
                monitorsPanel.Controls.Add(new Label { Text = "Nickname", Font = new Font(this.Font, FontStyle.Bold), AutoSize = true }, 1, 0);
                monitorsPanel.Controls.Add(new Label { Text = "Test Brightness", Font = new Font(this.Font, FontStyle.Bold), AutoSize = true }, 2, 0);

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
                        PopulateTargetCombo();
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
                    var valLabel = new Label { Text = $"{slider.Value}%", Width = 50, Margin = new Padding(5, 6, 0, 0) };

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

            layout.Controls.Add(CreateHeader("Hotkey & Shortcut Rules"));
            layout.Controls.Add(new Label
            {
                Text = "Active shortcuts that adjust brightness system-wide in the background.",
                Font = new Font(this.Font.FontFamily, 9F),
                AutoSize = true,
                ForeColor = Color.Gray,
                Margin = new Padding(0, 0, 0, 10)
            });

            var rulesList = new ListView
            {
                View = System.Windows.Forms.View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                Width = 720,
                Height = 160,
                Margin = new Padding(0, 0, 0, 10)
            };
            rulesList.Columns.Add("Target Display", 220);
            rulesList.Columns.Add("Action", 180);
            rulesList.Columns.Add("Shortcut", 180);
            rulesList.Columns.Add("Enabled", 100);

            void RefreshRulesList()
            {
                rulesList.Items.Clear();
                var currentMonitors = BrightnessService.Instance.GetMonitors();
                foreach (var rule in UserSettings.BrightnessShortcuts)
                {
                    string targetName = "All Displays";
                    if (rule.TargetMonitorId != "ALL")
                    {
                        var m = currentMonitors.FirstOrDefault(x => x.HardwareId == rule.TargetMonitorId);
                        targetName = m?.DisplayName ?? "Specific Display";
                    }

                    var item = new ListViewItem(targetName);
                    item.SubItems.Add(rule.GetActionDescription());
                    item.SubItems.Add(rule.GetFormattedShortcut());
                    item.SubItems.Add(rule.Enabled ? "Yes" : "No");
                    item.Tag = rule;
                    rulesList.Items.Add(item);
                }
            }

            RefreshRulesList();
            layout.Controls.Add(rulesList);

            var deleteBtn = new Button
            {
                Text = "Remove Selected Shortcut",
                Width = 200,
                Height = 32,
                Margin = new Padding(0, 0, 0, 20)
            };
            deleteBtn.Click += (s, e) =>
            {
                if (rulesList.SelectedItems.Count > 0 && rulesList.SelectedItems[0].Tag is DisplayShortcutRule rule)
                {
                    var updated = new List<DisplayShortcutRule>(UserSettings.BrightnessShortcuts);
                    updated.RemoveAll(r => r.Id == rule.Id);
                    UserSettings.BrightnessShortcuts = updated;
                    RefreshRulesList();
                }
            };
            layout.Controls.Add(deleteBtn);

            layout.Controls.Add(CreateHeader("Add New Shortcut"));

            var addPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 5, 0, 15)
            };

            PopulateTargetCombo();

            var actionCombo = new ComboBox
            {
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, 10, 0)
            };
            actionCombo.Items.Add(new ComboBoxItem("Increase Brightness", ShortcutAction.Increase));
            actionCombo.Items.Add(new ComboBoxItem("Decrease Brightness", ShortcutAction.Decrease));
            actionCombo.Items.Add(new ComboBoxItem("Set Specific Level", ShortcutAction.SetTo));
            actionCombo.SelectedIndex = 0;

            var amountBox = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 100,
                Value = 5,
                Width = 70,
                Margin = new Padding(0, 0, 10, 0)
            };

            var percentLabel = new Label
            {
                Text = "%",
                AutoSize = true,
                Margin = new Padding(0, 6, 10, 0)
            };

            var hotkeyBox = new HotkeyInputBox
            {
                Width = 180,
                Margin = new Padding(0, 0, 10, 0)
            };

            var addBtn = new Button
            {
                Text = "+ Add Shortcut",
                Width = 120,
                Height = 30,
                Margin = new Padding(0)
            };

            addBtn.Click += (s, e) =>
            {
                if (hotkeyBox.RecordedKey == Keys.None)
                {
                    MessageBox.Show("Please click the shortcut box and press your desired keyboard shortcut (e.g. Ctrl + U).",
                        "No Shortcut Pressed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (targetCombo.SelectedItem is not ComboBoxItem targetItem ||
                    actionCombo.SelectedItem is not ComboBoxItem actionItem)
                {
                    return;
                }

                var targetId = targetItem.Value?.ToString() ?? "ALL";
                var action = (ShortcutAction)(actionItem.Value ?? ShortcutAction.Increase);
                int val = (int)amountBox.Value;

                var newRule = new DisplayShortcutRule
                {
                    TargetMonitorId = targetId,
                    Action = action,
                    Value = val,
                    Modifiers = hotkeyBox.RecordedModifiers,
                    Key = hotkeyBox.RecordedKey,
                    Enabled = true
                };

                var updated = new List<DisplayShortcutRule>(UserSettings.BrightnessShortcuts) { newRule };
                UserSettings.BrightnessShortcuts = updated;

                hotkeyBox.ClearHotkey();
                RefreshRulesList();
            };

            addPanel.Controls.Add(targetCombo);
            addPanel.Controls.Add(actionCombo);
            addPanel.Controls.Add(amountBox);
            addPanel.Controls.Add(percentLabel);
            addPanel.Controls.Add(hotkeyBox);
            addPanel.Controls.Add(addBtn);

            layout.Controls.Add(addPanel);

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

        private Label CreateHeader(string text) => new Label { Text = text, Font = new Font(this.Font.FontFamily, 12F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 15, 0, 10) };

        private CheckBox CreateCheckBox(string text, bool @checked) => new CheckBox { Text = text, Checked = @checked, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };

    }
}
