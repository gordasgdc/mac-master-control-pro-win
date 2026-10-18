using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MacMasterControlPro.Client.Services;

/// Ilustratii "Studio Glass" de prezentare (sticla, metal, lumina). Se decodeaza o singura data, pe un fir de fundal, apoi se ataseaza;
/// lipsa resursei ascunde ilustratia. Nu stau niciodata in spatele informatiei de citit si sunt ascunse de accesibilitate.
public static class StudioArt
{
    private static readonly Dictionary<string, BitmapSource> Cache = new();

    public static async Task<BitmapSource?> LoadAsync(string name)
    {
        lock (Cache) { if (Cache.TryGetValue(name, out var c)) return c; }
        var img = await Task.Run(() =>
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/Assets/{name}.jpg", UriKind.Absolute);
                var b = new BitmapImage();
                b.BeginInit(); b.UriSource = uri; b.CacheOption = BitmapCacheOption.OnLoad; b.EndInit(); b.Freeze();
                return (BitmapSource?)b;
            }
            catch { return null; }
        });
        if (img is not null) lock (Cache) Cache[name] = img;
        return img;
    }

    /// Creeaza un card de ilustratie (colturi rotunjite, umbra discreta) incarcat asincron.
    public static Border Create(string name, double width, double height)
    {
        var image = new Image { Stretch = Stretch.UniformToFill, Width = width, Height = height };
        System.Windows.Automation.AutomationProperties.SetIsOffscreenBehavior(image, System.Windows.Automation.IsOffscreenBehavior.Offscreen);
        var card = new Border
        {
            Width = width, Height = height, CornerRadius = new CornerRadius(20), Child = image, Visibility = Visibility.Collapsed,
            Clip = new RectangleGeometry(new Rect(0, 0, width, height), 20, 20),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 4, Opacity = 0.14, Direction = 270 },
        };
        System.Windows.Automation.AutomationProperties.SetIsOffscreenBehavior(card, System.Windows.Automation.IsOffscreenBehavior.Offscreen);
        _ = LoadAsync(name).ContinueWith(t => { if (t.Result is { } src) card.Dispatcher.Invoke(() => { image.Source = src; card.Visibility = Visibility.Visible; }); });
        return card;
    }
}
