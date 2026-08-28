using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Lada.Models;
using Lada.Native;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    private BackgroundBlurManager? _backgroundBlurManager;
    private AcrylicBackdropHost? _acrylicBackdrop;
    private DispatcherTimer? _backgroundBlurStartupRetryTimer;

    private void InitializeBackgroundBlur(BackgroundBlurManager manager)
    {
        _backgroundBlurManager = manager;
        _backgroundBlurManager.Changed += UpdateBackgroundBlur;
        _perspectiveTiltManager!.Changed += UpdateBackgroundBlur;

        SourceInitialized += (_, _) => UpdateBackgroundBlur();
        Loaded += (_, _) => UpdateBackgroundBlur();
        LocationChanged += (_, _) => UpdateBackgroundBlurBoundsAndPairing();
        SizeChanged += (_, _) => UpdateBackgroundBlurBounds();
        IsVisibleChanged += (_, _) => UpdateBackgroundBlur();
        Activated += (_, _) => ReassertBackdropPairing();
        Closed += (_, _) =>
        {
            _backgroundBlurManager.Changed -= UpdateBackgroundBlur;
            _perspectiveTiltManager.Changed -= UpdateBackgroundBlur;
            _backgroundBlurStartupRetryTimer?.Stop();
            _backgroundBlurStartupRetryTimer = null;
            _acrylicBackdrop?.Dispose();
            _acrylicBackdrop = null;
        };
    }

    // A newly shown HWND can reach Loaded before DesktopAcrylicController
    // has left its transient fallback state. Existing windows usually get a
    // later StateChanged callback, but that transition is not guaranteed to
    // arrive after our subscription on every controller instance. Recheck
    // briefly after Show() so every newly-created Lada joins the already
    // enabled global blur state instead of remaining on the opaque fallback.
    public void EnsureBackgroundBlurAfterShow()
    {
        UpdateBackgroundBlur();

        if (_backgroundBlurManager is not { Enabled: true }
            || _perspectiveTiltManager is { Enabled: true }
            || _acrylicBackdrop is { HasLiveMaterial: true })
        {
            return;
        }

        _backgroundBlurStartupRetryTimer?.Stop();
        var attemptsRemaining = 8;
        _backgroundBlurStartupRetryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(125)
        };
        _backgroundBlurStartupRetryTimer.Tick += (_, _) =>
        {
            UpdateBackgroundBlur();
            attemptsRemaining--;
            if (_acrylicBackdrop is { HasLiveMaterial: true } || attemptsRemaining <= 0)
            {
                _backgroundBlurStartupRetryTimer?.Stop();
                _backgroundBlurStartupRetryTimer = null;
            }
        };
        _backgroundBlurStartupRetryTimer.Start();
    }

    private void UpdateBackgroundBlur() => UpdateBackgroundBlur(preserveZOrder: false);

    // Theme/accent changes only need to repaint the acrylic tint. Reapplying
    // SetWindowPos while a WPF Popup is open can reorder the popup's separate
    // HWND behind its own Lada, so callers handling a live color-picker edit
    // explicitly preserve the existing native Z order.
    private void UpdateBackgroundBlur(bool preserveZOrder)
    {
        var requested = _backgroundBlurManager is { Enabled: true };
        var perspectiveActive = _perspectiveTiltManager is { Enabled: true };
        var shouldShow = requested
            && !perspectiveActive
            && IsVisible
            && _hwnd != IntPtr.Zero
            && _themeManager is not null;

        if (!shouldShow)
        {
            _acrylicBackdrop?.Hide();
            MainBorder.SetResourceReference(Border.BackgroundProperty, "LadaBackgroundBrush");
            return;
        }

        if (_acrylicBackdrop is null)
        {
            _acrylicBackdrop = new AcrylicBackdropHost();
            _acrylicBackdrop.MaterialStateChanged += () =>
                Dispatcher.BeginInvoke(new Action(UpdateBackgroundBlur));
        }
        var bounds = GetPhysicalBounds();
        if (!_acrylicBackdrop.EnsureCreated(bounds, _themeManager!.Current))
        {
            MainBorder.SetResourceReference(Border.BackgroundProperty, "LadaBackgroundBrush");
            return;
        }

        _acrylicBackdrop.UpdateTheme(_themeManager.Current);
        _acrylicBackdrop.UpdateBounds(bounds);
        if (!_acrylicBackdrop.HasLiveMaterial)
        {
            // Never replace the user's transparency with Acrylic's opaque
            // fallback (for example when Windows disables transparency).
            _acrylicBackdrop.Hide();
            MainBorder.SetResourceReference(Border.BackgroundProperty, "LadaBackgroundBrush");
            return;
        }
        MainBorder.Background = BuildAcrylicTint();
        _acrylicBackdrop.Show();
        if (!preserveZOrder)
            PlaceBackdropForCurrentZOrder();
    }

    private void UpdateBackgroundBlurBounds()
    {
        if (_acrylicBackdrop is not { IsAvailable: true }
            || _backgroundBlurManager is not { Enabled: true }
            || _perspectiveTiltManager is { Enabled: true }
            || !IsVisible
            || _hwnd == IntPtr.Zero)
        {
            return;
        }

        _acrylicBackdrop.UpdateBounds(GetPhysicalBounds());
    }

    private void UpdateBackgroundBlurBoundsAndPairing()
    {
        UpdateBackgroundBlurBounds();

        // DragMove can reorder the WPF HWND while its separately composed
        // Acrylic HWND stays at the old depth. Keep the two adjacent during
        // the move so another lada's foreground cannot slip between this
        // lada's background and border.
        ReassertBackdropPairing();
    }

    private void ReassertBackdropPairing()
    {
        if (_acrylicBackdrop is { IsAvailable: true }
            && _backgroundBlurManager is { Enabled: true }
            && _perspectiveTiltManager is not { Enabled: true }
            && IsVisible)
        {
            PlaceBackdropDirectlyBehindLada();
        }
    }

    private Brush BuildAcrylicTint()
    {
        if (TryFindResource("LadaBackgroundBrush") is not SolidColorBrush themeBrush)
            return Brushes.Transparent;

        var color = themeBrush.Color;
        // The DWM companion already supplies the frosted material. This is
        // only a colour wash, not a second opaque panel over it.
        var maximumAlpha = _themeManager?.Current == AppTheme.Modernism ? 88 : 68;
        color.A = (byte)Math.Min(color.A, maximumAlpha);
        var tint = new SolidColorBrush(color);
        tint.Freeze();
        return tint;
    }

    private void PlaceBackdropForCurrentZOrder()
    {
        if (_hwnd == IntPtr.Zero)
            return;

        if (_isOverlayMode)
        {
            // Promote the visible WPF card first, then insert its Acrylic
            // companion directly behind that exact HWND. Promoting both via
            // HWND_TOPMOST independently lets other ladas (or a late
            // material-state callback) interleave between them, exposing an
            // orphan-looking blurred rectangle in Overlay mode.
            NativeMethods.SetWindowPos(
                _hwnd,
                NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOACTIVATE);
            PlaceBackdropDirectlyBehindLada();
        }
        else
        {
            PinToBottom();
        }
    }

    private void PlaceBackdropDirectlyBehindLada()
    {
        if (_hwnd == IntPtr.Zero || _acrylicBackdrop is not { IsAvailable: true })
            return;

        NativeMethods.SetWindowPos(
            _acrylicBackdrop.Handle,
            _hwnd,
            0, 0, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOACTIVATE);
    }
}
