using System;
using System.Runtime.InteropServices;
using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace Lada.Services;

internal static class WindowsAppRuntimeInitializer
{
    private static bool _initialized;

    public static bool TryInitialize()
    {
        if (_initialized)
            return true;

        try
        {
            // Windows App SDK 2.2 stable (0xMMMMNNNN, then dot-quad min version).
            const uint majorMinorVersion = 0x00020002;
            var minimumVersion = new PackageVersion(0x0002000200000000);
            _initialized = Bootstrap.TryInitialize(
                majorMinorVersion,
                string.Empty,
                minimumVersion,
                Bootstrap.InitializeOptions.None,
                out var result);

            if (!_initialized)
            {
                Logger.LogError(
                    "Windows App SDK initialization",
                    Marshal.GetExceptionForHR(result)
                    ?? new InvalidOperationException($"HRESULT 0x{result:X8}"));
            }

            return _initialized;
        }
        catch (Exception ex)
        {
            Logger.LogError("Windows App SDK initialization", ex);
            return false;
        }
    }
}
