using System.Windows.Media;

namespace FaaTools.Core.Synagogue.Rendering;

/// <summary>Ports color_from_hex / brush_from_hex from script.py.</summary>
public static class WpfColorHelper
{
    public static Color ColorFromHex(string? hex)
    {
        var clean = (hex ?? "#000000").TrimStart('#');
        if (clean.Length != 6)
        {
            clean = "000000";
        }

        var r = Convert.ToByte(clean[..2], 16);
        var g = Convert.ToByte(clean.Substring(2, 2), 16);
        var b = Convert.ToByte(clean.Substring(4, 2), 16);
        return Color.FromRgb(r, g, b);
    }

    public static SolidColorBrush BrushFromHex(string? hex) => new(ColorFromHex(hex));
}
