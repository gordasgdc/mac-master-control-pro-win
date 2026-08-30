using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Core.Services;
using Wpf.Ui.Controls;

namespace MacMasterControlPro.Client.Pages;

public partial class CloudPage : UserControl
{
    private readonly CloudManagerService _service = new();

    public CloudPage()
    {
        InitializeComponent();
        Refresh();
    }

    private void OnRescanClicked(object sender, RoutedEventArgs e) => Refresh();

    private void OnAddClicked(object sender, RoutedEventArgs e)
    {
        var window = new AddCloudRemoteWindow(_service) { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true) Refresh();
    }

    private void Refresh()
    {
        _service.RefreshRemotes();
        RemotesList.Items.Clear();
        EmptyText.Visibility = _service.Remotes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var remote in _service.Remotes)
        {
            var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoStack = new StackPanel();
            infoStack.Children.Add(new System.Windows.Controls.TextBlock { Text = remote.Name, FontWeight = FontWeights.Bold });
            infoStack.Children.Add(new System.Windows.Controls.TextBlock { Text = remote.Type, Foreground = System.Windows.Media.Brushes.Gray, FontSize = 11 });
            Grid.SetColumn(infoStack, 0);
            row.Children.Add(infoStack);

            var isMounted = _service.MountedDriveLetters.ContainsKey(remote.Name);
            var actionButton = new Wpf.Ui.Controls.Button
            {
                Content = isMounted ? $"Demontează ({_service.MountedDriveLetters[remote.Name]})" : "Montează",
                Margin = new Thickness(0, 0, 8, 0),
            };
            actionButton.Click += (_, _) =>
            {
                if (isMounted) { _service.Unmount(remote.Name); Refresh(); return; }
                if (!LicenseManager.Shared.IsUnlocked)
                {
                    var gate = new TrialGateWindow { Owner = Window.GetWindow(this) };
                    if (gate.ShowDialog() != true) return;
                }
                var letter = _service.Mount(remote.Name);
                StatusText.Text = letter is not null ? $"✔ Montat pe {letter}" : "Nicio literă de disc liberă.";
                Refresh();
            };
            Grid.SetColumn(actionButton, 1);
            row.Children.Add(actionButton);

            var deleteButton = new Wpf.Ui.Controls.Button { Content = "🗑" };
            deleteButton.Click += (_, _) => { _service.DeleteRemote(remote.Name); Refresh(); };
            Grid.SetColumn(deleteButton, 2);
            row.Children.Add(deleteButton);

            RemotesList.Items.Add(row);
        }
    }
}
