using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SmartTaskbar.Models;
using SmartTaskbar.Helpers;

namespace SmartTaskbar.Views
{
    public partial class SettingsForm : Form
    {
        private readonly HotkeyManager _hotkeyManager;
        private TabControl _tabControl;

        public SettingsForm(HotkeyManager hotkeyManager)
        {
            _hotkeyManager = hotkeyManager;
            InitializeComponent();
            SetupUI();
            SubscribeToSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "SmartTaskbar++ Settings";
            // Set a large enough default size that will scale with DPI
            this.Size = new Size(850, 650);
            this.MinimumSize = new Size(600, 500);
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
            _tabControl.TabPages.Add(CreateMonitorPage());
            _tabControl.TabPages.Add(CreateHotkeyPage());
        }

        private CheckBox _autoModeCheck;
        private CheckBox _inversionCheck;
        private CheckBox _disableLargeScreenCheck;
        private ComboBox _clickActionCombo;
        private ComboBox _doubleClickActionCombo;

        private void SubscribeToSettings()
        {
            UserSettings.SettingChanged += (s, propertyName) =>
            {
                if (this.IsDisposed) return;
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
                    }
                }));
            };
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

            _disableLargeScreenCheck = CreateCheckBox("Disable Large Screen Override", UserSettings.DisableLargeScreenOverride);
            _disableLargeScreenCheck.CheckedChanged += (s, e) => UserSettings.DisableLargeScreenOverride = _disableLargeScreenCheck.Checked;
            layout.Controls.Add(_disableLargeScreenCheck);
            layout.Controls.Add(new Label { Text = "Large screen detection forces the taskbar to stay visible on displays larger than the threshold (useful for presentations).", Font = new Font(this.Font.FontFamily, 9F), AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(0, 0, 0, 15) });

            layout.Controls.Add(CreateHeader("Icon Click Behavior"));
            
            _clickActionCombo = new ComboBox { Width = 350, DropDownStyle = ComboBoxStyle.DropDownList };
            _doubleClickActionCombo = new ComboBox { Width = 350, DropDownStyle = ComboBoxStyle.DropDownList };
            
            foreach (var action in Enum.GetValues(typeof(TrayClickAction)))
            {
                _clickActionCombo.Items.Add(action);
                _doubleClickActionCombo.Items.Add(action);
            }
            
            _clickActionCombo.SelectedItem = UserSettings.ClickAction;
            _doubleClickActionCombo.SelectedItem = UserSettings.DoubleClickAction;
            
            _clickActionCombo.SelectedIndexChanged += (s, e) => UserSettings.ClickAction = (TrayClickAction)_clickActionCombo.SelectedItem;
            _doubleClickActionCombo.SelectedIndexChanged += (s, e) => UserSettings.DoubleClickAction = (TrayClickAction)_doubleClickActionCombo.SelectedItem;
            
            layout.Controls.Add(new Label { Text = "Single Click:", AutoSize = true });
            layout.Controls.Add(_clickActionCombo);
            layout.Controls.Add(new Label { Text = "Double Click:", AutoSize = true, Margin = new Padding(0, 10, 0, 0) });
            layout.Controls.Add(_doubleClickActionCombo);

            layout.Controls.Add(new Label { Text = "Click Sensitivity (Delay):", AutoSize = true, Margin = new Padding(0, 15, 0, 0) });
            var delayLabel = new Label { Text = $"{UserSettings.ClickDelay} ms", AutoSize = true };
            var delaySlider = new TrackBar { Minimum = 100, Maximum = 1000, Value = UserSettings.ClickDelay, Width = 350 };
            delaySlider.ValueChanged += (s, e) => {
                UserSettings.ClickDelay = delaySlider.Value;
                delayLabel.Text = $"{delaySlider.Value} ms";
            };
            layout.Controls.Add(delayLabel);
            layout.Controls.Add(delaySlider);
            layout.Controls.Add(new Label { Text = "(Reduce delay for faster clicks, or set Double Click to 'None' for zero delay)", Font = new Font(this.Font.FontFamily, 8F), AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(0, 0, 0, 15) });

            layout.Controls.Add(CreateHeader("Large Screen Threshold"));
            
            var thresholdLabel = new Label { Text = $"Threshold: {UserSettings.LargeScreenThreshold}\"", AutoSize = true };
            var thresholdSlider = new TrackBar { Minimum = 10, Maximum = 40, Value = (int)UserSettings.LargeScreenThreshold, Width = 350 };
            thresholdSlider.ValueChanged += (s, e) => {
                UserSettings.LargeScreenThreshold = thresholdSlider.Value;
                thresholdLabel.Text = $"Threshold: {thresholdSlider.Value}\"";
            };
            layout.Controls.Add(thresholdLabel);
            layout.Controls.Add(thresholdSlider);

            var detectionMode = new ComboBox { Width = 350, DropDownStyle = ComboBoxStyle.DropDownList };
            detectionMode.Items.Add("Primary Monitor Only");
            detectionMode.Items.Add("Any Monitor (Highest)");
            detectionMode.SelectedIndex = (int)UserSettings.LargeScreenDetectionMode;
            detectionMode.SelectedIndexChanged += (s, e) => UserSettings.LargeScreenDetectionMode = (LargeScreenDetectionMode)detectionMode.SelectedIndex;
            layout.Controls.Add(new Label { Text = "Detection Mode:", AutoSize = true, Margin = new Padding(0, 15, 0, 0) });
            layout.Controls.Add(detectionMode);

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
                "NegativeGrayScale", 
                "NegativeSepia", 
                "NegativeRed", 
                "NegativeHueShift180",
                "NegativeHueShift180Variation1",
                "NegativeHueShift180Variation2",
                "NegativeHueShift180Variation3",
                "NegativeHueShift180Variation4"
            };
            var themeList = new ListBox { Width = 400, Height = 300, Font = new Font(this.Font.FontFamily, 10F) }; 
            foreach (var t in themes) themeList.Items.Add(t);
            themeList.SelectedItem = UserSettings.ActiveColorEffect;
            themeList.SelectedIndexChanged += (s, e) => UserSettings.ActiveColorEffect = themeList.SelectedItem.ToString();
            layout.Controls.Add(themeList);

            return page;
        }

        private TabPage CreateMonitorPage()
        {
            var page = new TabPage("Monitors");
            var layout = CreateFlowLayout();
            page.Controls.Add(layout);

            layout.Controls.Add(CreateHeader("Connected Monitors"));

            var monitors = MonitorManager.GetMonitorHandles();
            foreach (var hMonitor in monitors)
            {
                var name = MonitorManager.GetMonitorName(hMonitor);
                var group = new GroupBox { Text = name, Width = 550, Height = 180, Margin = new Padding(0, 0, 0, 20) };
                var gLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10) };
                group.Controls.Add(gLayout);

                gLayout.Controls.Add(CreateSlider("Brightness", MonitorManager.GetBrightness(hMonitor), (v) => MonitorManager.SetBrightness(hMonitor, (uint)v)));
                gLayout.Controls.Add(CreateSlider("Contrast", MonitorManager.GetContrast(hMonitor), (v) => MonitorManager.SetContrast(hMonitor, (uint)v)));

                layout.Controls.Add(group);
            }

            return page;
        }

        private TabPage CreateHotkeyPage()
        {
            var page = new TabPage("Hotkeys");
            var layout = CreateFlowLayout();
            page.Controls.Add(layout);

            layout.Controls.Add(CreateHeader("Global Hotkeys"));

            var grid = new DataGridView
            {
                Width = 600,
                Height = 300,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            grid.Columns.Add("Monitor", "Monitor");
            grid.Columns.Add("Action", "Action");
            grid.Columns.Add("Key", "Hotkey");
            
            RefreshHotkeyGrid(grid);
            layout.Controls.Add(grid);

            var btnPanel = new FlowLayoutPanel { Width = 600, Height = 50, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 15, 0, 0) };
            
            var addBtn = new Button { Text = "Add Hotkey", Width = 120, Height = 35 };
            addBtn.Click += (s, e) => {
                using var capture = new HotkeyCaptureForm();
                if (capture.ShowDialog() == DialogResult.OK)
                {
                    var newConfig = new HotkeyConfig 
                    { 
                        Name = "New Brightness Hotkey",
                        Modifiers = capture.SelectedModifiers,
                        Key = capture.SelectedKey,
                        Action = HotkeyAction.BrightnessUp,
                        TargetMonitor = "All",
                        Value = 10
                    };
                    var configs = UserSettings.HotkeyConfigs ?? new List<HotkeyConfig>();
                    configs.Add(newConfig);
                    UserSettings.HotkeyConfigs = configs;
                    
                    RefreshHotkeyGrid(grid);
                    MessageBox.Show("Hotkey added! Restart app to apply.");
                }
            };

            var clearBtn = new Button { Text = "Clear All", Width = 120, Height = 35 };
            clearBtn.Click += (s, e) => {
                UserSettings.HotkeyConfigs = new List<HotkeyConfig>();
                RefreshHotkeyGrid(grid);
            };

            btnPanel.Controls.Add(addBtn);
            btnPanel.Controls.Add(clearBtn);
            layout.Controls.Add(btnPanel);

            return page;
        }

        private void RefreshHotkeyGrid(DataGridView grid)
        {
            grid.Rows.Clear();
            var configs = UserSettings.HotkeyConfigs;
            if (configs == null) return;

            foreach (var config in configs)
            {
                grid.Rows.Add(config.TargetMonitor, config.Action.ToString(), GetHotkeyString(config));
            }
        }

        private string GetHotkeyString(HotkeyConfig config)
        {
            var parts = new List<string>();
            if ((config.Modifiers & Fun.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((config.Modifiers & Fun.MOD_ALT) != 0) parts.Add("Alt");
            if ((config.Modifiers & Fun.MOD_SHIFT) != 0) parts.Add("Shift");
            parts.Add(((Keys)config.Key).ToString());
            return string.Join(" + ", parts);
        }

        private FlowLayoutPanel CreateFlowLayout() => new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(15), AutoScroll = true, WrapContents = false };

        private Label CreateHeader(string text) => new Label { Text = text, Font = new Font(this.Font.FontFamily, 12F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 15, 0, 10) };

        private CheckBox CreateCheckBox(string text, bool @checked) => new CheckBox { Text = text, Checked = @checked, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };

        private Panel CreateSlider(string text, uint value, Action<int> onValueChange)
        {
            var p = new Panel { Width = 500, Height = 50 };
            var l = new Label { Text = $"{text}: {value}%", Width = 120, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleLeft };
            var s = new TrackBar { Minimum = 0, Maximum = 100, Value = (int)value, Width = 300, Dock = DockStyle.Fill, TickStyle = TickStyle.None };
            s.ValueChanged += (sender, e) => {
                onValueChange(s.Value);
                l.Text = $"{text}: {s.Value}%";
            };
            p.Controls.Add(s);
            p.Controls.Add(l);
            return p;
        }
    }
}
