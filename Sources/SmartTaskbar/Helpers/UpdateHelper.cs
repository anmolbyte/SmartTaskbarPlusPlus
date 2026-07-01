using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartTaskbar.Models;

namespace SmartTaskbar.Helpers
{
    public static class UpdateHelper
    {
        private const string RepoApiUrl = "https://api.github.com/repos/anmolbyte/SmartTaskbar/releases/latest";
        private static readonly HttpClient HttpClient = new HttpClient();

        static UpdateHelper()
        {
            // GitHub API requires a User-Agent
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "SmartTaskbar-App");
        }

        public static async Task CheckForUpdatesAsync(bool manualCheck = false)
        {
            try
            {
                UserSettings.LastUpdateCheck = DateTime.Now;
                
                var response = await HttpClient.GetStringAsync(RepoApiUrl);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (!root.TryGetProperty("tag_name", out var tagProp))
                {
                    if (manualCheck) MessageBox.Show("No releases found on GitHub.", "Update Check");
                    return;
                }

                var latestVersionTag = tagProp.GetString()?.TrimStart('v');
                var currentVersion = Application.ProductVersion.Split('+')[0]; 

                if (Version.TryParse(latestVersionTag, out var latest) && 
                    Version.TryParse(currentVersion, out var current))
                {
                    if (latest > current)
                    {
                        var body = root.GetProperty("body").GetString();
                        var downloadUrl = "";
                        
                        foreach (var asset in root.GetProperty("assets").EnumerateArray())
                        {
                            var name = asset.GetProperty("name").GetString();
                            if (name != null && name.EndsWith("Setup.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                                break;
                            }
                        }

                        if (string.IsNullOrEmpty(downloadUrl))
                        {
                            if (manualCheck) MessageBox.Show($"Version v{latestVersionTag} is available, but the installer was not found on the release page.", "Update Available");
                            return;
                        }

                        var result = MessageBox.Show(
                            $"A new version (v{latestVersionTag}) is available!\n\nChanges:\n{body}\n\nWould you like to download and install it now?",
                            "Update Available",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information);

                        if (result == DialogResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo(downloadUrl) { UseShellExecute = true });
                            Application.Exit();
                        }
                    }
                    else if (manualCheck)
                    {
                        MessageBox.Show($"You are running the latest version (v{currentVersion}).", "No Updates Found");
                    }
                }
                else if (manualCheck)
                {
                    MessageBox.Show($"Could not parse version numbers.\nLocal: {currentVersion}\nRemote: {latestVersionTag}", "Version Error");
                }
            }
            catch (Exception ex)
            {
                if (manualCheck) MessageBox.Show($"Failed to check for updates.\n\nError: {ex.Message}\n\nMake sure the repository '{RepoApiUrl}' is public and has releases.", "Update Error");
            }
        }

        public static bool ShouldCheckNow()
        {
            if (!UserSettings.CheckForUpdates) return false;

            var lastCheck = UserSettings.LastUpdateCheck;
            var now = DateTime.Now;
            var diff = now - lastCheck;

            return UserSettings.UpdateFrequency switch
            {
                UpdateFrequency.Hour => diff.TotalHours >= 1,
                UpdateFrequency.Day => diff.TotalDays >= 1,
                UpdateFrequency.Week => diff.TotalDays >= 7,
                UpdateFrequency.Month => diff.TotalDays >= 30,
                _ => false
            };
        }
    }
}
