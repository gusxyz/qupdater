using System;
using System.Drawing;
using System.Windows.Forms;

namespace QUpdater;

public partial class Program
{
        private static bool TryCreateProgressForm(string version, out Form progressForm, out ProgressBar progressBar, out Label sizeLabel)
        {
                var result = MessageBox.Show(
                        $"A new version of qBittorrent ({version}) is available. Would you like to download and install it?",
                        "Update Available",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                );

                if (result != DialogResult.Yes)
                {
                        progressForm = null;
                        progressBar = null;
                        sizeLabel = null;
                        return true;
                }

                progressForm = new Form
                {
                        Text = "Downloading Update",
                        Size = new Size(400, 150),
                        FormBorderStyle = FormBorderStyle.FixedDialog,
                        StartPosition = FormStartPosition.CenterScreen,
                        MaximizeBox = false,
                        MinimizeBox = false,
                        ControlBox = false,
                        TopMost = true
                };

                var label = new Label
                {
                        Text = "Downloading qBittorrent update...",
                        AutoSize = true,
                        Location = new Point(10, 20)
                };

                progressBar = new ProgressBar
                {
                        Location = new Point(10, 50),
                        Size = new Size(365, 23),
                        Minimum = 0,
                        Maximum = 100,
                        Style = ProgressBarStyle.Continuous
                };

                sizeLabel = new Label
                {
                        Text = "0 MB / 0 MB",
                        AutoSize = true,
                        Location = new Point(10, 80)
                };

                progressForm.Controls.AddRange(label, progressBar, sizeLabel);
                progressForm.Show();
                return false;
        }
        
        private void SetupTrayIcon()
        {
                // Load the icon from embedded resource
                Icon appIcon;
                try
                {
                        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                        using var stream = assembly.GetManifestResourceStream("QUpdater.icon.ico");
                        appIcon = stream != null ? new Icon(stream) : SystemIcons.Application;
                }
                catch
                {
                        appIcon = SystemIcons.Application;
                }

                _trayIcon = new NotifyIcon()
                {
                        Icon = appIcon,
                        Visible = true,
                        Text = "qUpdater"
                };

                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add("Check for Updates", null, async void (_, _) =>
                {
                        try
                        {
                                await CheckForUpdates();
                        }
                        catch (Exception ex)
                        {
                                MessageBox.Show(ex.Message, "Exception", MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                        }
                });
                contextMenu.Items.Add("About", null, ShowAbout);
                contextMenu.Items.Add("-");
                contextMenu.Items.Add("Exit", null, (_,_) => Application.Exit());

                _trayIcon.ContextMenuStrip = contextMenu;
        }

        private static void ShowAbout(object sender, EventArgs e)
        {
                MessageBox.Show(
                        "qUpdater\n\n" +
                        "This tool automatically checks for qBittorrent updates\n" +
                        "on startup and helps you install them.\n\n" +
                        "You can also manually check for updates using the tray menu.",
                        "About qUpdater",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                );
        }
}