using System;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using Windows.ApplicationModel;

namespace SmartTaskbar.Helpers
{
    public static class StartupHelper
    {
        private static string StartupFolderPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            "SmartTaskbar++.lnk");

        public static async void SetStartup(bool enable)
        {
            try
            {
                try
                {
                    var startupTask = await StartupTask.GetAsync("SmartTaskbarStartup");
                    if (startupTask != null)
                    {
                        if (enable)
                            await startupTask.RequestEnableAsync();
                        else
                            startupTask.Disable();
                        return;
                    }
                }
                catch
                {
                    // Not packaged or StartupTask not supported
                }

                if (enable)
                {
                    CreateShortcut();
                }
                else
                {
                    if (File.Exists(StartupFolderPath))
                    {
                        File.Delete(StartupFolderPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set startup: {ex.Message}");
            }
        }

        private static void CreateShortcut()
        {
            var appPath = Application.ExecutablePath;
            var directory = Path.GetDirectoryName(appPath) ?? "";

            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType)!;
                    dynamic shortcut = shell.CreateShortcut(StartupFolderPath);
                    shortcut.TargetPath = appPath;
                    shortcut.WorkingDirectory = directory;
                    shortcut.Arguments = "--silent";
                    shortcut.Save();
                    return;
                }
            }
            catch
            {
                // Fallback to powershell if COM automation is restricted
            }

            var escapedShortcutPath = StartupFolderPath.Replace("'", "''");
            var escapedAppPath = appPath.Replace("'", "''");
            var escapedDirectory = directory.Replace("'", "''");

            var command = $"$s = (New-Object -ComObject WScript.Shell).CreateShortcut('{escapedShortcutPath}'); " +
                          $"$s.TargetPath = '{escapedAppPath}'; " +
                          $"$s.WorkingDirectory = '{escapedDirectory}'; " +
                          "$s.Arguments = '--silent'; " +
                          "$s.Save()";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{command}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit();
        }

        public static bool IsStartupEnabled()
        {
            return File.Exists(StartupFolderPath);
        }
    }
}