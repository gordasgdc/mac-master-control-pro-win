using System.Diagnostics;
using System.Windows;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client;

public partial class TrialGateWindow : Window
{
    public TrialGateWindow()
    {
        InitializeComponent();
        MachineIdButton.Content = $"Machine ID: {MachineID.Display} (copiază)";
    }

    private void OnCopyMachineId(object sender, RoutedEventArgs e) => Clipboard.SetText(MachineID.Display);

    private void OnDonateClicked(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://gordas.dev/mac-master-control-pro") { UseShellExecute = true });

    private void OnActivateClicked(object sender, RoutedEventArgs e)
    {
        if (LicenseManager.Shared.Activate(KeyBox.Text))
        {
            DialogResult = true;
            Close();
            return;
        }
        ErrorText.Text = LicenseManager.Shared.ActivationError ?? "Cheie invalidă.";
        ErrorText.Visibility = Visibility.Visible;
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
