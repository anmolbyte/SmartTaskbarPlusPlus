using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using SmartTaskbar.Views;

namespace SmartTaskbar
{
    public static class Program
    {
        private const string MutexName = "SmartTaskbar_Settings_Mutex";

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            using var mutex = new Mutex(true, MutexName, out var createNew);
            if (!createNew)
            {
                // Another settings window is already open - bring it to the front
                var existingHwnd = FindWindow(null, "SmartTaskbar++ Settings");
                if (existingHwnd != IntPtr.Zero)
                {
                    ShowWindow(existingHwnd, SW_RESTORE);
                    BringWindowToTop(existingHwnd);
                    SetForegroundWindow(existingHwnd);
                }
                return;
            }

            ApplicationConfiguration.Initialize();

            var settingsForm = new SettingsForm();
            settingsForm.FormClosed += (s, e) =>
            {
                // Terminate completely to release all RAM immediately when closed
                Environment.Exit(0);
            };

            Application.Run(settingsForm);

            // Clean exit
            Environment.Exit(0);
        }
    }
}
