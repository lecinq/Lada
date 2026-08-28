using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace Lada.Windows;

// Sparse holographic instrumentation for the Howard theme. The surface is
// deliberately mostly empty: its grid, calibration marks, corner brackets
// and scan beam never add a broad opaque layer over the desktop. Like the
// Forecast renderer, every visible instance shares one capped animation
// clock and hidden/collapsed surfaces do no per-frame work.
public sealed class HowardHudSurface : FrameworkElement
{
    public static readonly DependencyProperty AccentColorProperty = DependencyProperty.Register(
        nameof(AccentColor),
        typeof(Color),
        typeof(HowardHudSurface),
        new FrameworkPropertyMetadata(
            Color.FromRgb(0x00, 0xE5, 0xFF),
            FrameworkPropertyMetadataOptions.AffectsRender,
            OnAccentColorChanged));

    private Pen _gridPen = null!;
    private Pen _minorPen = null!;
    private Pen _brightPen = null!;
    private Pen _scanPen = null!;
    private Brush _scanBrush = null!;
    private double _animationSeconds;

    public HowardHudSurface()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        RebuildDrawingResources();

        Loaded += (_, _) => HowardHudClock.Register(this);
        Unloaded += (_, _) => HowardHudClock.Unregister(this);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        DrawGrid(drawingContext, width, height);
        DrawCornerBrackets(drawingContext, width, height);
        DrawCalibrationMarks(drawingContext, width, height);

        // One faint scanning band supplies motion without turning the card
        // into a bright animation or materially increasing its background
        // opacity. It takes roughly seven seconds to cross a typical lada.
        var scanTravel = height + 44;
        var scanY = PositiveModulo(_animationSeconds * 34, scanTravel) - 22;
        drawingContext.DrawRectangle(_scanBrush, null, new Rect(0, scanY - 13, width, 26));
        drawingContext.DrawLine(_scanPen, new Point(0, scanY), new Point(width, scanY));
    }

    internal void AdvanceAnimation(double seconds)
    {
        _animationSeconds = seconds;
        InvalidateVisual();
    }

    private void DrawGrid(DrawingContext drawingContext, double width, double height)
    {
        const double majorSpacing = 36;
        const double minorSpacing = 12;

        for (var x = minorSpacing; x < width; x += minorSpacing)
        {
            var major = Math.Abs(x % majorSpacing) < 0.1;
            drawingContext.DrawLine(major ? _gridPen : _minorPen, new Point(x, 0), new Point(x, height));
        }

        for (var y = minorSpacing; y < height; y += minorSpacing)
        {
            var major = Math.Abs(y % majorSpacing) < 0.1;
            drawingContext.DrawLine(major ? _gridPen : _minorPen, new Point(0, y), new Point(width, y));
        }
    }

    private void DrawCornerBrackets(DrawingContext drawingContext, double width, double height)
    {
        const double inset = 7;
        const double length = 21;

        DrawBracket(drawingContext, new Point(inset, inset), length, 1, 1);
        DrawBracket(drawingContext, new Point(width - inset, inset), length, -1, 1);
        DrawBracket(drawingContext, new Point(inset, height - inset), length, 1, -1);
        DrawBracket(drawingContext, new Point(width - inset, height - inset), length, -1, -1);
    }

    private void DrawBracket(DrawingContext drawingContext, Point origin, double length, int horizontal, int vertical)
    {
        drawingContext.DrawLine(
            _brightPen,
            origin,
            new Point(origin.X + length * horizontal, origin.Y));
        drawingContext.DrawLine(
            _brightPen,
            origin,
            new Point(origin.X, origin.Y + length * vertical));

        // A detached secondary stroke keeps the silhouette technical and
        // angular instead of reading as a conventional rounded frame.
        var offset = 4 * vertical;
        drawingContext.DrawLine(
            _gridPen,
            new Point(origin.X + 6 * horizontal, origin.Y + offset),
            new Point(origin.X + 15 * horizontal, origin.Y + offset));
    }

    private void DrawCalibrationMarks(DrawingContext drawingContext, double width, double height)
    {
        for (var i = 1; i <= 5; i++)
        {
            var y = height * i / 6;
            var tick = i == 3 ? 8 : 4;
            drawingContext.DrawLine(_gridPen, new Point(0, y), new Point(tick, y));
            drawingContext.DrawLine(_gridPen, new Point(width - tick, y), new Point(width, y));
        }

        // Three compact telemetry bars add HUD rhythm while staying outside
        // the central content area and carrying no fake numerical data.
        var baseline = Math.Max(10, height - 11);
        for (var i = 0; i < 3; i++)
        {
            var barWidth = 8 + i * 5;
            drawingContext.DrawLine(
                i == 2 ? _brightPen : _gridPen,
                new Point(11, baseline - i * 3),
                new Point(11 + barWidth, baseline - i * 3));
        }
    }

    private static void OnAccentColorChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((HowardHudSurface)dependencyObject).RebuildDrawingResources();
    }

    private void RebuildDrawingResources()
    {
        var accent = AccentColor;
        _minorPen = Freeze(new Pen(new SolidColorBrush(WithAlpha(accent, 10)), 0.45));
        _gridPen = Freeze(new Pen(new SolidColorBrush(WithAlpha(accent, 30)), 0.65));
        _brightPen = Freeze(new Pen(new SolidColorBrush(WithAlpha(accent, 205)), 1.15));
        _scanPen = Freeze(new Pen(new SolidColorBrush(WithAlpha(accent, 70)), 0.7));

        var scanBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = new GradientStopCollection
            {
                new(WithAlpha(accent, 0), 0),
                new(WithAlpha(accent, 7), 0.42),
                new(WithAlpha(accent, 18), 0.5),
                new(WithAlpha(accent, 0), 1)
            }
        };
        _scanBrush = Freeze(scanBrush);
    }

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static double PositiveModulo(double value, double modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private static class HowardHudClock
    {
        private static readonly HashSet<HowardHudSurface> Surfaces = new();
        private static readonly Stopwatch Stopwatch = new();
        private static double _lastFrameSeconds;

        public static void Register(HowardHudSurface surface)
        {
            if (!Surfaces.Add(surface) || Surfaces.Count != 1)
                return;

            Stopwatch.Restart();
            _lastFrameSeconds = double.NegativeInfinity;
            CompositionTarget.Rendering += OnRendering;
        }

        public static void Unregister(HowardHudSurface surface)
        {
            Surfaces.Remove(surface);
            if (Surfaces.Count != 0)
                return;

            CompositionTarget.Rendering -= OnRendering;
            Stopwatch.Stop();
        }

        private static void OnRendering(object? sender, EventArgs e)
        {
            var seconds = Stopwatch.Elapsed.TotalSeconds;
            if (seconds - _lastFrameSeconds < 1.0 / 20.0)
                return;

            _lastFrameSeconds = seconds;
            foreach (var surface in Surfaces)
            {
                if (surface.IsVisible && surface.ActualWidth > 0 && surface.ActualHeight > 0)
                    surface.AdvanceAnimation(seconds);
            }
        }
    }
}
