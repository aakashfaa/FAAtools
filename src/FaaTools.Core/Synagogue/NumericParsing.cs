using System.Globalization;

namespace FaaTools.Core.Synagogue;

/// <summary>Ports to_float: tolerant numeric parsing for Excel cell values (commas, %, parens-as-negative).</summary>
public static class NumericParsing
{
    public static double? ToFloat(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case double d:
                return d;
            case int i:
                return i;
            case float f:
                return f;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        text = text.Replace(",", string.Empty).Replace("%", string.Empty);
        if (text.StartsWith('(') && text.EndsWith(')'))
        {
            text = "-" + text[1..^1];
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }
}
