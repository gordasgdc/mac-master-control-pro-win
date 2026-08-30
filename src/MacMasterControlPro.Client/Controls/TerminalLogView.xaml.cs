using System.Windows;
using System.Windows.Controls;

namespace MacMasterControlPro.Client.Controls;

/// Port 1:1 al panoului "terminal live" de pe Mac (`TerminalLogView.swift`).
/// Reutilizat in Curatare/Tweaks/Dependente/Cloud - orice operatie cu
/// comenzi externe scrie aici, linie cu linie, in timp real.
public partial class TerminalLogView : UserControl
{
    public TerminalLogView() => InitializeComponent();

    public void Clear()
    {
        LogText.Text = "";
        Root.Visibility = Visibility.Collapsed;
    }

    public void Append(string line)
    {
        Root.Visibility = Visibility.Visible;
        LogText.Text += (LogText.Text.Length > 0 ? "\n" : "") + line;
        Scroller.ScrollToEnd();
    }
}
