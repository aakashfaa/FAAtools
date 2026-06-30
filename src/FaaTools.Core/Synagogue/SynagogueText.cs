using System.Text.RegularExpressions;

namespace FaaTools.Core.Synagogue;

/// <summary>
/// Text normalization/classification helpers, ported verbatim from script.py:
/// normalize_spacing, normalize_key, normalize_option_label, is_first/second/third_level_marker,
/// is_space_allocation_option.
/// </summary>
public static partial class SynagogueText
{
    public static string NormalizeSpacing(string? text)
        => WhitespaceRegex().Replace((text ?? string.Empty).Trim(), " ");

    public static string NormalizeKey(string? text)
    {
        var cleaned = NormalizeSpacing(text).Replace("&", "and");
        cleaned = NonAlphaNumericRegex().Replace(cleaned, " ");
        return cleaned.Trim().ToLowerInvariant();
    }

    public static string NormalizeOptionLabel(string? text)
        => NormalizeSpacing((text ?? string.Empty).ToUpperInvariant());

    public static bool IsFirstLevelMarker(string? value)
        => Array.IndexOf(SynagogueWorkbookParser.SectionLetters, NormalizeSpacing(value).ToUpperInvariant()) >= 0;

    public static bool IsSecondLevelMarker(string? value)
        => SecondLevelMarkerRegex().IsMatch(NormalizeSpacing(value));

    public static bool IsThirdLevelMarker(string? value)
        => ThirdLevelMarkerRegex().IsMatch(NormalizeSpacing(value));

    public static bool IsSpaceAllocationOption(string? value)
    {
        var text = NormalizeOptionLabel(value);
        return text == "EXISTING" || OptionLabelRegex().IsMatch(text);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[^A-Za-z0-9]+")]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex(@"^\d+[A-Za-z]?\.$")]
    private static partial Regex SecondLevelMarkerRegex();

    [GeneratedRegex(@"^[a-zA-Z]\.$")]
    private static partial Regex ThirdLevelMarkerRegex();

    [GeneratedRegex(@"^OPTION\s+\d+$")]
    private static partial Regex OptionLabelRegex();
}
