using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using Lada.Services;

namespace Lada.Windows;

public partial class LadaWindow
{
    // Horizontal field of view for the tilt camera (degrees) -- fixed; the
    // camera's distance is recomputed on every resize (UpdateTiltGeometry)
    // so the 3D quad still maps 1:1 onto the window's actual pixel size at
    // rest, regardless of how the lada is currently sized.
    private const double CameraFieldOfViewDegrees = 30;

    // Degrees of rotation once a lada's center reaches the edge of its
    // current screen's working area -- scales down linearly to 0 at screen
    // center, giving the "mounted on a HUD panel" look this setting is
    // named for.
    private const double MaxTiltDegrees = 15;

    private PerspectiveTiltManager? _perspectiveTiltManager;
    private bool _tiltHostedIn3D;

    private void InitializePerspectiveTilt(PerspectiveTiltManager perspectiveTiltManager)
    {
        _perspectiveTiltManager = perspectiveTiltManager;
        _perspectiveTiltManager.Changed += UpdatePerspectiveTilt;
        Closed += (_, _) => _perspectiveTiltManager.Changed -= UpdatePerspectiveTilt;

        // Driven off the WINDOW's own SizeChanged, not MainBorder's --
        // MainBorder's size is now something this method itself sets (see
        // below), so watching it directly would mean reading back a value
        // from before WPF's next layout pass has applied it.
        SizeChanged += (_, _) => UpdateTiltGeometry();
        LocationChanged += (_, _) => UpdatePerspectiveTilt();
        Loaded += (_, _) =>
        {
            UpdateTiltGeometry();
            UpdatePerspectiveTilt();
        };
    }

    // Pins TiltRootContent to exactly this window's own current (padded)
    // size, and MainBorder/HudGlowRing to the smaller logical size centered
    // within it (LadaWindow.xaml). Once hosted inside Viewport2DVisual3D,
    // the content is no longer stretched to fill its parent the normal
    // 2D-layout way -- without this, it instead sizes itself to its
    // CONTENT's natural size, which is what made an earlier attempt render
    // enormous and made manual resizing behave oddly. The 3D quad and
    // camera distance are then derived from this Window's own
    // ActualWidth/Height directly (not read back off TiltRootContent,
    // which wouldn't reflect the assignment below until the next layout
    // pass) so they always match what was just set, with no one-frame lag.
    private void UpdateTiltGeometry()
    {
        var width = Math.Max(ActualWidth, 1);
        var height = Math.Max(ActualHeight, 1);
        TiltRootContent.Width = width;
        TiltRootContent.Height = height;

        var logicalWidth = Math.Max(width - 2 * HudGlowMargin, 1);
        var logicalHeight = Math.Max(height - 2 * HudGlowMargin, 1);
        MainBorder.Width = logicalWidth;
        MainBorder.Height = logicalHeight;
        UpdateMainBorderClip(logicalWidth, logicalHeight);
        HudGlowRing.Width = logicalWidth;
        HudGlowRing.Height = logicalHeight;

        var halfWidth = width / 2;
        var halfHeight = height / 2;
        TiltMesh.Positions = new Point3DCollection
        {
            new Point3D(-halfWidth, -halfHeight, 0),
            new Point3D(halfWidth, -halfHeight, 0),
            new Point3D(halfWidth, halfHeight, 0),
            new Point3D(-halfWidth, halfHeight, 0)
        };

        // WPF's PerspectiveCamera.FieldOfView applies to whichever of the
        // viewport's two dimensions is currently LARGER (horizontal when
        // wider-than-tall, vertical when taller-than-wide) -- not always
        // horizontal/width. Using halfWidth unconditionally here made the
        // distance wrong for portrait-shaped ladas, which showed up as the
        // rendered content visibly zooming in/out as a resize drag crossed
        // between landscape and portrait proportions. ApplyClipSafeScale
        // below must derive its own per-axis tan-half-fov from this same
        // distance to stay consistent.
        var fovRadians = CameraFieldOfViewDegrees * Math.PI / 180.0;
        var governingHalfExtent = Math.Max(halfWidth, halfHeight);
        TiltCamera.Position = new Point3D(0, 0, governingHalfExtent / Math.Tan(fovRadians / 2));
    }

    // Horizontal offset from the current screen's center rotates around the
    // vertical (Y) axis -- yaw, tilting the lada's left/right edges into
    // depth, the way a HUD panel would lean if mounted off to one side.
    // Vertical offset rotates around the horizontal (X) axis -- pitch, the
    // same idea for top/bottom (sign flipped per a live check: the lada's
    // top should tilt away/back when it sits below screen center, not
    // toward the viewer). Physical pixels throughout (GetPhysicalBounds),
    // since this compares directly against Screen.WorkingArea -- see
    // LadaWindow.Monitor.cs for why DIPs would be wrong here on a
    // non-100%-scaled monitor.
    private void UpdatePerspectiveTilt()
    {
        var enabled = _perspectiveTiltManager is { Enabled: true } && _hwnd != IntPtr.Zero;
        if (enabled != _tiltHostedIn3D)
        {
            SetPerspectiveTiltHosting(enabled);
            _tiltHostedIn3D = enabled;
        }

        if (!enabled)
        {
            TiltRotationX.Angle = 0;
            TiltRotationY.Angle = 0;
            TiltScale.ScaleX = TiltScale.ScaleY = TiltScale.ScaleZ = 1;
            return;
        }

        var bounds = GetPhysicalBounds();
        var workingArea = Screen.FromRectangle(bounds).WorkingArea;

        var centerX = bounds.X + bounds.Width / 2.0;
        var centerY = bounds.Y + bounds.Height / 2.0;
        var screenCenterX = workingArea.Left + workingArea.Width / 2.0;
        var screenCenterY = workingArea.Top + workingArea.Height / 2.0;

        var normalizedX = Math.Clamp((centerX - screenCenterX) / (workingArea.Width / 2.0), -1, 1);
        var normalizedY = Math.Clamp((centerY - screenCenterY) / (workingArea.Height / 2.0), -1, 1);

        var angleY = -normalizedX * MaxTiltDegrees;
        var angleX = -normalizedY * MaxTiltDegrees;
        TiltRotationY.Angle = angleY;
        TiltRotationX.Angle = angleX;

        ApplyClipSafeScale(angleX, angleY);
    }

    // Moves TiltRootContent (MainBorder + HudGlowRing) between rendering as
    // plain 2D content (RootHost.Children, crisp, exact hit-testing) and
    // being hosted inside TiltQuad's Viewport2DVisual3D (required to
    // actually render the 3D tilt). Only paid while tilt is genuinely on --
    // a live A/B test confirmed hosting through Viewport3D unconditionally
    // (the original implementation) was the root cause of a blur/resize-
    // drift/hit-test-offset bug affecting every lada regardless of whether
    // this toggle was even enabled.
    private void SetPerspectiveTiltHosting(bool hostIn3D)
    {
        if (hostIn3D)
        {
            RootHost.Children.Remove(TiltRootContent);
            TiltQuad.Visual = TiltRootContent;
            TiltViewport.Visibility = Visibility.Visible;
        }
        else
        {
            TiltQuad.Visual = null;
            TiltViewport.Visibility = Visibility.Collapsed;
            RootHost.Children.Add(TiltRootContent);
        }
    }

    // A rotated quad's corners project to a larger on-screen extent than
    // the unrotated quad (the corner swinging toward the camera gets
    // perspective-magnified) -- past a certain angle those corners would
    // land outside this window's own fixed pixel bounds and get hard
    // clipped at the window edge instead of following the lean. This
    // finds, for the CURRENT angles and CURRENT window size, the largest
    // uniform shrink needed so every corner's projected position still
    // lands within the original viewport bounds, by literally reproducing
    // the camera's rotate-then-project math in code (a few fixed-point
    // iterations converge quickly since the correction itself is small at
    // the angles this feature uses) -- reused from the same idea as the
    // shear-based prototype's scale compensation, just against a real
    // perspective projection instead of an affine shear.
    private void ApplyClipSafeScale(double angleXDegrees, double angleYDegrees)
    {
        var halfWidth = Math.Max(ActualWidth, 1) / 2;
        var halfHeight = Math.Max(ActualHeight, 1) / 2;
        var tanHalfFov = Math.Tan(CameraFieldOfViewDegrees * Math.PI / 180.0 / 2);
        var governingHalfExtent = Math.Max(halfWidth, halfHeight);
        var distance = governingHalfExtent / tanHalfFov;
        // Per-axis NDC normalization derived directly from distance
        // (rather than a single tanHalfFov + aspect multiplier) so both
        // edges land at exactly +/-1 at rest regardless of which axis
        // WPF's FieldOfView actually governs -- see UpdateTiltGeometry.
        var tanHalfFovX = halfWidth / distance;
        var tanHalfFovY = halfHeight / distance;

        var thetaX = angleXDegrees * Math.PI / 180.0;
        var thetaY = angleYDegrees * Math.PI / 180.0;
        var cosX = Math.Cos(thetaX);
        var sinX = Math.Sin(thetaX);
        var cosY = Math.Cos(thetaY);
        var sinY = Math.Sin(thetaY);

        Span<double> cornerXSigns = stackalloc double[] { -1, 1, 1, -1 };
        Span<double> cornerYSigns = stackalloc double[] { -1, -1, 1, 1 };

        var scale = 1.0;
        for (var iteration = 0; iteration < 4; iteration++)
        {
            var maxAbsNdc = 0.0;
            for (var corner = 0; corner < 4; corner++)
            {
                var x = cornerXSigns[corner] * halfWidth * scale;
                var y = cornerYSigns[corner] * halfHeight * scale;

                // Matches the XAML Transform3DGroup order: Scale, then
                // RotateX (pitch), then RotateY (yaw).
                var y1 = y * cosX;
                var z1 = y * sinX;

                var x2 = x * cosY + z1 * sinY;
                var z2 = -x * sinY + z1 * cosY;
                var y2 = y1;

                var denom = distance - z2;
                var ndcX = x2 / (denom * tanHalfFovX);
                var ndcY = y2 / (denom * tanHalfFovY);

                maxAbsNdc = Math.Max(maxAbsNdc, Math.Max(Math.Abs(ndcX), Math.Abs(ndcY)));
            }

            if (maxAbsNdc <= 1.001)
                break;

            scale /= maxAbsNdc;
        }

        TiltScale.ScaleX = scale;
        TiltScale.ScaleY = scale;
        TiltScale.ScaleZ = scale;
    }
}
