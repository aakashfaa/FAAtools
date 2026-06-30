namespace FaaTools.Core.Synagogue.Layout;

/// <summary>Ports get_root_color_hex / get_root_count_label / get_node_count_text from script.py.</summary>
public static class ColorAndLabelCatalog
{
    public static readonly IReadOnlyDictionary<string, string> RootColorHex = new Dictionary<string, string>
    {
        ["worship"] = "#A28CB5",
        ["social"] = "#FFF4CC",
        ["education"] = "#E1ACA8",
        ["administration"] = "#CED1EF",
        ["miscellaneous"] = "#D2D2D2",
        ["outdoor"] = "#C5D9A2",
        ["default"] = "#D9DEE8",
    };

    public static string GetRootColorHex(string rootName)
    {
        var key = SynagogueText.NormalizeKey(rootName);
        if (key.Contains("worship")) return RootColorHex["worship"];
        if (key.Contains("social")) return RootColorHex["social"];
        if (key.Contains("education")) return RootColorHex["education"];
        if (key.Contains("administration")) return RootColorHex["administration"];
        if (key.Contains("outdoor")) return RootColorHex["outdoor"];
        if (key.Contains("misc") || key.Contains("service") || key.Contains("support")) return RootColorHex["miscellaneous"];
        return RootColorHex["default"];
    }

    public static string GetRootCountLabel(string rootName)
    {
        var key = SynagogueText.NormalizeKey(rootName);
        if (key.Contains("worship")) return "Seats";
        if (key.Contains("social")) return "Meeting Rooms";
        if (key.Contains("education")) return "Classrooms";
        return "Occupants";
    }

    public static string? GetNodeCountText(double occupants, int level, string rootName)
    {
        if (occupants <= 0)
        {
            return null;
        }

        return level == 1
            ? $"{NumberFormatting.FormatNumber(occupants)} {GetRootCountLabel(rootName)}"
            : $"{NumberFormatting.FormatNumber(occupants)} Occupants";
    }
}
