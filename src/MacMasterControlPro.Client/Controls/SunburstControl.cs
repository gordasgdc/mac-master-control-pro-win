using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MacMasterControlPro.Core.Services;

namespace MacMasterControlPro.Client.Controls;

/// Hartă radială: sectoare proporționale cu mărimea reală. Inel interior = elementele folderului curent, inel exterior = conținutul lor.
/// Sectoarele sub ~0,4° se contopesc într-un singur sector „rest”, deci totalul rămâne exact. Lista de lângă hartă e alternativa accesibilă.
public sealed class SunburstControl : FrameworkElement
{
    private sealed record Arc(DiskNode? Node, int Ring, double Start, double End, string Hue);

    private DiskNode? _root;
    private List<Arc> _arcs = new();
    private HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private DiskNode? _hover;

    public event Action<DiskNode>? Tapped;
    public event Action<DiskNode>? Drilled;
    public event Action<DiskNode?>? HoverChanged;

    public void SetData(DiskNode? root, IEnumerable<string> selectedPaths)
    {
        _root = root; _selected = new HashSet<string>(selectedPaths, StringComparer.OrdinalIgnoreCase);
        _arcs = Layout(root); InvalidateVisual();
    }

    public void SetHover(DiskNode? n) { if (_hover != n) { _hover = n; InvalidateVisual(); } }

    private static List<Arc> Layout(DiskNode? root)
    {
        var list = new List<Arc>();
        if (root is null || root.UsedBytes <= 0) return list;
        double total = root.UsedBytes, a = -Math.PI / 2;
        foreach (var k in root.SortedChildren.Where(c => c.UsedBytes > 0))
        {
            var sweep = k.UsedBytes / total * 2 * Math.PI;
            list.Add(new Arc(k, 1, a, a + sweep, k.Name));
            double b = a; long rest = 0;
            foreach (var c in k.SortedChildren.Where(c => c.UsedBytes > 0))
            {
                var cs = (double)c.UsedBytes / Math.Max(k.UsedBytes, 1) * sweep;
                if (cs < 0.007) { rest += c.UsedBytes; continue; }
                list.Add(new Arc(c, 2, b, b + cs, k.Name)); b += cs;
            }
            if (rest > 0) list.Add(new Arc(null, 2, b, b + (double)rest / Math.Max(k.UsedBytes, 1) * sweep, ""));
            a += sweep;
        }
        return list;
    }

    private (double hole, double r1, double r2a, double r2b) Radii()
    {
        var R = Math.Min(ActualWidth, ActualHeight) / 2 - 10;
        return (R * 0.36, R * 0.70, R * 0.73, R);
    }

    private bool IsSel(DiskNode? n)
    {
        if (n is null) return false;
        if (_selected.Contains(n.Path)) return true;
        var cur = Path.GetDirectoryName(n.Path);
        while (!string.IsNullOrEmpty(cur)) { if (_selected.Contains(cur)) return true; cur = Path.GetDirectoryName(cur); }
        return false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));   // zonă de hit-test
        if (_root is null || ActualWidth < 40 || ActualHeight < 40) return;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        var (hole, r1, r2a, r2b) = Radii();
        var fg = (Brush)(TryFindResource("TextPrimaryBrush") ?? Brushes.Black);
        var muted = (Brush)(TryFindResource("TextSecondaryBrush") ?? Brushes.Gray);
        var bg = (Brush)(TryFindResource("WindowBackgroundBrush") ?? Brushes.White);
        foreach (var arc in _arcs)
        {
            var (ra, rb) = arc.Ring == 1 ? (hole, r1) : (r2a, r2b);
            var sel = IsSel(arc.Node);
            var mid = (arc.Start + arc.End) / 2;
            var off = sel ? 7 : 0;
            var center = new Point(c.X + Math.Cos(mid) * off, c.Y + Math.Sin(mid) * off);
            var dim = _hover is not null && arc.Node is not null && _hover != arc.Node;
            var brush = arc.Node is null ? new SolidColorBrush(Color.FromArgb(70, 128, 128, 128)) : StudioPalette.Gradient(arc.Hue, dim ? 0.45 : arc.Ring == 2 ? 0.82 : 1);
            var pen = sel ? new Pen(fg, 2.5) : arc.Node == _hover ? new Pen(Brushes.White, 2) : new Pen(bg, arc.Ring == 1 ? 2 : 1.2);
            dc.DrawGeometry(brush, pen, Sector(center, ra, rb, arc.Start, arc.End));
        }
        var n = _hover ?? _root;
        DrawCentered(dc, n.Name.Length > 22 ? n.Name[..21] + "…" : n.Name, 14, FontWeights.SemiBold, fg, c.X, c.Y - hole * 0.40, hole * 1.7);
        DrawCentered(dc, DiskNode.FormatBytes(n.UsedBytes), Math.Max(20, hole * 0.34), FontWeights.Bold, fg, c.X, c.Y - hole * 0.18, hole * 1.7);
        var caption = ReferenceEquals(n, _root) ? L.X("în acest folder", "in this folder", "en esta carpeta") : $"{(double)n.UsedBytes / Math.Max(_root.UsedBytes, 1):P0} "+L.X("din folder", "of folder", "de la carpeta");
        DrawCentered(dc, caption, 11.5, FontWeights.Normal, muted, c.X, c.Y + hole * 0.30, hole * 1.7);
    }

    private static void DrawCentered(DrawingContext dc, string text, double size, FontWeight w, Brush b, double cx, double y, double maxW)
    {
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, w, FontStretches.Normal),
            size, b, VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip) { MaxTextWidth = maxW, TextAlignment = TextAlignment.Center, Trimming = TextTrimming.CharacterEllipsis, MaxLineCount = 1 };
        dc.DrawText(ft, new Point(cx - maxW / 2, y));
    }

    private static Geometry Sector(Point c, double r0, double r1, double a0, double a1)
    {
        const double gap = 0.006;
        var s = a0 + Math.Min(gap, (a1 - a0) / 4); var e = a1 - Math.Min(gap, (a1 - a0) / 4);
        if (e <= s) e = s + 0.0005;
        Point P(double r, double t) => new(c.X + r * Math.Cos(t), c.Y + r * Math.Sin(t));
        var large = e - s > Math.PI;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(P(r1, s), true, true);
            ctx.ArcTo(P(r1, e), new Size(r1, r1), 0, large, SweepDirection.Clockwise, true, false);
            ctx.LineTo(P(r0, e), true, false);
            ctx.ArcTo(P(r0, s), new Size(r0, r0), 0, large, SweepDirection.Counterclockwise, true, false);
        }
        g.Freeze();
        return g;
    }

    private DiskNode? Hit(Point p)
    {
        if (_root is null) return null;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        var (hole, r1, r2a, r2b) = Radii();
        double dx = p.X - c.X, dy = p.Y - c.Y, r = Math.Sqrt(dx * dx + dy * dy), t = Math.Atan2(dy, dx);
        if (t < -Math.PI / 2) t += 2 * Math.PI;
        var ring = r >= hole && r <= r1 ? 1 : r >= r2a && r <= r2b ? 2 : 0;
        return ring == 0 ? null : _arcs.FirstOrDefault(a => a.Ring == ring && t >= a.Start && t < a.End)?.Node;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var n = Hit(e.GetPosition(this));
        if (n != _hover) { _hover = n; HoverChanged?.Invoke(n); InvalidateVisual(); }
        Cursor = n is null ? Cursors.Arrow : Cursors.Hand;
    }
    protected override void OnMouseLeave(MouseEventArgs e) { if (_hover is not null) { _hover = null; HoverChanged?.Invoke(null); InvalidateVisual(); } }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        var n = Hit(e.GetPosition(this)); if (n is null) return;
        if (e.ClickCount >= 2) Drilled?.Invoke(n); else Tapped?.Invoke(n);
    }
}
