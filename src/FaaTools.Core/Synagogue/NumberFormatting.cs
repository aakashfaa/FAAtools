using System.Globalization;

namespace FaaTools.Core.Synagogue;

/// <summary>Ports format_number / format_percent from script.py.</summary>
public static class NumberFormatting
{
    public static string FormatNumber(double? value)
    {
        if (value is not { } v)
        {
            return "0";
        }

        var rounded = Math.Round(v, 2);
        return Math.Abs(rounded - Math.Round(rounded)) < 0.01
            ? rounded.ToString("N0", CultureInfo.InvariantCulture)
            : rounded.ToString("N1", CultureInfo.InvariantCulture);
    }

    public static string FormatPercent(double? value)
        => value is not { } v ? "0%" : $"{Math.Round(v, MidpointRounding.AwayFromZero):0}%";
}
