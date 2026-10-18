using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Client.Services;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Pages;

public partial class SettingsPage : UserControl
{
    private readonly MainWindow? _mainWindow;
    private bool _loaded;

    public SettingsPage(MainWindow? mainWindow = null)
    {
        InitializeComponent();
        _mainWindow = mainWindow;

        RenderLabels();

        ThemeCombo.SelectedItem = ThemeCombo.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(i => (string)i.Tag == ThemeManager.Current.ToString()) ?? ThemeCombo.Items[0];

        var scale = TextScaleStore.Load();
        TextScaleCombo.SelectedItem = TextScaleCombo.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(i => (string)i.Tag == scale.ToString()) ?? TextScaleCombo.Items[1];

        LanguageCombo.SelectedItem = LanguageCombo.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(i => (string)i.Tag == LanguageStore.Current.ToString()) ?? LanguageCombo.Items[0];

        NameBox.Text = UserProfileStore.Name;
        EmailBox.Text = UserProfileStore.Email;
        _loaded = true;
    }

    private void RenderLabels()
    {
        TitleText.Text = "⚙️ " + L.T("sidebar.settings");
        AppearanceCard.Header = L.T("settings.appearance");
        ThemeLabel.Text = L.T("settings.theme");
        TextScaleLabel.Text = L.T("settings.textSize");
        LanguageLabel.Text = L.T("settings.language");
        ProfileCard.Header = L.T("settings.profile");
        NameBox.PlaceholderText = L.T("settings.name");
        EmailBox.PlaceholderText = L.T("settings.email");
        GuideButton.Content = L.T("settings.guide");
        DiagCard.Header = L.T("diag.title");
        DiagSession.Text = L.T("diag.session") + ": " + MacMasterControlPro.Core.Services.DiagnosticLog.SessionId;
        DiagOpenButton.Content = L.T("diag.open");
        DiagCopyButton.Content = L.T("diag.copy");
        DiagExportButton.Content = L.T("diag.export");
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || ThemeCombo.SelectedItem is not ComboBoxItem item) return;
        if (Enum.TryParse<AppTheme>((string)item.Tag, out var theme))
        {
            ThemeManager.Set(theme);
        }
    }

    private void OnTextScaleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || TextScaleCombo.SelectedItem is not ComboBoxItem item) return;
        if (Enum.TryParse<TextScalePreference>((string)item.Tag, out var preference))
        {
            TextScaleStore.Save(preference);
            _mainWindow?.ApplyTextScale(preference);
        }
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || LanguageCombo.SelectedItem is not ComboBoxItem item) return;
        if (Enum.TryParse<AppLanguage>((string)item.Tag, out var lang))
        {
            LanguageStore.Current = lang;
            RenderLabels();
        }
    }

    private void OnProfileChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        UserProfileStore.Name = NameBox.Text;
        UserProfileStore.Email = EmailBox.Text;
        _mainWindow?.RefreshProfile();
    }

    private void OnDiagOpenFolder(object sender, RoutedEventArgs e)
    {
        System.IO.Directory.CreateDirectory(MacMasterControlPro.Core.Services.DiagnosticLog.Directory_);
        System.Diagnostics.Process.Start("explorer.exe", "\"" + MacMasterControlPro.Core.Services.DiagnosticLog.Directory_ + "\"");
    }

    private void OnDiagCopySession(object sender, RoutedEventArgs e) =>
        Clipboard.SetText(MacMasterControlPro.Core.Services.DiagnosticLog.SessionId);

    private void OnDiagExport(object sender, RoutedEventArgs e)
    {
        var items = MacMasterControlPro.Core.Services.DiagnosticExport.Preview();
        var list = string.Join("\n", items.Select(i => $"{i.Name}  ({i.Bytes / 1024.0:0.#} KB)"));
        if (MessageBox.Show(L.T("diag.previewIntro") + "\n\n" + list, L.T("diag.export"), MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        var dlg = new Microsoft.Win32.OpenFolderDialog();
        if (dlg.ShowDialog() != true) return;
        try
        {
            var zip = MacMasterControlPro.Core.Services.DiagnosticExport.Export(dlg.FolderName);
            System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + zip + "\"");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    private void OnOpenGuideClicked(object sender, RoutedEventArgs e)
    {
        var suffix = LanguageStore.Current switch
        {
            AppLanguage.Es => "ES",
            AppLanguage.En => "EN",
            _ => "RO",
        };
        var path = Path.Combine(AppContext.BaseDirectory, $"Instructiuni_Utilizare_{suffix}.pdf");
        if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, "Instructiuni_Utilizare_RO.pdf");
        if (File.Exists(path))
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
