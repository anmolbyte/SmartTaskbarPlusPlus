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
        }

        private void InitializeComponent()
        {
            this.Text = "SmartTaskbar++ Settings";
            this.Size = new Size(600, 500);
            this.MinimumSize = new Size(500, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = SystemFonts.MessageBoxFont;
            this.AutoScaleMode = AutoScaleMode.Dpi;
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

        private TabPage CreateGeneralPage()
        {
            var page = new TabPage("General");
            var layout = CreateFlowLayout();
            page.Controls.Add(layout);

            layout.Controls.Add(CreateHeader("Application Behavior"));
            
            var autoModeCheck = CreateCheckBox("Enable Smart Auto-Hide", UserSettings.AutoModeType == AutoModeType.Auto);
            autoModeCheck.CheckedChanged += (s, e) => UserSettings.AutoModeType = autoModeCheck.Checked ? AutoModeType.Auto : AutoModeType.None;
            layout.Controls.Add(autoModeCheck);

            layout.Controls.Add(CreateHeader("Large Screen Detection"));
            
            var thresholdLabel = new Label { Text = $"Threshold: {UserSettings.LargeScreenThreshold}\"", AutoSize = true };
            var thresholdSlider = new TrackBar { Minimum = 10, Maximum = 40, Value = (int)UserSettings.LargeScreenThreshold, Width = 300 };
            thresholdSlider.ValueChanged += (s, e) => {
                UserSettings.LargeScreenThreshold = thresholdSlider.Value;
                thresholdLabel.Text = $"Threshold: {thresholdSlider.Value}\"";
            };
            layout.Controls.Add(thresholdLabel);
            layout.Controls.Add(thresholdSlider);

            var detectionMode = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
            detectionMode.Items.Add("Primary Monitor Only");
            detectionMode.Items.Add("Any Monitor (Highest)");
            detectionMode.SelectedIndex = (int)UserSettings.LargeScreenDetectionMode;
            detectionMode.SelectedIndexChanged += (s, e) => UserSettings.LargeScreenDetectionMode = (LargeScreenDetectionMode)detectionMode.SelectedIndex;
            layout.Controls.Add(new Label { Text = "Detection Mode:", AutoSize = true, Margin = new Padding(0, 10, 0, 0) });
            layout.Controls.Add(detectionMode);

            return page;
        }

        private TabPage CreateColorPage()
        {
            var page = new TabPage("Color Effects");
            var layout = CreateFlowLayout();
            page.Controls.Add(layout);

            layout.Controls.Add(CreateHeader("Screen Inversion"));
            
            var inversionCheck = CreateCheckBox("Enable Color Transformation", UserSettings.IsNegativeModeEnabled);
            inversionCheck.CheckedChanged += (s, e) => UserSettings.IsNegativeModeEnabled = inversionCheck.Checked;
            layout.Controls.Add(inversionCheck);

            layout.Controls.Add(CreateHeader("Active Theme"));
            
            var themes = new[] { "Negative", "GrayScale", "Sepia", "Red", "NegativeHueShift180" };
            var themeList = new ListBox { Width = 300, Height = 120 };
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
                var group = new GroupBox { Text = name, Width = 400, Height = 150 };
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
                Width = 500,
                Height = 200,
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

            var btnPanel = new FlowLayoutPanel { Width = 500, Height = 50, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 10, 0, 0) };
            
            var addBtn = new Button { Text = "Add Hotkey", Width = 100, Height = 30 };
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

            var clearBtn = new Button { Text = "Clear All", Width = 100, Height = 30 };
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

        private Label CreateHeader(string text) => new Label { Text = text, Font = new Font(this.Font.FontFamily, 11F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 10, 0, 10) };

        private CheckBox CreateCheckBox(string text, bool @checked) => new CheckBox { Text = text, Checked = @checked, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };

        private Panel CreateSlider(string text, uint value, Action<int> onValueChange)
        {
            var p = new Panel { Width = 350, Height = 40 };
            var l = new Label { Text = $"{text}: {value}%", Width = 100, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleLeft };
            var s = new TrackBar { Minimum = 0, Maximum = 100, Value = (int)value, Dock = DockStyle.Fill, TickStyle = TickStyle.None };
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
