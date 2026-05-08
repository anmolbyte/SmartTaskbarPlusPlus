using System;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;

namespace SmartTaskbar.Helpers
{
    public static class StartupHelper
    {
        private static string StartupFolderPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            "SmartTaskbar++.lnk");

        public static void SetStartup(bool enable)
        {
            try
            {
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
            var directory = Path.GetDirectoryName(appPath);
            
            // Use PowerShell to create the shortcut to avoid COM reference issues in .NET 6
            var command = $"$s = (New-Object -ComObject WScript.Shell).CreateShortcut('{StartupFolderPath}'); " +
                          $"$s.TargetPath = '{appPath}'; " +
                          $"$s.WorkingDirectory = '{directory}'; " +
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
