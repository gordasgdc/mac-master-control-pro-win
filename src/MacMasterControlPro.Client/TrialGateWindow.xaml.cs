using System.Diagnostics;
using System.Windows;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client;

public partial class TrialGateWindow : Window
{
    public TrialGateWindow()
    {
        InitializeComponent();
        TitleText.Text = L.T("trial.title");
        BodyText.Text = L.T("trial.body");
        KeyBox.PlaceholderText = L.T("trial.key");
        DonateButton.Content = L.T("trial.donate");
        ActivateButton.Content = L.T("trial.activate");
        CancelButton.Content = L.T("trial.cancel");
        MachineIdButton.Content = $"Machine ID: {MachineID.Display} (copiază)";
    }

    private void OnCopyMachineId(object sender, RoutedEventArgs e) => Clipboard.SetText(MachineID.Display);

    private void OnDonateClicked(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://gordas.dev/mac-master-control-pro") { UseShellExecute = true });

    private void OnWhatsAppClicked(object sender, RoutedEventArgs e)
    {
        var message = $"Salut! Doresc să achiziționez / activez licența Lifetime (9 EUR) pentru PC-ul meu — Master Control Studio Pro. Machine ID: {MachineID.Display}";
        Process.Start(new ProcessStartInfo(WhatsAppLink.Url(message)) { UseShellExecute = true });
    }

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
