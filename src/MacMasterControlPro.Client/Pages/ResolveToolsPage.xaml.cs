using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class ResolveToolsPage : UserControl
{
    private DispatcherTimer? _timer;
    private readonly Dictionary<string, string> _notifiedJobStates = new();
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    private ResolveMediaAuditResult? _lastResult;
    private readonly HashSet<string> _selectedForDeletion = new();
    private CloudManagerService? _cloud;

    public ResolveToolsPage()
    {
        InitializeComponent();
        LoadEmailSettings();
        LoadConfigFolders();
        LoadRemotes();
    }

    // MARK: - Notificare la final de randare

    private void OnNotifierToggled(object sender, RoutedEventArgs e)
    {
        if (NotifierToggle.IsChecked == true)
        {
            _trayIcon ??= new System.Windows.Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Information,
                Visible = true,
                Text = "Master Control Studio Pro",
            };
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timer.Tick += (_, _) => PollRenderJobsOnce();
            _timer.Start();
            NotifierStatusText.Text = "Activă — verifică la 5 secunde";
        }
        else
        {
            _timer?.Stop();
            _timer = null;
            NotifierStatusText.Text = "Inactivă";
        }
    }

    private void PollRenderJobsOnce()
    {
        Task.Run(() =>
        {
            var (jobs, error) = ResolveRenderJobQuery.FetchJobs();
            Dispatcher.Invoke(() =>
            {
                if (error != null) { NotifierErrorText.Text = "⚠ " + error; return; }
                NotifierErrorText.Text = "";
                foreach (var job in jobs ?? new())
                {
                    var terminal = new[] { "Complete", "Failed", "Cancelled" }.FirstOrDefault(t => job.Status.Contains(t));
                    if (terminal is null) continue;
                    if (_notifiedJobStates.TryGetValue(job.Id, out var prev) && prev == terminal) continue;
                    _notifiedJobStates[job.Id] = terminal;
                    FireNotification(job.Id, terminal);
                }
            });
        });
    }

    private void FireNotification(string jobName, string status)
    {
        var title = status == "Complete" ? "✔ Randare terminată" : $"✘ Randare {status.ToLower()}";
        _trayIcon?.ShowBalloonTip(8000, title, jobName, System.Windows.Forms.ToolTipIcon.Info);
        Task.Run(() => EmailNotifierService.Send($"{title} — {jobName}", $"Job: {jobName}\nStatus: {status}"));
    }

    // MARK: - Setari email

    private void LoadEmailSettings()
    {
        var s = EmailNotifierService.Settings;
        EmailEnabledCheck.IsChecked = s.Enabled;
        SmtpHostBox.Text = s.SmtpHost;
        SmtpPortBox.Text = s.SmtpPort.ToString();
        UsernameBox.Text = s.Username;
        AppPasswordBox.Password = s.AppPassword;
        RecipientBox.Text = s.Recipient;
        EmailFieldsPanel.Visibility = s.Enabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnEmailSettingChanged(object sender, RoutedEventArgs e)
    {
        EmailFieldsPanel.Visibility = EmailEnabledCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SaveEmailSettings();
    }

    private void OnSaveEmailSettings(object sender, RoutedEventArgs e) => SaveEmailSettings();

    private void SaveEmailSettings()
    {
        EmailNotifierService.Settings = new EmailNotifierSettings
        {
            Enabled = EmailEnabledCheck.IsChecked == true,
            SmtpHost = SmtpHostBox.Text,
            SmtpPort = int.TryParse(SmtpPortBox.Text, out var p) ? p : 587,
            Username = UsernameBox.Text,
            AppPassword = AppPasswordBox.Password,
            Recipient = RecipientBox.Text,
        };
    }

    private void OnSendTestEmail(object sender, RoutedEventArgs e)
    {
        SaveEmailSettings();
        TestEmailButton.IsEnabled = false;
        EmailTestStatusText.Text = "Se trimite…";
        Task.Run(() =>
        {
            var (ok, error) = EmailNotifierService.Send("Test — Master Control Studio Pro", "Dacă vezi acest email, notificarea funcționează.");
            Dispatcher.Invoke(() =>
            {
                TestEmailButton.IsEnabled = true;
                EmailTestStatusText.Text = ok ? "✔ Trimis — verifică inboxul." : $"✘ {error}";
            });
        });
    }

    // MARK: - Auditor Media Pool

    private void OnScanClicked(object sender, RoutedEventArgs e)
    {
        ScanButton.IsEnabled = false;
        ScanErrorText.Text = "";
        Task.Run(() =>
        {
            var (result, error) = ResolveMediaAuditService.ScanCurrentProject();
            Dispatcher.Invoke(() =>
            {
                ScanButton.IsEnabled = true;
                if (error != null) { ScanErrorText.Text = "⚠ " + DescribeAuditError(error); _lastResult = null; RenderFlags(); return; }
                _lastResult = result;
                _selectedForDeletion.Clear();
                ScanResultText.Text = $"„{result!.ProjectName}” — {result.TotalClips} clipuri, {result.Flags.Count} semnalate";
                RenderFlags();
            });
        });
    }

    private void RenderFlags()
    {
        FlagsList.Items.Clear();
        if (_lastResult is null) { DeleteSelectedButton.IsEnabled = false; return; }
        foreach (var flag in _lastResult.Flags)
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var check = new CheckBox { Content = flag.ClipName };
            check.Checked += (_, _) => { _selectedForDeletion.Add(flag.FilePath); DeleteSelectedButton.IsEnabled = _selectedForDeletion.Count > 0; };
            check.Unchecked += (_, _) => { _selectedForDeletion.Remove(flag.FilePath); DeleteSelectedButton.IsEnabled = _selectedForDeletion.Count > 0; };
            Grid.SetColumn(check, 0);
            row.Children.Add(check);

            var reason = new TextBlock { Text = flag.Reason, FontSize = 10.5, Foreground = flag.Reason == "Offline" ? System.Windows.Media.Brushes.OrangeRed : System.Windows.Media.Brushes.Orange };
            Grid.SetColumn(reason, 1);
            row.Children.Add(reason);
            FlagsList.Items.Add(row);
        }
    }

    private void OnDeleteSelectedClicked(object sender, RoutedEventArgs e)
    {
        var paths = _selectedForDeletion.ToList();
        Log.Clear();
        Log.Append($"$ Ștergere {paths.Count} clip(uri) din Media Pool…");
        Task.Run(() =>
        {
            var (deleted, error) = ResolveMediaAuditService.DeleteClips(paths);
            Dispatcher.Invoke(() =>
            {
                if (error != null) Log.Append("✘ " + error);
                else { Log.Append($"✔ {deleted} clip(uri) șterse din Media Pool."); OnScanClicked(this, new RoutedEventArgs()); }
            });
        });
    }

    private string DescribeAuditError(string error) => error switch
    {
        "no_scripting_access" => "DaVinci Resolve nu rulează sau scripting-ul nu e activat (Preferences → General → External scripting using = Local).",
        "no_project" => "Niciun proiect deschis în Resolve.",
        "scripting_unavailable" => "Scripting API Resolve indisponibil (python.exe nu a fost găsit în PATH).",
        _ => error,
    };

    // MARK: - Sincronizare LUT/Fusion

    private void LoadConfigFolders()
    {
        ConfigFoldersList.Items.Clear();
        foreach (var folder in ResolveConfigSyncService.Folders)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock { Text = folder.Exists ? "✔" : "✘", Foreground = folder.Exists ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 0, 8, 0) });
            row.Children.Add(new TextBlock { Text = folder.Label, FontSize = 11 });
            ConfigFoldersList.Items.Add(row);
        }
    }

    private void LoadRemotes()
    {
        _cloud = new CloudManagerService();
        _cloud.RefreshRemotes();
        RemoteCombo.ItemsSource = _cloud.Remotes.Select(r => r.Name).ToList();
        if (RemoteCombo.Items.Count > 0) RemoteCombo.SelectedIndex = 0;
    }

    private void OnSyncClicked(object sender, RoutedEventArgs e)
    {
        if (_cloud is null || RemoteCombo.SelectedItem is not string remoteName) return;
        var direction = UploadRadio.IsChecked == true ? SyncDirection.Upload : SyncDirection.Download;
        var folders = ResolveConfigSyncService.Folders.Where(f => f.Exists).ToList();
        if (folders.Count == 0) { Log.Append("ℹ Niciun folder de configurare Resolve găsit local."); return; }
        SyncButton.IsEnabled = false;
        var remaining = folders.Count;
        foreach (var folder in folders)
        {
            var remotePath = "MacMasterControlPro-ResolveConfig/" + System.IO.Path.GetFileName(folder.Path);
            _cloud.SyncFolder(folder.Path, remoteName, remotePath, direction, mirror: false,
                log: line => Dispatcher.Invoke(() => Log.Append(line)),
                completion: _ => Dispatcher.Invoke(() => { if (--remaining <= 0) SyncButton.IsEnabled = true; }));
        }
    }
}
