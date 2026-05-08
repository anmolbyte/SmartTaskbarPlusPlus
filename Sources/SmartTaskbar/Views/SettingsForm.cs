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
        public SettingsForm()
        {
            InitializeComponent();
            SetupUI();
            SubscribeToSettings();
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
            _tabControl.TabPages.Add(CreateUpdatesPage());
        }
        
        private TabControl _tabControl;

        private CheckBox _autoModeCheck;
        private CheckBox _inversionCheck;
        private CheckBox _disableLargeScreenCheck;
        private ComboBox _clickActionCombo;
        private ComboBox _doubleClickActionCombo;

        private CheckBox _startOnLoginCheck;

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
                        case nameof(UserSettings.StartOnLogin):
                            if (_startOnLoginCheck != null) _startOnLoginCheck.Checked = UserSettings.StartOnLogin;
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

            _startOnLoginCheck = CreateCheckBox("Launch SmartTaskbar++ on Windows Startup", UserSettings.StartOnLogin);
            _startOnLoginCheck.CheckedChanged += (s, e) => UserSettings.StartOnLogin = _startOnLoginCheck.Checked;
            layout.Controls.Add(_startOnLoginCheck);

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

        private CheckBox _checkUpdatesCheck;
        private ComboBox _updateFreqCombo;

        private TabPage CreateUpdatesPage()
        {
            var page = new TabPage("Updates");
            var layout = CreateFlowLayout();
            page.Controls.Add(layout);

            layout.Controls.Add(CreateHeader("Automatic Updates"));

            layout.Controls.Add(new Label { Text = $"Current Version: v{Application.ProductVersion}", AutoSize = true, Font = new Font(this.Font.FontFamily, 10F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 15) });

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

            var checkBtn = new Button { Text = "Check for Updates Now", Width = 250, Height = 40, Margin = new Padding(0, 30, 0, 0) };
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

            UserSettings.SettingChanged += (s, prop) => {
                if (prop == nameof(UserSettings.LastUpdateCheck)) {
                    this.Invoke(new Action(() => lastCheckLabel.Text = $"Last checked: {UserSettings.LastUpdateCheck:g}"));
                }
            };

            return page;
        }

        private FlowLayoutPanel CreateFlowLayout() => new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(15), AutoScroll = true, WrapContents = false };

        private Label CreateHeader(string text) => new Label { Text = text, Font = new Font(this.Font.FontFamily, 12F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 15, 0, 10) };

        private CheckBox CreateCheckBox(string text, bool @checked) => new CheckBox { Text = text, Checked = @checked, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };

    }
}
