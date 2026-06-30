using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace FaaTools.Core.Naming;

/// <summary>
/// Ports sanitize_filename and format_date_for_cell from the existing pyRevit scripts.
/// </summary>
public static partial class FileNaming
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

    public static string SanitizeFilename(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Unnamed Project";
        }

        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(InvalidChars, chars[i]) >= 0)
            {
                chars[i] = '-';
            }
        }

        var cleaned = WhitespaceRegex().Replace(new string(chars), " ").Trim().Trim('.');
        return string.IsNullOrEmpty(cleaned) ? "Unnamed Project" : cleaned;
    }

    /// <summary>
    /// Formats a date as e.g. "July 17, 2025 3:20 PM" - non-zero-padded day and hour, matching
    /// the existing scripts' output but via .NET's native custom format instead of their
    /// leading-zero string-replace workaround.
    /// </summary>
    public static string FormatDateForCell(DateTime dt)
        => dt.ToString("MMMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
