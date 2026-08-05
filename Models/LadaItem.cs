using System;

namespace Lada.Models;

public sealed class LadaItem
{
    public string Path { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Column { get; set; }
    public int Row { get; set; }
    public bool IsDrawer { get; set; }
    public bool IsClockWidget { get; set; }
    public string? TimeZoneId { get; set; }
    public bool IsDiskWidget { get; set; }
    public string? DrivePath { get; set; }
    public bool IsTimerWidget { get; set; }
    public int TimerDurationSeconds { get; set; }
    public double TimerRemainingSeconds { get; set; }
    public DateTime? TimerEndUtc { get; set; }
    public bool IsBatteryWidget { get; set; }
    public bool IsMemoryWidget { get; set; }
    public bool IsCpuWidget { get; set; }
    public bool IsGpuWidget { get; set; }
    public string? GpuIdentifier { get; set; }
    public bool IsNetworkWidget { get; set; }
    public string? NetworkAdapterIdentifier { get; set; }
    public bool ShowDetailedView { get; set; }
    public bool IsDesktopAbsorbed { get; set; }
    public int? OriginalDesktopX { get; set; }
    public int? OriginalDesktopY { get; set; }
}
