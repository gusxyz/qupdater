using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QUpdater;

public partial class Program
{
        [GeneratedRegex(@"qbittorrent_(\d+\.\d+\.\d+)_x64_setup\.exe")]
        private static partial Regex ExecutableRegex();

        private NotifyIcon _trayIcon;
        private HttpClient _httpClient;
        private bool _isChecking;

        [STAThread]
        private static void Main()
        {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var program = new Program();
                program.Initialize();
                Application.Run();
        }

        private void Initialize()
        {
                _httpClient = new HttpClient();
                SetupTrayIcon();
                _ = CheckForUpdates();
        }

        private static string GetInstalledVersion()
        {
                string[] registryPaths =
                [
                        @"SOFTWARE\WOW6432Node\qBittorrent",
                        @"SOFTWARE\qBittorrent"
                ];

                foreach (var path in registryPaths)
                {
                        try
                        {
                                using var key = Registry.LocalMachine.OpenSubKey(path);
                                if (key == null) continue;
                                var version = key.GetValue("Version") as string;
                                if (!string.IsNullOrEmpty(version))
                                        return version;
                        }
                        catch
                        {
                                // ignored
                        }
                }

                string[] programPaths =
                [
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "qBittorrent", "qbittorrent.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "qBittorrent", "qbittorrent.exe")
                ];

                foreach (var path in programPaths)
                {
                        if (!File.Exists(path)) continue;
                        var versionInfo = FileVersionInfo.GetVersionInfo(path);
                        try
                        {
                                return
                                        $"{versionInfo.FileMajorPart}.{versionInfo.FileMinorPart}.{versionInfo.FileBuildPart}";
                        }
                        catch
                        {
                                // ignored
                        }
                }

                return null;
        }

        private async Task<(string version, string url)> GetLatestVersion()
        {
                try
                {
                        var response = await _httpClient.GetStringAsync("https://sourceforge.net/projects/qbittorrent/best_release.json");
                        using var document = JsonDocument.Parse(response);
                        var root = document.RootElement;
                        var windows = root.GetProperty("platform_releases").GetProperty("windows");
                        var filename = windows.GetProperty("filename").GetString();
                        var url = windows.GetProperty("url").GetString();

                        if (filename != null)
                        {
                                var match = ExecutableRegex().Match(filename);
                                if (match.Success) return (match.Groups[1].Value, url);
                        }
                }
                catch (Exception ex)
                {
                        MessageBox.Show($"Error checking for updates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                return (null, null);
        }

        private async Task DownloadAndInstall(string version, string url)
        {
                if (TryCreateProgressForm(version, out var progressForm, out var progressBar, out var sizeLabel))
                        return;
                
                try
                {
                        var downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", $"qbittorrent_{version}_x64_setup.exe");

                        using (var response =
                               await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                        {
                                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                                var totalMb = totalBytes / 1024.0 / 1024.0;

                                await using (var downloadStream = await response.Content.ReadAsStreamAsync())
                                await using (var fileStream = new FileStream(downloadPath, FileMode.Create))
                                {
                                        var buffer = new byte[8192];
                                        int bytesRead;
                                        var totalBytesRead = 0L;

                                        while ((bytesRead =
                                                       await downloadStream.ReadAsync(buffer)) > 0)
                                        {
                                                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                                                totalBytesRead += bytesRead;

                                                var progress = (int)(totalBytesRead * 100 / totalBytes);
                                                var downloadedMb = totalBytesRead / 1024.0 / 1024.0;

                                                progressBar.Value = progress;
                                                sizeLabel.Text = $"{downloadedMb:F1} MB / {totalMb:F1} MB";
                                                progressForm.Update();
                                        }
                                }
                        }

                        progressForm.Close();

                        // Kill qBittorrent if running
                        foreach (var process in Process.GetProcessesByName("qbittorrent"))
                        {
                                process.Kill();
                                await process.WaitForExitAsync();
                        }

                        // Start installer
                        Process.Start(new ProcessStartInfo(downloadPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                        progressForm.Close();
                        MessageBox.Show($"Error downloading update: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
        }

        private async Task CheckForUpdates()
        {
                if (_isChecking)
                        return;

                _isChecking = true;

                try
                {
                        var currentVersion = GetInstalledVersion();
                        if (string.IsNullOrEmpty(currentVersion))
                        {
                                MessageBox.Show("Could not detect qBittorrent installation.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                        }

                        var (latestVersion, downloadUrl) = await GetLatestVersion();
                        if (!string.IsNullOrEmpty(latestVersion))
                        {
                                var current = Version.Parse(currentVersion);
                                var latest = Version.Parse(latestVersion);

                                if (latest > current)
                                        await DownloadAndInstall(latestVersion, downloadUrl);
                                else
                                        MessageBox.Show("qBittorrent is up to date!", "No Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                }
                finally
                {
                        _isChecking = false;
                }
        }
}