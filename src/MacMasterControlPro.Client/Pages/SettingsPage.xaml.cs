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
