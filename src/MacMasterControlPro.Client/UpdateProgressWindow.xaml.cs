using System.Windows;

namespace MacMasterControlPro.Client;

public partial class UpdateProgressWindow : Window
{
    public UpdateProgressWindow(string version)
    {
        InitializeComponent();
        TitleText.Text = $"Master Control Studio Pro {version}";

        var owner = Application.Current?.MainWindow;
        if (owner is not null && owner.IsLoaded && !ReferenceEquals(owner, this))
        {
            Owner = owner;
        }
    }

    public void SetStatus(string text) => StatusText.Text = text;
}
