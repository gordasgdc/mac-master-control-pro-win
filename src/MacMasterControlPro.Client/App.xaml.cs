using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace MacMasterControlPro.Client;

public partial class App : Application
{
    /// Accent amber/cupru (Regula 7/16) — face toate controalele Wpf.Ui
    /// sa foloseasca aceeasi paleta ca Mac/gordas.dev, fara stiluri manuale.
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Services.ThemeManager.Apply();
        ApplicationAccentColorManager.Apply(Color.FromRgb(0xD9, 0x8A, 0x3D));
    }
}
