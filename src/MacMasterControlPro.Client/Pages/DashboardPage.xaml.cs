using System.Windows.Controls;

namespace MacMasterControlPro.Client.Pages;

public partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
        TitleText.Text = L.T("dashboard.title");
        TaglineText.Text = L.T("dashboard.tagline");
    }
}
