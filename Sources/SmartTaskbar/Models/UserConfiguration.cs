using System.Collections.Generic;
using SmartTaskbar.Models;

namespace SmartTaskbar
{
    /// <summary>
    ///     User settings configuration
    /// </summary>
    internal struct UserConfiguration
    {
        /// <summary>
        ///     Auto mode type
        /// </summary>
        public AutoModeType AutoModeType { get; set; }

        /// <summary>
        ///     Show taskbar when exiting
        /// </summary>
        public bool ShowTaskbarWhenExit { get; set; }

        /// <summary>
        ///     Hide the taskbar when a fullscreen application is in the foreground
        /// </summary>
        public bool HideTaskbarWhenFullscreen { get; set; }

        /// <summary>
        ///     Start on login
        /// </summary>
        public bool StartOnLogin { get; set; }

        /// <summary>
        ///     Large screen threshold in inches
        /// </summary>
        public double LargeScreenThreshold { get; set; }

        /// <summary>
        ///     Disable the large screen override behavior
        /// </summary>
        public bool DisableLargeScreenOverride { get; set; }

        /// <summary>
        ///     Whether screen inversion is enabled
        /// </summary>
        public bool IsNegativeModeEnabled { get; set; }

        /// <summary>
        ///     Current active color effect name
        /// </summary>
        public string ActiveColorEffect { get; set; }

        public TrayClickAction ClickAction { get; set; }
        public LargeScreenDetectionMode LargeScreenDetectionMode { get; set; }
        
        public bool CheckForUpdates { get; set; }
        public UpdateFrequency UpdateFrequency { get; set; }
        public DateTime LastUpdateCheck { get; set; }
    }
}
