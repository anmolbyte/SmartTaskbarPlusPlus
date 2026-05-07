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
        ///     Large screen threshold in inches
        /// </summary>
        public double LargeScreenThreshold { get; set; }

        /// <summary>
        ///     Disable the large screen override behavior
        /// </summary>
        public bool DisableLargeScreenOverride { get; set; }
    }
}
