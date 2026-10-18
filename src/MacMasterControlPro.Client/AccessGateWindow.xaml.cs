using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Core.Services;
using MacMasterControlPro.Core.Services.Access;

namespace MacMasterControlPro.Client;

/// Panou de acces: starea (probă / acces complet), oferte (dacă ediția le are), cerere de licență, câmp pentru licență.
public partial class AccessGateWindow : Window
{
    private static System.Globalization.CultureInfo Culture => new(L.X("ro-RO", "en-GB", "es-ES"));
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
                TitleText.Text = L.X("Sesiunea de probă s-a încheiat", "The trial session has ended", "La sesión de prueba ha terminado");
                BodyText.Text = string.Format(L.X(
                    "Proba durează 10 minute de utilizare activă. Următoarea sesiune este disponibilă pe {0}. Setările tale nu se șterg: în probă nu se modifică nimic din configurația ta.",
                    "The trial lasts 10 minutes of active use. The next session is available on {0}. Your settings are not erased: the trial never changes your configuration.",
                    "La prueba dura 10 minutos de uso activo. La próxima sesión estará disponible el {0}. Tus ajustes no se borran: la prueba nunca modifica tu configuración."),
                    l.Next.LocalDateTime.ToString("d MMMM yyyy, HH:mm", Culture));
                break;
            case AccessMode.DemoAvailable d:
                TitleText.Text = string.Format(L.X("Sesiune de probă: {0}:{1:00} rămase", "Trial session: {0}:{1:00} left", "Sesión de prueba: {0}:{1:00} restantes"), d.RemainingSeconds / 60, d.RemainingSeconds % 60);
                BodyText.Text = L.X("Cât timp o folosești activ, toate funcțiile sunt disponibile.", "While you use it actively, every feature is available.", "Mientras la uses activamente, todas las funciones están disponibles.");
                break;
            default:
                TitleText.Text = L.X("Acces complet", "Full access", "Acceso completo");
                BodyText.Text = L.X("Licența este activă pe acest calculator.", "The licence is active on this computer.", "La licencia está activa en este equipo.");
                break;
        }
        OffersPanel.Children.Clear();
        if (p.Offers.Count > 0)
        {
            OffersPanel.Children.Add(new TextBlock { Text = L.X("Susține proiectul printr-o donație și primești licență:", "Support the project with a donation and receive a licence:", "Apoya el proyecto con una donación y recibe una licencia:"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
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
        RequestButton.Content = L.X("Cere o licență", "Request a licence", "Solicitar una licencia");
        PageButton.Content = L.X("Deschide pagina produsului", "Open the product page", "Abrir la página del producto");
        CopyIdButton.Content = L.X("Copiază Machine ID", "Copy Machine ID", "Copiar Machine ID");
        ApplyButton.Content = L.X("Aplică", "Apply", "Aplicar"); CloseButton.Content = L.X("Închide", "Close", "Cerrar");
        KeyBox.PlaceholderText = L.X("Lipește licența primită", "Paste the licence you received", "Pega la licencia recibida");
        EditionText.Text = p.IsOfficialEdition ? L.X("Ediție oficială GDC", "Official GDC edition", "Edición oficial de GDC") : L.X("Ediție comunitară (cod sursă MIT)", "Community edition (MIT source code)", "Edición comunitaria (código fuente MIT)");
    }

    private void OnRequestClicked(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(WhatsAppLink.Url(LicenseManager.Shared.Provider.ActivationRequestText())) { UseShellExecute = true });
    private void OnPageClicked(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://gordas.dev/mac-master-control-pro/") { UseShellExecute = true });
    private void OnCopyMachineId(object sender, RoutedEventArgs e) => Clipboard.SetText(MachineID.Display);
    private async void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        var err = await LicenseManager.Shared.Provider.SubmitLicenseAsync(KeyBox.Text);
        MessageText.Text = err ?? L.X("Licență aplicată.", "Licence applied.", "Licencia aplicada.");
        if (err is null) DialogResult = true;
    }
    private void OnCloseClicked(object sender, RoutedEventArgs e) { DialogResult = LicenseManager.Shared.IsUnlocked; Close(); }
}
