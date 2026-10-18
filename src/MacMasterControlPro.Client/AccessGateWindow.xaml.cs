using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Core.Services;
using MacMasterControlPro.Core.Services.Access;

namespace MacMasterControlPro.Client;

/// Panou de acces: starea (probă / acces complet), oferte (dacă ediția le are), cerere de licență, câmp pentru licență.
public partial class AccessGateWindow : Window
{
    public AccessGateWindow()
    {
        InitializeComponent();
        Render();
        LicenseManager.Shared.Changed += () => Dispatcher.Invoke(Render);
    }

    private void Render()
    {
        var p = LicenseManager.Shared.Provider;
        switch (p.Mode)
        {
            case AccessMode.DemoLocked l:
                TitleText.Text = "Sesiunea de probă s-a încheiat";
                BodyText.Text = $"Proba durează 10 minute de utilizare activă. Următoarea sesiune este disponibilă pe {l.Next.LocalDateTime:d MMMM yyyy, HH:mm}. În probă, aplicația pornește de fiecare dată de la zero: setările și cache-urile nu se păstrează.";
                break;
            case AccessMode.DemoAvailable d:
                TitleText.Text = $"Sesiune de probă: {d.RemainingSeconds / 60}:{d.RemainingSeconds % 60:00} rămase";
                BodyText.Text = "Cât timp o folosești activ, toate funcțiile sunt disponibile.";
                break;
            default:
                TitleText.Text = "Acces complet"; BodyText.Text = "Licența este activă pe acest calculator.";
                break;
        }
        OffersPanel.Children.Clear();
        if (p.Offers.Count > 0)
        {
            OffersPanel.Children.Add(new TextBlock { Text = "Susține proiectul printr-o donație și primești licență:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
            foreach (var o in p.Offers)
            {
                var g = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.Children.Add(new TextBlock { Text = o.Title });
                var amt = new TextBlock { Text = o.AmountText, FontWeight = FontWeights.SemiBold }; Grid.SetColumn(amt, 1); g.Children.Add(amt);
                OffersPanel.Children.Add(g);
            }
        }
        LicensePanel.Visibility = p.Mode.IsFull ? Visibility.Collapsed : Visibility.Visible;
        RequestButton.Content = "Cere o licență"; PageButton.Content = "Pagina produsului"; CopyIdButton.Content = "Copiază Machine ID";
        ApplyButton.Content = "Aplică"; CloseButton.Content = "Închide"; KeyBox.PlaceholderText = "Lipește licența primită";
        EditionText.Text = p.EditionLabel;
    }

    private void OnRequestClicked(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(WhatsAppLink.Url(LicenseManager.Shared.Provider.ActivationRequestText())) { UseShellExecute = true });
    private void OnPageClicked(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://gordas.dev/mac-master-control-pro/") { UseShellExecute = true });
    private void OnCopyMachineId(object sender, RoutedEventArgs e) => Clipboard.SetText(MachineID.Display);
    private async void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        var err = await LicenseManager.Shared.Provider.SubmitLicenseAsync(KeyBox.Text);
        MessageText.Text = err ?? "Licență aplicată.";
        if (err is null) DialogResult = true;
    }
    private void OnCloseClicked(object sender, RoutedEventArgs e) { DialogResult = LicenseManager.Shared.IsUnlocked; Close(); }
}
