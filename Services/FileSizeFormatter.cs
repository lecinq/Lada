using Lada.Resources;

namespace Lada.Services;

public static class FileSizeFormatter
{
    private const double Kilo = 1024;
    private const double Mega = Kilo * 1024;
    private const double Giga = Mega * 1024;

    public static string FormatBytes(long bytes)
    {
        if (bytes >= Giga)
            return $"{(bytes / Giga).ToString("0.#")} {Strings.FileSizeGigabytes}";
        if (bytes >= Mega)
            return $"{(bytes / Mega).ToString("0.#")} {Strings.FileSizeMegabytes}";
        if (bytes >= Kilo)
            return $"{(bytes / Kilo).ToString("0.#")} {Strings.FileSizeKilobytes}";

        return $"{bytes} {Strings.FileSizeBytes}";
    }
}
