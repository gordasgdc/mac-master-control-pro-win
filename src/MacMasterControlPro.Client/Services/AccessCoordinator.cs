using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MacMasterControlPro.Core.Services;
using MacMasterControlPro.Core.Services.Access;

namespace MacMasterControlPro.Client.Services;

/// Legătura dintre interfață și `IAccessProvider`. Măsoară „utilizarea activă”: fereastra activă ȘI (interacțiune în ultimele 5 s SAU o
/// operație lungă în curs). Aplicația lăsată deschisă, fără interacțiune, nu consumă sesiunea de probă.
public static class AccessCoordinator
{
    private static DispatcherTimer? _timer;
    private static DateTime _lastInteraction = DateTime.MinValue;
    private static int _busy;
    private static bool _wasLocked;

    /// Marchează o operație lungă (scanare etc.) ca utilizare activă cât timp rezultatul e păstrat: `using var _ = AccessCoordinator.Busy();`
    public static IDisposable Busy() { Interlocked.Increment(ref _busy); return new Releaser(); }
    private sealed class Releaser : IDisposable { private int _d; public void Dispose() { if (Interlocked.Exchange(ref _d, 1) == 0) Interlocked.Decrement(ref _busy); } }

    public static void Start()
    {
        if (_timer is not null) return;
        var m = LicenseManager.Shared;
        _wasLocked = !m.Mode.AllowsFeatures;
        if (!m.Mode.IsFull) DemoProfile.Wipe();    // în probă pornește de la zero
        InputManager.Current.PreProcessInput += (_, e) =>
        {
            if (e.StagingItem.Input is MouseButtonEventArgs or KeyEventArgs or MouseWheelEventArgs) _lastInteraction = DateTime.UtcNow;
        };
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        _ = m.Provider.RefreshAsync();
    }

    private static void Tick()
    {
        var w = Application.Current?.MainWindow;
        var active = w is { IsActive: true } && ((DateTime.UtcNow - _lastInteraction).TotalSeconds < 5 || _busy > 0);
        var m = LicenseManager.Shared;
        m.Provider.Tick(active);
        var locked = !m.Mode.AllowsFeatures;
        if (_wasLocked && !locked && !m.Mode.IsFull) DemoProfile.Wipe();   // sesiune nouă: de la zero
        if (!_wasLocked && locked) DemoProfile.Wipe();                      // sesiunea s-a încheiat
        _wasLocked = locked;
    }
}
