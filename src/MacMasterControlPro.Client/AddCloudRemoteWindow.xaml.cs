using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client;

public partial class AddCloudRemoteWindow : Window
{
    private readonly CloudManagerService _service;
    private readonly Dictionary<string, TextBox> _fieldBoxes = new();

    public AddCloudRemoteWindow(CloudManagerService service)
    {
        InitializeComponent();
        _service = service;
        ProviderCombo.ItemsSource = Enum.GetValues<CloudProviderType>()
            .Select(t => new ComboBoxItem { Content = t.Label(), Tag = t });
        ProviderCombo.SelectedIndex = 0;
    }

    private void OnProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProviderCombo.SelectedItem is not ComboBoxItem item || item.Tag is not CloudProviderType type) return;
        FieldsPanel.Children.Clear();
        _fieldBoxes.Clear();

        OAuthNote.Visibility = type.IsOAuth() ? Visibility.Visible : Visibility.Collapsed;
        foreach (var field in type.Fields())
        {
            var box = new Wpf.Ui.Controls.TextBox { PlaceholderText = field.Label, Margin = new Thickness(0, 0, 0, 6) };
            if (field.IsSecure) box.MaxLength = 128; // masking real ar necesita PasswordBox dedicat per camp
            FieldsPanel.Children.Add(box);
            _fieldBoxes[field.Key] = box;
        }
    }

    private void OnCreateClicked(object sender, RoutedEventArgs e)
    {
        if (ProviderCombo.SelectedItem is not ComboBoxItem item || item.Tag is not CloudProviderType type) return;
        if (string.IsNullOrWhiteSpace(NameBox.Text)) return;

        CreateButton.IsEnabled = false;
        StatusText.Text = type.IsOAuth() ? "Verifică browser-ul pentru autorizare…" : "Se creează…";

        var values = _fieldBoxes.ToDictionary(kv => kv.Key, kv => kv.Value.Text);
        var (success, output) = _service.CreateRemote(NameBox.Text.Trim(), type, values);

        CreateButton.IsEnabled = true;
        if (success)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            StatusText.Text = string.IsNullOrWhiteSpace(output) ? "Eroare la creare." : output;
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
