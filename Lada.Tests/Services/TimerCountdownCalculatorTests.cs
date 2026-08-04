using System;
using Lada.Services;
using Xunit;

namespace Lada.Tests.Services;

public class TimerCountdownCalculatorTests
{
    [Fact]
    public void RemainingFrom_EndInFuture_ReturnsDifference()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var end = now.AddSeconds(90);

        var result = TimerCountdownCalculator.RemainingFrom(end, now);

        Assert.Equal(TimeSpan.FromSeconds(90), result);
    }

    [Fact]
    public void RemainingFrom_EndInPast_ClampsToZero()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var end = now.AddSeconds(-30);

        var result = TimerCountdownCalculator.RemainingFrom(end, now);

        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public void RemainingFrom_EndExactlyNow_ReturnsZero()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var result = TimerCountdownCalculator.RemainingFrom(now, now);

        Assert.Equal(TimeSpan.Zero, result);
    }
}
