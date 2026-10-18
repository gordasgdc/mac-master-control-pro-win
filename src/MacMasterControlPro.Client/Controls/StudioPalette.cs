using System.Windows.Media;

namespace MacMasterControlPro.Client.Controls;

/// Paleta „Studio Glass”: perechi de gradient alese stabil după numele elementului (nu se schimbă la navigare).
public static class StudioPalette
{
    private static readonly (Color A, Color B)[] Hues =
    {
        (Color.FromRgb(0x7D, 0xA3, 0xFF), Color.FromRgb(0x3B, 0x5E, 0xE0)),   // cobalt
        (Color.FromRgb(0x4F, 0xD1, 0xC5), Color.FromRgb(0x0F, 0x8F, 0x8A)),   // lagună
        (Color.FromRgb(0xFF, 0xC4, 0x6B), Color.FromRgb(0xE8, 0x87, 0x1F)),   // chihlimbar
        (Color.FromRgb(0xC2, 0xA6, 0xFF), Color.FromRgb(0x7A, 0x52, 0xD8)),   // orhidee
        (Color.FromRgb(0xFF, 0x9E, 0xB5), Color.FromRgb(0xD4, 0x4A, 0x73)),   // trandafir
        (Color.FromRgb(0x8C, 0xDB, 0x8C), Color.FromRgb(0x33, 0x9E, 0x54)),   // jad
        (Color.FromRgb(0x8C, 0xCC, 0xFF), Color.FromRgb(0x26, 0x8C, 0xD9)),   // cer
        (Color.FromRgb(0xB8, 0xC2, 0xD1), Color.FromRgb(0x75, 0x82, 0x94)),   // ardezie
    };

    public static (Color A, Color B) For(string name)
    {
        uint h = 5381;
        foreach (var c in name) h = unchecked(h * 33 + c);
        return Hues[h % (uint)Hues.Length];
    }

    public static Brush Gradient(string name, double opacity = 1)
    {
        var (a, b) = For(name);
        var br = new LinearGradientBrush(a, b, 45) { Opacity = opacity };
        br.Freeze();
        return br;
    }
}
