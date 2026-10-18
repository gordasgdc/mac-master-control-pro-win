using System.Diagnostics;
using System.Linq;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Core.Services;
using MacMasterControlPro.Core.Services.Access;

namespace MacMasterControlPro.Client;

/// Panou de acces: starea (probă / acces complet), oferte (dacă ediția le are), cerere de licență, câmp pentru licență.
public partial class AccessGateWindow : Window
{
    private static System.Globalization.CultureInfo Culture => new(L.X("ro-RO", "en-GB", "es-ES"));
    /// Oferta aleasă (id + versiune din lista semnată). Niciodată aleasă implicit; dacă lista se schimbă și perechea nu mai e validă, se cere o nouă alegere.
    private static (string Id, int Version)? _selected;
    private string? _notice;
    private static bool CanRequest(IAccessProvider p) => !p.IsOfficialEdition || _selected is not null;

    public AccessGateWindow()
    {
        InitializeComponent();
        Render();
        LicenseManager.Shared.Changed += () => Dispatcher.Invoke(Render);
    }

    private void Render()
    {
        var p = LicenseManager.Shared.Provider;
        Title = L.X("Acces", "Access", "Acceso");
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
        var offers = p.Offers.OrderBy(o => o.SortKey).ThenBy(o => o.Id, StringComparer.Ordinal).ToList();
        if (_selected is { } sel && !offers.Any(o => o.Id == sel.Id && o.Version == sel.Version))
        {
            _selected = null;
            _notice = L.X("Lista de oferte s-a actualizat. Alege din nou oferta.", "The offer list has been updated. Please choose your offer again.", "La lista de ofertas se ha actualizado. Elige de nuevo tu oferta.");
        }
        if (offers.Count > 0)
        {
            OffersPanel.Children.Add(new TextBlock { Text = L.X("Susține proiectul printr-o donație și primești licență:", "Support the project with a donation and receive a licence:", "Apoya el proyecto con una donación y recibe una licencia:"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
            foreach (var o in offers)
            {
                var offer = o;
                var isSel = _selected is { } s0 && s0.Id == offer.Id && s0.Version == offer.Version;
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var indicator = new Grid { Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                var ring = new System.Windows.Shapes.Ellipse { StrokeThickness = 1.5 }; ring.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, isSel ? "AccentFillColorDefaultBrush" : "TextSecondaryBrush");
                indicator.Children.Add(ring);
                if (isSel) { var dot = new System.Windows.Shapes.Ellipse { Width = 10, Height = 10 }; dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "AccentFillColorDefaultBrush"); indicator.Children.Add(dot); }
                row.Children.Add(indicator);
                var title = new TextBlock { Text = offer.Title, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(title, 1); row.Children.Add(title);
                var amt = new TextBlock { Text = offer.AmountText, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(amt, 2); row.Children.Add(amt);
                var radio = new RadioButton { GroupName = "offers", IsChecked = isSel, Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 8, 10, 8), Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand,
                    Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='RadioButton'><Border Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border></ControlTemplate>") };
                System.Windows.Automation.AutomationProperties.SetName(radio, $"{offer.Title}, {offer.AmountText}");
                radio.Checked += (_, _) => { if (_selected is not { } cur || cur.Id != offer.Id || cur.Version != offer.Version) { _selected = (offer.Id, offer.Version); _notice = null; Render(); } };
                var border = new Border { Margin = new Thickness(0, 0, 0, 6), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(isSel ? 1.5 : 1), Child = radio };
                border.SetResourceReference(Border.BorderBrushProperty, isSel ? "AccentFillColorDefaultBrush" : "CardStrokeBrush");
                border.SetResourceReference(Border.BackgroundProperty, "CardBackgroundBrush");
                if (isSel) { border.Background = new SolidColorBrush(Color.FromArgb(36, 0xD0, 0x8A, 0x3C)); }
                OffersPanel.Children.Add(border);
            }
            var hint = new TextBlock { FontSize = 12, Margin = new Thickness(0, 2, 0, 0) };
            hint.SetResourceReference(TextBlock.ForegroundProperty, _notice is null ? "TextSecondaryBrush" : "StatusWarningBrush");
            hint.Text = _notice ?? (_selected is null ? L.X("Alege o ofertă", "Choose an offer", "Elige una oferta") : "");
            OffersPanel.Children.Add(hint);
        }
        else if (p.IsOfficialEdition && !p.Mode.IsFull)
        {
            OffersPanel.Children.Add(new TextBlock { Text = L.X("Ofertele nu sunt disponibile acum. Verifică conexiunea la internet și încearcă din nou.", "Offers are not available right now. Check your internet connection and try again.", "Las ofertas no están disponibles ahora. Comprueba tu conexión a internet e inténtalo de nuevo."), TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 0, 0, 6) });
        }
        RequestButton.IsEnabled = CanRequest(p); CopyRequestButton.IsEnabled = CanRequest(p);
        LicensePanel.Visibility = p.Mode.IsFull ? Visibility.Collapsed : Visibility.Visible;
        RequestButton.Content = L.X("Cere o licență", "Request a licence", "Solicitar una licencia");
        PageButton.Content = L.X("Deschide pagina produsului", "Open the product page", "Abrir la página del producto");
        CopyIdButton.Content = L.X("Copiază Machine ID", "Copy Machine ID", "Copiar Machine ID");
        CopyRequestButton.Content = L.X("Copiază cererea", "Copy request", "Copiar solicitud");
        ApplyButton.Content = L.X("Aplică", "Apply", "Aplicar"); CloseButton.Content = L.X("Închide", "Close", "Cerrar");
        KeyBox.PlaceholderText = L.X("Lipește licența primită", "Paste the licence you received", "Pega la licencia recibida");
        EditionText.Text = p.IsOfficialEdition ? L.X("Ediție oficială GDC", "Official GDC edition", "Edición oficial de GDC") : L.X("Ediție comunitară (cod sursă MIT)", "Community edition (MIT source code)", "Edición comunitaria (código fuente MIT)");
    }

    private static (string Key, string Text)? _cachedRequest;
    /// Același text pentru „Cere o licență” și „Copiază cererea”: generat o dată per ofertă aleasă și limbă (identificatorul cererii și nonce-ul nu se schimbă între cele două acțiuni).
    private static string RequestText()
    {
        var key = $"{_selected?.Id ?? "-"}|{_selected?.Version ?? 0}|{CoreL.Code}";
        if (_cachedRequest is { } c && c.Key == key) return c.Text;
        var t = LicenseManager.Shared.Provider.ActivationRequestText(_selected?.Id); _cachedRequest = (key, t); return t;
    }

    /// Deschide conversația WhatsApp cu mesajul pregătit; utilizatorul decide trimiterea. Dacă nu se poate deschide, cererea se copiază.
    private void OnRequestClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var url = WhatsAppLink.Url(RequestText());
            if (DemoHooks.WhatsAppSink is { } sink) { System.IO.File.WriteAllText(sink, url); return; }
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            try { Clipboard.SetText(RequestText()); } catch { }
            MessageText.Text = L.X("WhatsApp nu a putut fi deschis. Cererea a fost copiată: o poți trimite manual.", "WhatsApp could not be opened. The request was copied: you can send it manually.", "No se pudo abrir WhatsApp. La solicitud se ha copiado: puedes enviarla manualmente.");
        }
    }
    private void OnPageClicked(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://gordas.dev/mac-master-control-pro/") { UseShellExecute = true });
    private void OnCopyRequest(object sender, RoutedEventArgs e) { Clipboard.SetText(RequestText()); MessageText.Text = L.X("Cererea a fost copiată.", "The request was copied.", "La solicitud se ha copiado."); }
    private void OnCopyMachineId(object sender, RoutedEventArgs e) => Clipboard.SetText(DemoHooks.DisplayMachineId);
    private async void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        var err = await LicenseManager.Shared.Provider.SubmitLicenseAsync(KeyBox.Text);
        MessageText.Text = err ?? L.X("Licență aplicată.", "Licence applied.", "Licencia aplicada.");
        if (err is null) DialogResult = true;
    }
    private void OnCloseClicked(object sender, RoutedEventArgs e) { DialogResult = LicenseManager.Shared.IsUnlocked; Close(); }
}
