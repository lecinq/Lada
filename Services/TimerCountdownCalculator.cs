using System;

namespace Lada.Services;

public static class TimerCountdownCalculator
{
    public static TimeSpan RemainingFrom(DateTime endUtc, DateTime nowUtc)
    {
        var remaining = endUtc - nowUtc;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }
}
