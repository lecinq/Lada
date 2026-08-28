using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lada.Models;

namespace Lada.Windows;

// Lightweight, asset-free rain layer used by the Forecast theme. All live
// surfaces share one rendering clock; each surface only redraws at 24 fps and
// is skipped while collapsed or hidden. The condensation texture is a single
// small, seamless fractal bitmap generated once for the whole process.
public sealed class ForecastRainSurface : FrameworkElement
{
    private const int MaximumDropCount = 56;
    private const int MaximumSnowflakeCount = 340;
    private const int MaximumRainStreakCount = 220;
    private const double SnowSlope10Degrees = 0.17632698070846498;
    private static int s_nextSurfaceId;

    private static readonly ImageSource NoiseTexture = BuildNoiseTexture();
    private static readonly Brush AtmosphereBrush = Freeze(new LinearGradientBrush(
        Color.FromArgb(38, 27, 72, 105),
        Color.FromArgb(12, 4, 15, 26),
        new Point(0, 0),
        new Point(1, 1)));
    private static readonly Brush CloudGlowBrush = Freeze(new RadialGradientBrush
    {
        Center = new Point(0.24, 0.08),
        GradientOrigin = new Point(0.24, 0.08),
        RadiusX = 0.85,
        RadiusY = 0.72,
        GradientStops = new GradientStopCollection
        {
            new(Color.FromArgb(26, 144, 197, 226), 0),
            new(Color.FromArgb(8, 73, 126, 164), 0.52),
            new(Colors.Transparent, 1)
        }
    });
    private static readonly Brush DayGlowBrush = Freeze(new RadialGradientBrush
    {
        Center = new Point(0.18, 0),
        GradientOrigin = new Point(0.18, 0),
        RadiusX = 0.9,
        RadiusY = 0.78,
        GradientStops = new GradientStopCollection
        {
            new(Color.FromArgb(72, 188, 224, 239), 0),
            new(Color.FromArgb(18, 88, 154, 189), 0.5),
            new(Colors.Transparent, 1)
        }
    });
    private static readonly Brush FogVeilBrush = Freeze(new LinearGradientBrush(
        Color.FromArgb(74, 186, 205, 211),
        Color.FromArgb(20, 94, 126, 139),
        new Point(0, 0),
        new Point(1, 1)));
    private static readonly Brush StormSkyBrush = Freeze(new LinearGradientBrush(
        Color.FromArgb(184, 86, 105, 120),
        Color.FromArgb(170, 39, 58, 75),
        new Point(0, 0),
        new Point(1, 1)));
    private static readonly Brush StormFlashBrush = Freeze(new RadialGradientBrush
    {
        Center = new Point(0.28, 0.12),
        GradientOrigin = new Point(0.28, 0.12),
        RadiusX = 0.82,
        RadiusY = 0.92,
        GradientStops = new GradientStopCollection
        {
            new(Color.FromArgb(150, 225, 240, 255), 0),
            new(Color.FromArgb(54, 150, 183, 213), 0.52),
            new(Colors.Transparent, 1)
        }
    });
    // The bolt is already a fractal path. This black/white/black-equivalent
    // alpha ramp is then used as a luminance mask across its bounds, keeping
    // the energy in the centre instead of painting a flat white stroke.
    private static readonly Brush LightningLuminanceMask = Freeze(new LinearGradientBrush
    {
        StartPoint = new Point(0, 0.5),
        EndPoint = new Point(1, 0.5),
        GradientStops = new GradientStopCollection
        {
            new(Colors.Transparent, 0),
            new(Color.FromArgb(118, 255, 255, 255), 0.30),
            new(Colors.White, 0.5),
            new(Color.FromArgb(118, 255, 255, 255), 0.70),
            new(Colors.Transparent, 1)
        }
    });
    private static readonly Pen LightningHaloPen = Freeze(new Pen(
        new SolidColorBrush(Color.FromArgb(102, 139, 198, 255)), 10.5)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round,
        LineJoin = PenLineJoin.Round
    });
    private static readonly Pen LightningBodyPen = Freeze(new Pen(
        new SolidColorBrush(Color.FromArgb(236, 221, 241, 255)), 3.2)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round,
        LineJoin = PenLineJoin.Round
    });
    private static readonly Pen LightningCorePen = Freeze(new Pen(
        Brushes.White, 1.05)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round,
        LineJoin = PenLineJoin.Round
    });
    private static readonly Brush StarBrush = Freeze(new SolidColorBrush(
        Color.FromArgb(230, 222, 240, 255)));
    private static readonly Pen StarRayPen = Freeze(new Pen(
        new SolidColorBrush(Color.FromArgb(130, 197, 226, 255)), 0.45));
    private static readonly Brush SnowSkyBrush = Freeze(new LinearGradientBrush(
        Color.FromArgb(176, 22, 48, 76),
        Color.FromArgb(148, 34, 61, 91),
        new Point(0, 0),
        new Point(1, 1)));
    private static readonly Brush SnowFarBrush = Freeze(new SolidColorBrush(
        Color.FromArgb(148, 224, 239, 249)));
    private static readonly Brush SnowMidBrush = Freeze(new RadialGradientBrush
    {
        GradientOrigin = new Point(0.42, 0.38),
        GradientStops = new GradientStopCollection
        {
            new(Color.FromArgb(228, 249, 253, 255), 0),
            new(Color.FromArgb(178, 222, 239, 249), 0.46),
            new(Colors.Transparent, 1)
        }
    });
    private static readonly Brush SnowNearBrush = Freeze(new RadialGradientBrush
    {
        GradientOrigin = new Point(0.40, 0.36),
        GradientStops = new GradientStopCollection
        {
            new(Color.FromArgb(190, 250, 253, 255), 0),
            new(Color.FromArgb(112, 218, 235, 246), 0.42),
            new(Color.FromArgb(24, 185, 211, 230), 0.78),
            new(Colors.Transparent, 1)
        }
    });
    private static readonly Brush DropGlassBrush = Freeze(new LinearGradientBrush
    {
        StartPoint = new Point(0.28, 0),
        EndPoint = new Point(0.72, 1),
        GradientStops = new GradientStopCollection
        {
            new(Color.FromArgb(8, 205, 234, 247), 0),
            new(Color.FromArgb(20, 105, 161, 193), 0.62),
            new(Color.FromArgb(58, 2, 15, 27), 0.88),
            new(Color.FromArgb(88, 164, 211, 234), 1)
        }
    });
    private static readonly Brush DropCausticBrush = Freeze(new LinearGradientBrush(
        Color.FromArgb(0, 218, 242, 252),
        Color.FromArgb(88, 206, 236, 249),
        new Point(0.2, 0),
        new Point(0.8, 1)));
    private static readonly Pen DropShadowPen = Freeze(new Pen(
        new SolidColorBrush(Color.FromArgb(72, 0, 8, 16)), 1.5));
    private static readonly Pen DropRimPen = Freeze(new Pen(
        new SolidColorBrush(Color.FromArgb(48, 210, 237, 249)), 0.45));
    private static readonly Pen RainStreakFarPen = Freeze(new Pen(
        new SolidColorBrush(Color.FromArgb(74, 205, 223, 235)), 0.42)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round
    });
    private static readonly Pen RainStreakMidPen = Freeze(new Pen(
        new SolidColorBrush(Color.FromArgb(108, 213, 230, 241)), 0.72)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round
    });
    private static readonly Pen RainStreakNearPen = Freeze(new Pen(
        new SolidColorBrush(Color.FromArgb(138, 222, 237, 247)), 1.08)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round
    });

    private readonly ImageBrush _coarseNoiseBrush;
    private readonly ImageBrush _fineNoiseBrush;
    private readonly TranslateTransform _coarseNoiseTransform = new();
    private readonly TranslateTransform _fineNoiseTransform = new();
    private Brush _weatherTintBrush = Brushes.Transparent;
    private readonly List<RainDrop> _drops = new();
    private readonly List<Snowflake> _snowflakes = new();
    private readonly List<RainStreak> _rainStreaks = new();
    private readonly List<Star> _stars = new();
    private double _animationSeconds;
    private double _dropLayoutWidth;
    private double _dropLayoutHeight;
    private bool _dropLayoutInitialized;
    private ForecastWeatherProfile _profile = ForecastWeatherProfile.Fallback;
    private ForecastWeatherKind _dropLayoutKind = ForecastWeatherKind.Fallback;
    private double _dropLayoutDensity = -1;
    private double _snowLayoutWidth;
    private double _snowLayoutHeight;
    private double _snowLayoutDensity = -1;
    private double _rainStreakLayoutWidth;
    private double _rainStreakLayoutHeight;
    private double _rainStreakLayoutDensity = -1;
    private double _starLayoutWidth;
    private double _starLayoutHeight;
    private Point[]? _lightningTrunk;
    private readonly List<LightningBranch> _lightningBranches = new();
    private readonly int _surfaceId;
    private readonly Random _lightningRandom;
    private long _lightningCycle = -1;
    private long _lightningSequence;
    private double _lightningPathWidth;
    private double _lightningStartedAtSeconds = double.NegativeInfinity;
    private double _nextLightningAtSeconds = double.PositiveInfinity;
    private bool _lightningSchedulePending;
    private double _weatherStartedAtSeconds;

    public ForecastRainSurface()
    {
        _surfaceId = Interlocked.Increment(ref s_nextSurfaceId);
        _lightningRandom = new Random(unchecked(Environment.TickCount * 397 ^ _surfaceId * 7919));
        IsHitTestVisible = false;
        ClipToBounds = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);

        _coarseNoiseBrush = CreateNoiseBrush(680, 0.28, _coarseNoiseTransform);
        _fineNoiseBrush = CreateNoiseBrush(340, 0.08, _fineNoiseTransform);

        Loaded += (_, _) => ForecastRainClock.Register(this);
        Unloaded += (_, _) => ForecastRainClock.Unregister(this);
    }

    public void SetWeather(WeatherSnapshot? snapshot)
    {
        var previousKind = _profile.Kind;
        _profile = ForecastWeatherProfile.From(snapshot);
        _coarseNoiseBrush.Opacity = 0.28 * _profile.MistOpacity;
        _fineNoiseBrush.Opacity = 0.08 * _profile.MistOpacity;
        _weatherTintBrush = Freeze(new SolidColorBrush(_profile.TintColor));
        _drops.Clear();
        _snowflakes.Clear();
        _rainStreaks.Clear();
        _dropLayoutInitialized = false;
        _snowLayoutDensity = -1;
        _rainStreakLayoutDensity = -1;
        if (previousKind != _profile.Kind)
        {
            _weatherStartedAtSeconds = _animationSeconds;
            ResetLightningSchedule(_profile.Kind == ForecastWeatherKind.Storm);
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        if (_profile.Kind == ForecastWeatherKind.Snow)
            EnsureSnowLayout(width, height);
        else if (_profile.Kind is ForecastWeatherKind.Rain or ForecastWeatherKind.Storm)
        {
            EnsureRainStreakLayout(width, height);
            EnsureDropLayout(width, height);
        }
        else
            EnsureDropLayout(width, height);

        var bounds = new Rect(0, 0, width, height);
        drawingContext.PushOpacity(Math.Clamp(_profile.AtmosphereOpacity, 0, 1));
        drawingContext.DrawRectangle(AtmosphereBrush, null, bounds);
        drawingContext.Pop();
        if (_profile.Kind == ForecastWeatherKind.Snow)
            drawingContext.DrawRectangle(SnowSkyBrush, null, bounds);
        else if (_profile.Kind == ForecastWeatherKind.Storm)
            drawingContext.DrawRectangle(StormSkyBrush, null, bounds);
        if (_profile.TintOpacity > 0)
        {
            drawingContext.PushOpacity(Math.Clamp(_profile.TintOpacity, 0, 1));
            drawingContext.DrawRectangle(_weatherTintBrush, null, bounds);
            drawingContext.Pop();
        }
        if (_profile.DayGlowOpacity > 0)
        {
            drawingContext.PushOpacity(_profile.DayGlowOpacity);
            drawingContext.DrawRectangle(DayGlowBrush, null, bounds);
            drawingContext.Pop();
        }
        if (_profile.ShowStars)
            DrawStars(drawingContext, width, height);
        drawingContext.PushOpacity(Math.Clamp(_profile.CloudOpacity, 0, 1));
        drawingContext.DrawRectangle(CloudGlowBrush, null, bounds);
        drawingContext.Pop();
        drawingContext.DrawRectangle(_coarseNoiseBrush, null, bounds);
        drawingContext.DrawRectangle(_fineNoiseBrush, null, bounds);

        if (_profile.Kind == ForecastWeatherKind.Fog)
        {
            drawingContext.PushOpacity(0.34);
            drawingContext.DrawRectangle(FogVeilBrush, null, bounds);
            drawingContext.Pop();
        }

        if (_profile.Kind == ForecastWeatherKind.Snow)
        {
            DrawSnow(drawingContext, width, height);
        }
        else if (_profile.Kind is ForecastWeatherKind.Rain or ForecastWeatherKind.Storm)
        {
            DrawRainStreaks(drawingContext, width, height);
            DrawScreenDrops(drawingContext, width, height);
        }
        else
        {
            DrawScreenDrops(drawingContext, width, height);
        }

        if (_profile.LightningStrength > 0)
        {
            var stormCycle = _animationSeconds - _lightningStartedAtSeconds;
            var reveal = stormCycle < 0.68
                ? Smooth(Math.Clamp(stormCycle / 0.68, 0, 1))
                : stormCycle < 1.10 ? 1 : 0;
            var pulse = stormCycle < 0.68
                ? 0.34 + reveal * 0.66
                : stormCycle < 0.94
                    ? 1 - (stormCycle - 0.68) / 0.26 * 0.52
                    : stormCycle < 1.10
                        ? Math.Sin((stormCycle - 0.94) / 0.16 * Math.PI) * 0.62
                        : 0;
            if (pulse > 0 && reveal > 0)
            {
                EnsureLightningPath(width, height, _lightningSequence);
                var revealedGeometry = BuildLightningRevealGeometry(reveal);
                drawingContext.PushOpacity(pulse * _profile.LightningStrength * 0.18);
                drawingContext.DrawRectangle(StormFlashBrush, null, bounds);
                drawingContext.Pop();
                drawingContext.PushOpacity(pulse * _profile.LightningStrength);
                drawingContext.PushOpacityMask(LightningLuminanceMask);
                drawingContext.DrawGeometry(null, LightningHaloPen, revealedGeometry);
                drawingContext.DrawGeometry(null, LightningBodyPen, revealedGeometry);
                drawingContext.DrawGeometry(null, LightningCorePen, revealedGeometry);
                drawingContext.Pop();
                drawingContext.Pop();
            }
        }
    }

    internal void AdvanceAnimation(double seconds)
    {
        _animationSeconds = seconds;
        AdvanceLightningSchedule(seconds);
        var wind = 1 + Math.Min(_profile.WindSpeed / 45, 1.6);
        var weatherSeconds = Math.Max(0, seconds - _weatherStartedAtSeconds);
        var sway = Math.Sin(weatherSeconds * 0.19) * _profile.Turbulence;
        _coarseNoiseTransform.X = PositiveModulo(
            weatherSeconds * 0.82 * wind * _profile.CoarseMotion + sway, 680);
        _coarseNoiseTransform.Y = PositiveModulo(
            weatherSeconds * 1.05 * _profile.VerticalMotion, 680);
        _fineNoiseTransform.X = -PositiveModulo(
            weatherSeconds * 1.35 * wind * _profile.FineMotion - sway * 0.62, 340);
        _fineNoiseTransform.Y = PositiveModulo(
            weatherSeconds * 1.75 * _profile.VerticalMotion, 340);
        InvalidateVisual();
    }

    private void DrawStars(DrawingContext drawingContext, double width, double height)
    {
        EnsureStarLayout(width, height);
        foreach (var star in _stars)
        {
            var x = PositiveModulo(star.X * width + _animationSeconds * star.Drift, width);
            var y = star.Y * height;
            var twinkle = 0.50 + 0.50 * Math.Sin(_animationSeconds * star.TwinkleSpeed + star.Phase);
            drawingContext.PushOpacity(0.34 + twinkle * 0.58);
            drawingContext.DrawEllipse(StarBrush, null, new Point(x, y), star.Radius, star.Radius);
            if (star.HasRays)
            {
                var ray = star.Radius * 2.7;
                drawingContext.DrawLine(StarRayPen, new Point(x - ray, y), new Point(x + ray, y));
                drawingContext.DrawLine(StarRayPen, new Point(x, y - ray), new Point(x, y + ray));
            }
            drawingContext.Pop();
        }
    }

    private void EnsureStarLayout(double width, double height)
    {
        if (_stars.Count > 0
            && Math.Abs(width - _starLayoutWidth) < 24
            && Math.Abs(height - _starLayoutHeight) < 24)
        {
            return;
        }

        _starLayoutWidth = width;
        _starLayoutHeight = height;
        _stars.Clear();
        var count = Math.Clamp((int)(width * height / 1700), 28, 76);
        var random = new DeterministicRandom((uint)(Math.Round(width) * 733 + Math.Round(height) * 193 + 0x7F4A7C15));
        for (var i = 0; i < count; i++)
        {
            var radius = 0.38 + Math.Pow(random.NextDouble(), 2.15) * 1.35;
            _stars.Add(new Star(
                X: random.NextDouble(),
                Y: 0.04 + random.NextDouble() * 0.90,
                Radius: radius,
                Phase: random.NextDouble() * Math.PI * 2,
                TwinkleSpeed: 0.22 + random.NextDouble() * 1.15,
                Drift: 0.08 + random.NextDouble() * 0.22,
                HasRays: radius > 1.42));
        }
    }

    private void EnsureLightningPath(double width, double height, long cycle)
    {
        if (_lightningTrunk is not null && _lightningCycle == cycle)
            return;

        _lightningCycle = cycle;
        _lightningPathWidth = width;
        var seed = unchecked((uint)(cycle * 0x9E3779B9L
            + (long)_surfaceId * 0x85EBCA6BL
            + Math.Round(width) * 397
            + Math.Round(height) * 31));
        var random = new DeterministicRandom(seed);
        const int pointCount = 28;
        var points = new Point[pointCount];
        var originX = width * (0.08 + random.NextDouble() * 0.56);
        var overallDrift = width * (random.NextDouble() - 0.5) * 0.28;

        for (var i = 0; i < pointCount; i++)
        {
            var t = i / (double)(pointCount - 1);
            var broadNoise = ContourNoise(i, pointCount, 5, seed) - 0.5;
            var detailNoise = ContourNoise(i, pointCount, 14, seed ^ 0x68BC21EBu) - 0.5;
            var x = originX
                + overallDrift * t
                + broadNoise * width * 0.20
                + detailNoise * width * 0.042;
            points[i] = new Point(
                Math.Clamp(x, width * 0.035, width * 0.965),
                -6 + t * (height + 12));
        }

        _lightningTrunk = points;
        _lightningBranches.Clear();

        // Smaller noisy offshoots share the same trunk, as real stepped
        // leaders do, but diverge with independent deterministic noise.
        foreach (var branchIndex in new[] { 7, 13, 19 })
        {
            var branchCount = 4 + (int)(random.NextDouble() * 3);
            var branch = new Point[branchCount];
            branch[0] = points[branchIndex];
            var direction = random.NextDouble() < 0.5 ? -1 : 1;
            for (var i = 1; i < branchCount; i++)
            {
                var progress = i / (double)(branchCount - 1);
                var jitter = (random.NextDouble() - 0.5) * width * 0.038;
                branch[i] = new Point(
                    branch[0].X + direction * width * 0.11 * progress + jitter,
                    branch[0].Y + height * 0.15 * progress);
            }
            _lightningBranches.Add(new LightningBranch(
                branchIndex / (double)(pointCount - 1),
                branchIndex,
                branch));
        }
    }

    private Geometry BuildLightningRevealGeometry(double reveal)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var strikeAge = Math.Max(0, _animationSeconds - _lightningStartedAtSeconds);
            var phase = _surfaceId * 0.731 + _lightningSequence * 1.619;
            Point[]? turbulentTrunk = null;
            if (_lightningTrunk is not null)
            {
                turbulentTrunk = ApplyLightningTurbulence(
                    _lightningTrunk,
                    strikeAge,
                    phase,
                    _lightningPathWidth * 0.0055);
                AppendPartialLightningPath(context, turbulentTrunk, reveal);
            }

            foreach (var branch in _lightningBranches)
            {
                var branchReveal = Math.Clamp(
                    (reveal - branch.StartProgress) / Math.Max(1 - branch.StartProgress, 0.01) * 1.55,
                    0,
                    1);
                var turbulentBranch = ApplyLightningTurbulence(
                    branch.Points,
                    strikeAge,
                    phase + branch.StartProgress * 8.7,
                    _lightningPathWidth * 0.0038);
                if (turbulentTrunk is not null && branch.TrunkIndex < turbulentTrunk.Length)
                    turbulentBranch[0] = turbulentTrunk[branch.TrunkIndex];
                AppendPartialLightningPath(context, turbulentBranch, branchReveal);
            }
        }
        geometry.Freeze();
        return geometry;
    }

    private static void AppendPartialLightningPath(
        StreamGeometryContext context,
        IReadOnlyList<Point> points,
        double reveal)
    {
        if (points.Count < 2 || reveal <= 0)
            return;

        var revealedSegments = Math.Clamp(reveal, 0, 1) * (points.Count - 1);
        var fullSegments = Math.Min((int)Math.Floor(revealedSegments), points.Count - 1);
        var visiblePoints = new List<Point>(fullSegments + 2) { points[0] };
        for (var index = 1; index <= fullSegments; index++)
            visiblePoints.Add(points[index]);

        if (fullSegments < points.Count - 1)
        {
            var fraction = revealedSegments - fullSegments;
            if (fraction > 0)
            {
                var start = points[fullSegments];
                var end = points[fullSegments + 1];
                visiblePoints.Add(new Point(
                    start.X + (end.X - start.X) * fraction,
                    start.Y + (end.Y - start.Y) * fraction));
            }
        }

        if (visiblePoints.Count < 2)
            return;

        // A Catmull-Rom spline converted to Bezier segments keeps the fractal
        // silhouette while removing the mechanical corners between leaders.
        const double smoothness = 0.68;
        context.BeginFigure(visiblePoints[0], false, false);
        for (var index = 0; index < visiblePoints.Count - 1; index++)
        {
            var p0 = index == 0 ? visiblePoints[index] : visiblePoints[index - 1];
            var p1 = visiblePoints[index];
            var p2 = visiblePoints[index + 1];
            var p3 = index + 2 < visiblePoints.Count ? visiblePoints[index + 2] : p2;
            var control1 = new Point(
                p1.X + (p2.X - p0.X) * smoothness / 6,
                p1.Y + (p2.Y - p0.Y) * smoothness / 6);
            var control2 = new Point(
                p2.X - (p3.X - p1.X) * smoothness / 6,
                p2.Y - (p3.Y - p1.Y) * smoothness / 6);
            context.BezierTo(control1, control2, p2, true, false);
        }
    }

    private static Point[] ApplyLightningTurbulence(
        IReadOnlyList<Point> source,
        double strikeAge,
        double phase,
        double amplitude)
    {
        var result = new Point[source.Count];
        for (var index = 0; index < source.Count; index++)
        {
            var progress = source.Count <= 1 ? 0 : index / (double)(source.Count - 1);
            var envelope = index == 0 ? 0 : 0.38 + Math.Sin(progress * Math.PI) * 0.62;
            // Three frequencies form a compact fBm-like displacement. Their
            // different temporal rates add a subtle electrical shimmer.
            var turbulence = Math.Sin(index * 1.37 + strikeAge * 15.0 + phase)
                + Math.Sin(index * 3.11 - strikeAge * 23.0 + phase * 1.83) * 0.48
                + Math.Sin(index * 6.29 + strikeAge * 34.0 + phase * 3.07) * 0.22;
            result[index] = new Point(
                source[index].X + turbulence / 1.70 * amplitude * envelope,
                source[index].Y);
        }
        return result;
    }

    private void AdvanceLightningSchedule(double seconds)
    {
        if (_profile.Kind != ForecastWeatherKind.Storm)
            return;

        if (_lightningSchedulePending)
        {
            _nextLightningAtSeconds = seconds + NextLightningDelay();
            _lightningSchedulePending = false;
            return;
        }

        if (seconds < _nextLightningAtSeconds)
            return;

        _lightningStartedAtSeconds = seconds;
        _nextLightningAtSeconds = seconds + NextLightningDelay();
        _lightningSequence++;
        _lightningTrunk = null;
        _lightningBranches.Clear();
        _lightningCycle = -1;
    }

    private double NextLightningDelay() => 6 + _lightningRandom.NextDouble() * 2;

    private void ResetLightningSchedule(bool enable)
    {
        _lightningTrunk = null;
        _lightningBranches.Clear();
        _lightningCycle = -1;
        _lightningStartedAtSeconds = double.NegativeInfinity;
        _nextLightningAtSeconds = double.PositiveInfinity;
        _lightningSchedulePending = enable;
    }

    private void DrawScreenDrops(DrawingContext drawingContext, double width, double height)
    {
        foreach (var drop in _drops)
        {
            var travel = height + drop.Radius * 2 + 28;
            var fallSpeed = drop.Speed * _profile.DropSpeed;
            var y = PositiveModulo(drop.StartY * travel + _animationSeconds * fallSpeed, travel)
                - drop.Radius;
            var windDrift = 1 + Math.Min(_profile.WindSpeed / 32, 1.8);
            var x = drop.X * width
                + Math.Sin(_animationSeconds * drop.WobbleSpeed + drop.Phase)
                * drop.HorizontalDrift * windDrift;

            drop.MotionTransform.X = x;
            drop.MotionTransform.Y = y;
            drawingContext.PushTransform(drop.MotionTransform);
            drawingContext.PushOpacity(drop.Opacity);

            // These beads live on the foreground "glass", above the rain
            // streaks in the scene. Their noise-built contours stay free of
            // wakes/rivulets, as in the earlier Forecast treatment.
            drawingContext.DrawGeometry(
                DropGlassBrush,
                drop.Layer == 2 ? DropShadowPen : null,
                drop.WaterGeometry);
            if (drop.Layer > 0)
                drawingContext.DrawGeometry(null, DropRimPen, drop.CausticGeometry);
            drawingContext.Pop();
            drawingContext.Pop();
        }
    }

    private void DrawRainStreaks(DrawingContext drawingContext, double width, double height)
    {
        var wind = Math.Min(_profile.WindSpeed / 55, 0.72);
        var intensitySpeed = _profile.Kind == ForecastWeatherKind.Storm
            ? 1.0
            : Math.Clamp(_profile.DropSpeed * 0.78, 0.68, 1.20);
        foreach (var streak in _rainStreaks)
        {
            var travel = height + streak.Length + 18;
            var y = PositiveModulo(
                    streak.StartY * travel + _animationSeconds * streak.Speed * intensitySpeed,
                    travel)
                - streak.Length;
            var x = streak.X * width
                + Math.Sin(_animationSeconds * streak.SwaySpeed + streak.Phase)
                * streak.Sway;
            var slant = -streak.Length * (0.025 + wind * 0.10);
            var pen = streak.Layer switch
            {
                0 => RainStreakFarPen,
                1 => RainStreakMidPen,
                _ => RainStreakNearPen
            };

            drawingContext.PushOpacity(streak.Opacity);
            drawingContext.DrawLine(
                pen,
                new Point(x, y),
                new Point(x + slant, y + streak.Length));
            drawingContext.Pop();
        }
    }

    private void EnsureRainStreakLayout(double width, double height)
    {
        if (_rainStreaks.Count > 0
            && Math.Abs(width - _rainStreakLayoutWidth) < 24
            && Math.Abs(height - _rainStreakLayoutHeight) < 24
            && Math.Abs(_rainStreakLayoutDensity - _profile.DropDensity) < 0.05)
        {
            return;
        }

        _rainStreakLayoutWidth = width;
        _rainStreakLayoutHeight = height;
        _rainStreakLayoutDensity = _profile.DropDensity;
        _rainStreaks.Clear();

        var baseCount = (int)(width * height / 2600) + 50;
        var targetCount = Math.Clamp(
            (int)Math.Round(baseCount * _profile.DropDensity),
            72,
            MaximumRainStreakCount);
        var random = new DeterministicRandom(
            (uint)(Math.Round(width) * 487 + Math.Round(height) * 89 + 0x4D7C21B5));

        for (var index = 0; index < targetCount; index++)
        {
            var depth = Math.Pow(random.NextDouble(), 1.75);
            var layer = depth < 0.28 ? 0 : depth < 0.70 ? 1 : 2;
            _rainStreaks.Add(new RainStreak(
                X: 0.01 + random.NextDouble() * 0.98,
                StartY: random.NextDouble(),
                Length: 4.5 + depth * 17 + random.NextDouble() * 4,
                Speed: 145 + depth * 265 + random.NextDouble() * 70,
                Sway: 0.4 + random.NextDouble() * 2.4,
                SwaySpeed: 0.22 + random.NextDouble() * 0.52,
                Phase: random.NextDouble() * Math.PI * 2,
                Opacity: 0.46 + depth * 0.42,
                Layer: layer));
        }
    }

    private void DrawSnow(DrawingContext drawingContext, double width, double height)
    {
        var windFlutter = 1 + Math.Min(_profile.WindSpeed / 50, 0.55);
        foreach (var flake in _snowflakes)
        {
            var travel = height + flake.Radius * 2 + 24;
            var baseY = PositiveModulo(
                    flake.StartY * travel + _animationSeconds * flake.Speed,
                    travel)
                - flake.Radius;

            // Three incommensurate sine octaves form a cheap continuous
            // pseudo-noise signal. Every flake has its own phase/frequency,
            // so the field floats irregularly instead of swaying in unison.
            var turbulenceX = SnowTurbulence(
                _animationSeconds,
                flake.WobbleSpeed,
                flake.Phase);
            var turbulenceY = SnowTurbulence(
                _animationSeconds,
                flake.WobbleSpeed * 0.71,
                flake.Phase + 2.37);
            var y = baseY + turbulenceY * flake.VerticalFlutter;

            // The expanded off-screen strip on the right feeds particles
            // into the frame throughout their ten-degree down-left travel,
            // keeping the density even from top to bottom.
            var spawnWidth = width + travel * SnowSlope10Degrees;
            var x = flake.X * spawnWidth
                - (baseY + flake.Radius) * SnowSlope10Degrees
                + turbulenceX * flake.HorizontalFlutter * windFlutter;

            var brush = flake.Layer switch
            {
                0 => SnowFarBrush,
                1 => SnowMidBrush,
                _ => SnowNearBrush
            };

            var shimmer = 0.92 + 0.08 * SnowTurbulence(
                _animationSeconds,
                flake.WobbleSpeed * 0.43,
                flake.Phase + 4.91);
            drawingContext.PushOpacity(flake.Opacity * shimmer);
            drawingContext.DrawEllipse(
                brush,
                null,
                new Point(x, y),
                flake.Radius,
                flake.Radius * flake.AspectRatio);

            // Mid/near flakes are small asymmetric clusters rather than
            // pristine circles. Overlapping soft lobes break the contour
            // without allocating a unique bitmap or Effect per particle.
            if (flake.Layer > 0)
            {
                var lobeDistance = flake.Radius * flake.LobeDistance;
                var lobeX = x + Math.Cos(flake.LobeAngle) * lobeDistance;
                var lobeY = y + Math.Sin(flake.LobeAngle) * lobeDistance;
                var lobeRadius = flake.Radius * flake.LobeScale;
                drawingContext.PushOpacity(0.48);
                drawingContext.DrawEllipse(
                    brush,
                    null,
                    new Point(lobeX, lobeY),
                    lobeRadius,
                    lobeRadius * (1.12 - (flake.AspectRatio - 0.78) * 0.35));
                drawingContext.Pop();

                if (flake.Layer == 2)
                {
                    var satelliteRadius = flake.Radius * 0.24;
                    drawingContext.PushOpacity(0.28);
                    drawingContext.DrawEllipse(
                        SnowMidBrush,
                        null,
                        new Point(
                            x - Math.Cos(flake.LobeAngle) * flake.Radius * 0.46,
                            y - Math.Sin(flake.LobeAngle) * flake.Radius * 0.46),
                        satelliteRadius,
                        satelliteRadius * 0.84);
                    drawingContext.Pop();
                }
            }
            drawingContext.Pop();
        }
    }

    private static double SnowTurbulence(double seconds, double speed, double phase)
    {
        var octave1 = Math.Sin(seconds * speed + phase);
        var octave2 = Math.Sin(seconds * speed * 1.93 + phase * 2.17) * 0.48;
        var octave3 = Math.Sin(seconds * speed * 3.71 + phase * 4.03) * 0.22;
        return (octave1 + octave2 + octave3) / 1.70;
    }

    private void EnsureSnowLayout(double width, double height)
    {
        if (_snowflakes.Count > 0
            && Math.Abs(width - _snowLayoutWidth) < 24
            && Math.Abs(height - _snowLayoutHeight) < 24
            && Math.Abs(_snowLayoutDensity - _profile.DropDensity) < 0.05)
        {
            return;
        }

        _snowLayoutWidth = width;
        _snowLayoutHeight = height;
        _snowLayoutDensity = _profile.DropDensity;
        _snowflakes.Clear();

        var baseCount = (int)(width * height / 1150) + 36;
        var targetCount = Math.Clamp(
            (int)Math.Round(baseCount * _profile.DropDensity),
            54,
            MaximumSnowflakeCount);
        var random = new DeterministicRandom(
            (uint)(Math.Round(width) * 613 + Math.Round(height) * 43 + 0x71A5C3D9));

        for (var i = 0; i < targetCount; i++)
        {
            // Most flakes live in the distant sub-pixel layer; progressively
            // fewer occupy the middle and softly blurred foreground layers.
            var proximity = Math.Pow(random.NextDouble(), 2.45);
            var radius = 0.34 + proximity * 4.35;
            var layer = proximity < 0.20 ? 0 : proximity < 0.58 ? 1 : 2;
            var opacity = Math.Clamp(
                (0.48 + proximity * 0.46) * (0.76 + random.NextDouble() * 0.24),
                0.34,
                0.94);

            _snowflakes.Add(new Snowflake(
                X: random.NextDouble(),
                StartY: random.NextDouble(),
                Radius: radius,
                AspectRatio: 0.78 + random.NextDouble() * 0.30,
                Speed: 8 + proximity * 34 + random.NextDouble() * 7,
                HorizontalFlutter: 0.62 + proximity * 6.5 + random.NextDouble() * 1.15,
                VerticalFlutter: 0.28 + proximity * 2.35 + random.NextDouble() * 0.58,
                WobbleSpeed: 0.28 + random.NextDouble() * 0.86,
                Phase: random.NextDouble() * Math.PI * 2,
                Opacity: opacity,
                Layer: layer,
                LobeAngle: random.NextDouble() * Math.PI * 2,
                LobeScale: 0.31 + random.NextDouble() * 0.28,
                LobeDistance: 0.24 + random.NextDouble() * 0.32));
        }
    }

    private void EnsureDropLayout(double width, double height)
    {
        if (_dropLayoutInitialized
            && Math.Abs(width - _dropLayoutWidth) < 24
            && Math.Abs(height - _dropLayoutHeight) < 24
            && _dropLayoutKind == _profile.Kind
            && Math.Abs(_dropLayoutDensity - _profile.DropDensity) < 0.05)
        {
            return;
        }

        _dropLayoutWidth = width;
        _dropLayoutHeight = height;
        _dropLayoutInitialized = true;
        _dropLayoutKind = _profile.Kind;
        _dropLayoutDensity = _profile.DropDensity;
        _drops.Clear();

        var baseCount = Math.Clamp((int)(width * height / 7000) + 16, 18, 36);
        var targetCount = Math.Clamp((int)Math.Round(baseCount * _profile.DropDensity), 0, MaximumDropCount);
        var random = new DeterministicRandom((uint)(Math.Round(width) * 397 + Math.Round(height) * 17 + 0x5F3759DF));

        for (var i = 0; i < targetCount; i++)
        {
            // Explicit depth layers, mirroring Storm's rain system while
            // preserving the bead-without-trail identity of ordinary rain.
            // Seed the first three particles across all layers so even a
            // very light shower always contains the full depth range.
            var layerRoll = random.NextDouble();
            var layer = i < 3
                ? i
                : layerRoll < 0.52 ? 0 : layerRoll < 0.84 ? 1 : 2;
            var radius = layer switch
            {
                0 => 1.8 + random.NextDouble() * 1.25,
                1 => 3.0 + random.NextDouble() * 2.35,
                _ => 5.1 + random.NextDouble() * 3.35
            };
            var opacity = layer switch
            {
                0 => 0.34 + random.NextDouble() * 0.16,
                1 => 0.62 + random.NextDouble() * 0.17,
                _ => 0.88 + random.NextDouble() * 0.12
            };
            var speed = layer switch
            {
                0 => 3.0 + random.NextDouble() * 5.5,
                1 => 7.5 + random.NextDouble() * 9.0,
                _ => 14.0 + random.NextDouble() * 12.0
            };
            var verticalStretch = 1.02 + random.NextDouble() * 0.72;
            var taperStrength = random.NextDouble() * 0.42;
            var shapeSeed = random.NextUInt();
            var water = BuildDropletGeometry(radius, verticalStretch, taperStrength, shapeSeed);
            var caustic = BuildCausticGeometry(radius, verticalStretch, shapeSeed ^ 0x9E3779B9u);

            _drops.Add(new RainDrop(
                X: 0.025 + random.NextDouble() * 0.95,
                StartY: random.NextDouble(),
                Radius: radius,
                Speed: speed,
                HorizontalDrift: (0.18 + random.NextDouble() * 0.82) * (0.72 + layer * 0.30),
                WobbleSpeed: 0.35 + random.NextDouble() * 0.75,
                Phase: random.NextDouble() * Math.PI * 2,
                Opacity: opacity,
                Layer: layer,
                WaterGeometry: water,
                CausticGeometry: caustic));
        }
    }

    // A low-frequency contour plus two finer octaves produces a different,
    // softly asymmetric silhouette for every drop. Stretch and taper are
    // continuous per-drop parameters as well, so there are no two rigid
    // shape families hiding underneath the noise.
    private static Geometry BuildDropletGeometry(double radius, double verticalStretch, double taperStrength, uint seed)
    {
        const int pointCount = 18;
        var points = new Point[pointCount];

        for (var i = 0; i < pointCount; i++)
        {
            var angle = -Math.PI / 2 + i * Math.PI * 2 / pointCount;
            var octave1 = ContourNoise(i, pointCount, 3, seed);
            var octave2 = ContourNoise(i, pointCount, 6, seed ^ 0x68BC21EBu);
            var octave3 = ContourNoise(i, pointCount, 9, seed ^ 0x02E5BE93u);
            var variation = 0.82 + octave1 * 0.17 + octave2 * 0.075 + octave3 * 0.035;

            // Pull the top inward by a different amount for each bead and
            // let the lower half collect into a subtly heavier water mass.
            var normalizedY = Math.Sin(angle);
            var taper = normalizedY < 0
                ? 1.0 - taperStrength * -normalizedY
                : 1.0;
            var x = Math.Cos(angle) * radius * variation * taper;
            var y = normalizedY * radius * verticalStretch * variation;
            if (normalizedY > 0)
                y += radius * normalizedY * taperStrength * 0.24;
            points[i] = new Point(x, y);
        }

        return BuildClosedSmoothGeometry(points);
    }

    // A partial inner contour catches only the lower/right part of the
    // droplet, closer to a refracted edge than a painted white reflection.
    private static Geometry BuildCausticGeometry(double radius, double verticalStretch, uint seed)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var yScale = Math.Max(1.0, verticalStretch * 0.9);
            var wobble = (HashNoise(2, 7, seed) - 0.5) * radius * 0.18;
            context.BeginFigure(new Point(-radius * 0.54 + wobble, radius * 0.26 * yScale), false, false);
            context.BezierTo(
                new Point(-radius * 0.18, radius * 0.78 * yScale),
                new Point(radius * 0.38, radius * 0.82 * yScale),
                new Point(radius * 0.58 + wobble, radius * 0.22 * yScale),
                true,
                false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Geometry BuildClosedSmoothGeometry(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var firstMidpoint = Midpoint(points[^1], points[0]);
            context.BeginFigure(firstMidpoint, true, true);
            for (var i = 0; i < points.Count; i++)
            {
                var next = points[(i + 1) % points.Count];
                context.QuadraticBezierTo(points[i], Midpoint(points[i], next), true, false);
            }
        }
        geometry.Freeze();
        return geometry;
    }

    private static Point Midpoint(Point a, Point b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    private static double ContourNoise(int index, int count, int frequency, uint seed)
    {
        var position = index * frequency / (double)count;
        var cell = (int)Math.Floor(position);
        var fraction = Smooth(position - cell);
        var a = HashNoise(cell % frequency, frequency, seed);
        var b = HashNoise((cell + 1) % frequency, frequency, seed);
        return a + (b - a) * fraction;
    }


    private static ImageBrush CreateNoiseBrush(double tileSize, double opacity, Transform transform) => new(NoiseTexture)
    {
        AlignmentX = AlignmentX.Left,
        AlignmentY = AlignmentY.Top,
        Stretch = Stretch.Fill,
        TileMode = TileMode.Tile,
        ViewportUnits = BrushMappingMode.Absolute,
        Viewport = new Rect(0, 0, tileSize, tileSize),
        Opacity = opacity,
        Transform = transform
    };

    private static ImageSource BuildNoiseTexture()
    {
        const int size = 128;
        const int stride = size * 4;
        var pixels = new byte[size * stride];
        var alpha = new byte[size * size];

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                // Ordinary fBm reads as cloudy volume. The previous ridged
                // transform emphasized every contour and looked etched.
                var noise = 0.62 * PeriodicValueNoise(x, y, size, 3, 0xA511E9B3)
                    + 0.27 * PeriodicValueNoise(x, y, size, 7, 0x63D83595)
                    + 0.11 * PeriodicValueNoise(x, y, size, 13, 0xC2B2AE35);
                var cloud = Smooth(Math.Clamp((noise - 0.18) / 0.72, 0, 1));
                alpha[y * size + x] = (byte)Math.Clamp(7 + cloud * 118, 0, 255);
            }
        }

        // Three periodic box-blur passes approximate a Gaussian blur while
        // preserving a perfectly seamless tile. This work happens once for
        // the whole process, not on animation frames.
        for (var pass = 0; pass < 3; pass++)
            alpha = BlurPeriodic(alpha, size, radius: 3);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var index = y * stride + x * 4;
                pixels[index] = 235;     // B
                pixels[index + 1] = 218; // G
                pixels[index + 2] = 190; // R
                pixels[index + 3] = alpha[y * size + x];
            }
        }

        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] BlurPeriodic(byte[] source, int size, int radius)
    {
        var result = new byte[source.Length];
        var diameter = radius * 2 + 1;
        var sampleCount = diameter * diameter;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var total = 0;
                for (var offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    var sampleY = (y + offsetY + size) % size;
                    for (var offsetX = -radius; offsetX <= radius; offsetX++)
                    {
                        var sampleX = (x + offsetX + size) % size;
                        total += source[sampleY * size + sampleX];
                    }
                }
                result[y * size + x] = (byte)(total / sampleCount);
            }
        }
        return result;
    }

    private static double PeriodicValueNoise(int x, int y, int size, int cells, uint seed)
    {
        var gx = x * cells / (double)size;
        var gy = y * cells / (double)size;
        var x0 = (int)Math.Floor(gx);
        var y0 = (int)Math.Floor(gy);
        var tx = Smooth(gx - x0);
        var ty = Smooth(gy - y0);

        var n00 = HashNoise(x0 % cells, y0 % cells, seed);
        var n10 = HashNoise((x0 + 1) % cells, y0 % cells, seed);
        var n01 = HashNoise(x0 % cells, (y0 + 1) % cells, seed);
        var n11 = HashNoise((x0 + 1) % cells, (y0 + 1) % cells, seed);

        var top = n00 + (n10 - n00) * tx;
        var bottom = n01 + (n11 - n01) * tx;
        return top + (bottom - top) * ty;
    }

    private static double HashNoise(int x, int y, uint seed)
    {
        var value = unchecked((uint)x * 0x1F123BB5u ^ (uint)y * 0x5F356495u ^ seed);
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return value / (double)uint.MaxValue;
    }

    private static double Smooth(double value) => value * value * (3 - 2 * value);

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

    private readonly record struct ForecastWeatherProfile(
        ForecastWeatherKind Kind,
        double AtmosphereOpacity,
        double CloudOpacity,
        double MistOpacity,
        double DayGlowOpacity,
        double DropDensity,
        double DropSpeed,
        double WindSpeed,
        bool ShowStars,
        double LightningStrength,
        Color TintColor,
        double TintOpacity,
        double CoarseMotion,
        double FineMotion,
        double VerticalMotion,
        double Turbulence)
    {
        public static ForecastWeatherProfile Fallback { get; } = new(
            ForecastWeatherKind.Fallback, 1, 1, 1, 0, 0.82, 1, 4, false, 0,
            Color.FromRgb(18, 43, 61), 0.10, 0.48, 0.72, 0.38, 3);

        public static ForecastWeatherProfile From(WeatherSnapshot? snapshot)
        {
            if (snapshot is null)
                return Fallback;

            var cloud = Math.Clamp(snapshot.CloudCover / 100, 0, 1);
            var liquid = snapshot.Precipitation + snapshot.Rain + snapshot.Showers;
            var dayGlow = snapshot.IsDay ? 1.0 : 0.08;
            return snapshot.Kind switch
            {
                ForecastWeatherKind.Clear => new(snapshot.Kind, 0.42,
                    snapshot.IsDay ? 0.10 : 0.42,
                    snapshot.IsDay ? 0.24 : 0.66,
                    dayGlow, 0, 1, snapshot.WindSpeed, !snapshot.IsDay, 0,
                    snapshot.IsDay ? Color.FromRgb(44, 118, 154) : Color.FromRgb(4, 10, 33),
                    snapshot.IsDay ? 0.16 : 0.38,
                    snapshot.IsDay ? 0.08 : 0.22,
                    snapshot.IsDay ? 0.12 : 0.34,
                    snapshot.IsDay ? 0.04 : 0.08,
                    snapshot.IsDay ? 0.4 : 1.4),
                ForecastWeatherKind.Cloudy => new(snapshot.Kind, 0.66, 0.30 + cloud * 0.70, 0.55 + cloud * 0.48, dayGlow * 0.55, 0, 1, snapshot.WindSpeed, false, 0,
                    Color.FromRgb(55, 76, 91), 0.20, 0.52, 0.84, 0.18, 3),
                ForecastWeatherKind.Fog => new(snapshot.Kind, 0.78, 1.0, 1.65, dayGlow * 0.32, 0, 1, snapshot.WindSpeed, false, 0,
                    Color.FromRgb(126, 147, 153), 0.28, 0.18, 0.10, 0.035, 1.2),
                ForecastWeatherKind.Rain => new(snapshot.Kind, 0.94, 0.82 + cloud * 0.35, 1.05, dayGlow * 0.24,
                    Math.Clamp(0.48 + liquid * 0.34, 0.48, 1.35),
                    Math.Clamp(0.82 + liquid * 0.16, 0.82, 1.55), snapshot.WindSpeed, false, 0,
                    Color.FromRgb(19, 48, 70), 0.26, 0.86, 1.18, 0.72, 6),
                ForecastWeatherKind.Snow => new(snapshot.Kind, 0.62, 0.26 + cloud * 0.16, 0.34, dayGlow * 0.12,
                    Math.Clamp(0.58 + snapshot.Snowfall * 0.24, 0.58, 1.30), 1, snapshot.WindSpeed, false, 0,
                    Color.FromRgb(31, 61, 91), 0.34, 0.18, 0.30, 0.16, 1.4),
                ForecastWeatherKind.Storm => new(snapshot.Kind, 0.78, 1.08, 0.92, 0.08,
                    Math.Clamp(1.12 + liquid * 0.18, 1.12, 1.55), 1.55, snapshot.WindSpeed, false, 0.88,
                    Color.FromRgb(54, 72, 87), 0.30, 1.12, 1.72, 0.92, 12),
                _ => Fallback
            };
        }
    }

    private sealed record Star(
        double X,
        double Y,
        double Radius,
        double Phase,
        double TwinkleSpeed,
        double Drift,
        bool HasRays);

    private sealed record Snowflake(
        double X,
        double StartY,
        double Radius,
        double AspectRatio,
        double Speed,
        double HorizontalFlutter,
        double VerticalFlutter,
        double WobbleSpeed,
        double Phase,
        double Opacity,
        int Layer,
        double LobeAngle,
        double LobeScale,
        double LobeDistance);

    private sealed record RainStreak(
        double X,
        double StartY,
        double Length,
        double Speed,
        double Sway,
        double SwaySpeed,
        double Phase,
        double Opacity,
        int Layer);

    private sealed record LightningBranch(
        double StartProgress,
        int TrunkIndex,
        IReadOnlyList<Point> Points);

    private sealed class RainDrop
    {
        public RainDrop(
            double X,
            double StartY,
            double Radius,
            double Speed,
            double HorizontalDrift,
            double WobbleSpeed,
            double Phase,
            double Opacity,
            int Layer,
            Geometry WaterGeometry,
            Geometry CausticGeometry)
        {
            this.X = X;
            this.StartY = StartY;
            this.Radius = Radius;
            this.Speed = Speed;
            this.HorizontalDrift = HorizontalDrift;
            this.WobbleSpeed = WobbleSpeed;
            this.Phase = Phase;
            this.Opacity = Opacity;
            this.Layer = Layer;
            this.WaterGeometry = WaterGeometry;
            this.CausticGeometry = CausticGeometry;
        }

        public double X { get; }
        public double StartY { get; }
        public double Radius { get; }
        public double Speed { get; }
        public double HorizontalDrift { get; }
        public double WobbleSpeed { get; }
        public double Phase { get; }
        public double Opacity { get; }
        public int Layer { get; }
        public Geometry WaterGeometry { get; }
        public Geometry CausticGeometry { get; }
        public TranslateTransform MotionTransform { get; } = new();
    }

    private struct DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(uint seed) => _state = seed == 0 ? 1u : seed;

        public double NextDouble()
        {
            var x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x / (double)uint.MaxValue;
        }

        public uint NextUInt()
        {
            NextDouble();
            return _state;
        }
    }

    private static class ForecastRainClock
    {
        private static readonly HashSet<ForecastRainSurface> Surfaces = new();
        private static readonly Stopwatch Stopwatch = new();
        private static double _lastFrameSeconds;

        public static void Register(ForecastRainSurface surface)
        {
            if (!Surfaces.Add(surface) || Surfaces.Count != 1)
                return;

            Stopwatch.Restart();
            _lastFrameSeconds = double.NegativeInfinity;
            CompositionTarget.Rendering += OnRendering;
        }

        public static void Unregister(ForecastRainSurface surface)
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
            if (seconds - _lastFrameSeconds < 1.0 / 24.0)
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
