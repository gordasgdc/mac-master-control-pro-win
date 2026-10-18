using System.Windows.Markup;
namespace MacMasterControlPro.Client;

/// Text in trei limbi direct din XAML: `{loc:Loc Ro='…', En='…', Es='…'}`; se rezolva la incarcarea paginii, dupa limba curenta.
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public string Ro { get; set; } = "";
    public string En { get; set; } = "";
    public string Es { get; set; } = "";
    public override object ProvideValue(IServiceProvider serviceProvider) => L.X(Ro, En, Es);
}
