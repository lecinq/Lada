using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware;

namespace Lada.Services;

// Shared by every CPU/GPU widget across every lada, so adding a second CPU
// widget doesn't double the LibreHardwareMonitor polling cost. Lazily opened:
// most users never add one of these widgets, so there's no reason to start
// reading hardware sensors before the first is created.
public sealed class HardwareMonitorService : IDisposable
{
    private Computer? _computer;
    private DispatcherTimer? _timer;

    public event Action? Updated;

    public void EnsureStarted()
    {
        if (_computer is not null)
            return;

        _computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true, IsNetworkEnabled = true };
        _computer.Open();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Refresh();
    }

    private void Refresh()
    {
        if (_computer is null)
            return;

        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(nameof(Refresh), ex);
        }

        Updated?.Invoke();
    }

    public float? GetCpuLoad() => FindSensor(HardwareType.Cpu, SensorType.Load, "Total");

    public float? GetCpuTemperature() => FindSensor(HardwareType.Cpu, SensorType.Temperature, "Package");

    // No single "CPU Total" clock sensor exists (unlike Load) -- each core
    // can run at a different frequency under turbo/throttling, so the
    // average across "CPU Core #N" sensors is the closest thing to one
    // headline number.
    public float? GetCpuFrequency()
    {
        if (_computer is null)
            return null;

        var hardware = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (hardware is null)
            return null;

        var coreClocks = hardware.Sensors
            .Where(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) && IsRealReading(SensorType.Clock, s.Value))
            .Select(s => s.Value!.Value)
            .ToList();

        return coreClocks.Count > 0 ? coreClocks.Average() : null;
    }

    public float? GetGpuFrequency(string? gpuId) => FindDeviceSensor(IsGpu, gpuId, SensorType.Clock, "Core");

    public IReadOnlyList<(string Id, string Name)> GetGpus() => GetDevices(IsGpu);

    public float? GetGpuLoad(string? gpuId) => FindDeviceSensor(IsGpu, gpuId, SensorType.Load, "Core");

    public float? GetGpuTemperature(string? gpuId) => FindDeviceSensor(IsGpu, gpuId, SensorType.Temperature, "Core");

    public IReadOnlyList<(string Id, string Name)> GetNetworkAdapters() => GetDevices(IsNetwork);

    public float? GetNetworkDownloadSpeed(string? adapterId) => FindDeviceSensor(IsNetwork, adapterId, SensorType.Throughput, "Download");

    public float? GetNetworkUploadSpeed(string? adapterId) => FindDeviceSensor(IsNetwork, adapterId, SensorType.Throughput, "Upload");

    private float? FindSensor(HardwareType hardwareType, SensorType sensorType, string preferNameContains)
    {
        if (_computer is null)
            return null;

        var hardware = _computer.Hardware.FirstOrDefault(h => h.HardwareType == hardwareType);
        return hardware is null ? null : PickSensorValue(hardware, sensorType, preferNameContains);
    }

    private IReadOnlyList<(string Id, string Name)> GetDevices(Func<HardwareType, bool> matchesType)
    {
        if (_computer is null)
            return Array.Empty<(string, string)>();

        return _computer.Hardware
            .Where(h => matchesType(h.HardwareType))
            .Select(h => (h.Identifier.ToString() ?? h.Name, h.Name))
            .ToList();
    }

    // deviceId is the user-picked one (matched by Identifier), falling back
    // to the first detected device of that type if not found -- e.g. right
    // after a config was loaded for hardware that's since changed.
    private float? FindDeviceSensor(Func<HardwareType, bool> matchesType, string? deviceId, SensorType sensorType, string preferNameContains)
    {
        if (_computer is null)
            return null;

        var hardware = _computer.Hardware.FirstOrDefault(h => matchesType(h.HardwareType) && h.Identifier.ToString() == deviceId)
            ?? _computer.Hardware.FirstOrDefault(h => matchesType(h.HardwareType));

        return hardware is null ? null : PickSensorValue(hardware, sensorType, preferNameContains);
    }

    private static float? PickSensorValue(IHardware hardware, SensorType sensorType, string preferNameContains)
    {
        var candidates = hardware.Sensors.Where(s => s.SensorType == sensorType).ToList();
        var sensor = candidates.FirstOrDefault(s => s.Name.Contains(preferNameContains, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault();

        return IsRealReading(sensorType, sensor?.Value) ? sensor!.Value : null;
    }

    // Without administrator rights, LibreHardwareMonitor's kernel driver
    // can't load, so CPU temperature/clock sensors stay registered but
    // never receive a real reading -- they report a flat 0 instead of
    // going null. 0 is a legitimate reading for Load (idle CPU/GPU) and
    // Throughput (idle network adapter), but never for Temperature or
    // Clock on hardware that's powered on and running, so it's a reliable
    // "no real data" signal for those two.
    private static bool IsRealReading(SensorType sensorType, float? value) =>
        value is { } v && (sensorType is SensorType.Load or SensorType.Throughput || v != 0);

    private static bool IsGpu(HardwareType type) =>
        type == HardwareType.GpuNvidia || type == HardwareType.GpuAmd || type == HardwareType.GpuIntel;

    private static bool IsNetwork(HardwareType type) => type == HardwareType.Network;

    public void Dispose()
    {
        _timer?.Stop();
        _computer?.Close();
    }
}
