using FaaTools.Core.Excel;
using FaaTools.Core.Synagogue.Model;

namespace FaaTools.Core.Synagogue;

/// <summary>
/// Parses a Synagogue Master Program workbook's "Area Tabulation" sheet into a ParsedWorkbook.
/// Direct port of parse_synagogue_workbook / parse_space_allocation_options / parse_area_hierarchy
/// / compute_node_metrics / build_snapshot_roots / get_summary_area from script.py.
///
/// Operates against IExcelCellsAccessor (not a live Excel session directly) so it can be unit
/// tested with a fake implementation instead of requiring Excel to be installed.
/// </summary>
public static class SynagogueWorkbookParser
{
    public const string DefaultSheetName = "Area Tabulation";

    public static readonly string[] SectionLetters = ["A.", "B.", "C.", "D.", "E.", "F.", "G.", "H."];
    public static readonly string[] AreaSectionLetters = ["A.", "B.", "C.", "D.", "E.", "F."];

    private const int SpaceAllocationSearchLimit = 80;

    public static ParsedWorkbook Parse(IExcelCellsAccessor cells)
    {
        var projectName = SynagogueText.NormalizeSpacing(cells.GetText(1, 1));
        var options = FindSpaceAllocationOptions(cells);
        var (roots, summaryRows, floorRows) = ParseAreaHierarchy(cells, options);

        if (roots.Count == 0)
        {
            throw new InvalidOperationException("No area sections A-F were found in the workbook.");
        }

        return new ParsedWorkbook(
            string.IsNullOrEmpty(projectName) ? "Synagogue Project" : projectName,
            options,
            roots,
            summaryRows,
            floorRows);
    }

    /// <summary>
    /// Scans column A (rows 1..min(usedRows,80)) for the "Space Allocation" header, then walks
    /// that row's columns classifying EXISTING / OPTION &lt;N&gt; labels until a "NOTES:" sentinel.
    /// Assigns OccupantCol = 7 + 4*index, AreaCol = 9 + 4*index per discovered option - this is
    /// the dynamic mechanism that lets users add more options to the workbook later.
    /// </summary>
    public static IReadOnlyList<SpaceAllocationOption> FindSpaceAllocationOptions(IExcelCellsAccessor cells)
    {
        var searchLimit = Math.Min(cells.UsedRowCount, SpaceAllocationSearchLimit);
        var optionRow = -1;
        for (var row = 1; row <= searchLimit; row++)
        {
            if (SynagogueText.NormalizeKey(cells.GetText(row, 1)) == "space allocation")
            {
                optionRow = row;
                break;
            }
        }

        if (optionRow < 0)
        {
            throw new InvalidOperationException("Could not find the \"Space Allocation\" row in column A.");
        }

        var options = new List<SpaceAllocationOption>();
        var seen = new HashSet<string>();
        for (var col = 2; col <= cells.UsedColumnCount; col++)
        {
            var rawText = cells.GetText(optionRow, col);
            if (SynagogueText.NormalizeOptionLabel(rawText) == "NOTES:")
            {
                break;
            }

            if (!SynagogueText.IsSpaceAllocationOption(rawText))
            {
                continue;
            }

            var label = SynagogueText.NormalizeOptionLabel(rawText);
            if (!seen.Add(label))
            {
                continue;
            }

            var optionIndex = options.Count;
            options.Add(new SpaceAllocationOption(label, label, 7 + (optionIndex * 4), 9 + (optionIndex * 4)));
        }

        if (options.Count == 0)
        {
            throw new InvalidOperationException("No usable space allocation options were found.");
        }

        return options;
    }

    /// <summary>
    /// Single top-to-bottom pass down column A tracking the current section letter (A.-H.),
    /// building the 3-level tree for A.-F., a summary-row dict for G., and a floor-row dict for H.
    /// </summary>
    private static (List<SpaceNode> Roots, Dictionary<string, SummaryRow> SummaryRows, Dictionary<string, SummaryRow> FloorRows)
        ParseAreaHierarchy(IExcelCellsAccessor cells, IReadOnlyList<SpaceAllocationOption> options)
    {
        var roots = new List<SpaceNode>();
        var summaryRows = new Dictionary<string, SummaryRow>();
        var floorRows = new Dictionary<string, SummaryRow>();

        string? currentSection = null;
        SpaceNode? currentRoot = null;
        SpaceNode? currentSecond = null;

        for (var row = 1; row <= cells.UsedRowCount; row++)
        {
            var firstMarker = SynagogueText.NormalizeSpacing(cells.GetText(row, 1)).ToUpperInvariant();
            if (Array.IndexOf(SectionLetters, firstMarker) >= 0)
            {
                currentSection = firstMarker;
                currentSecond = null;
                if (Array.IndexOf(AreaSectionLetters, firstMarker) >= 0)
                {
                    currentRoot = MakeNode(1, firstMarker, cells.GetText(row, 2), row, cells, options);
                    roots.Add(currentRoot);
                }
                else
                {
                    currentRoot = null;
                }

                continue;
            }

            if (currentSection is not null && Array.IndexOf(AreaSectionLetters, currentSection) >= 0 && currentRoot is not null)
            {
                var secondMarker = SynagogueText.NormalizeSpacing(cells.GetText(row, 2));
                var secondName = cells.GetText(row, 3);
                if (string.IsNullOrEmpty(secondName) && SynagogueText.IsSecondLevelMarker(cells.GetText(row, 1)))
                {
                    secondMarker = SynagogueText.NormalizeSpacing(cells.GetText(row, 1));
                    secondName = cells.GetText(row, 2);
                }

                if (SynagogueText.IsSecondLevelMarker(secondMarker) && !string.IsNullOrEmpty(secondName))
                {
                    currentSecond = MakeNode(2, secondMarker, secondName, row, cells, options);
                    currentRoot.Children.Add(currentSecond);
                    continue;
                }

                var thirdMarker = SynagogueText.NormalizeSpacing(cells.GetText(row, 3));
                var thirdName = cells.GetText(row, 4);
                if (string.IsNullOrEmpty(thirdName) && SynagogueText.IsThirdLevelMarker(cells.GetText(row, 2)))
                {
                    thirdMarker = SynagogueText.NormalizeSpacing(cells.GetText(row, 2));
                    thirdName = cells.GetText(row, 3);
                }

                if (currentSecond is not null && SynagogueText.IsThirdLevelMarker(thirdMarker) && !string.IsNullOrEmpty(thirdName))
                {
                    currentSecond.Children.Add(MakeNode(3, thirdMarker, thirdName, row, cells, options));
                }

                continue;
            }

            if (currentSection == "G.")
            {
                var name = cells.GetText(row, 2);
                if (!string.IsNullOrEmpty(name))
                {
                    summaryRows[SynagogueText.NormalizeKey(name)] =
                        new SummaryRow(SynagogueText.NormalizeSpacing(name), row, GetOptionValueMap(cells, row, options));
                }

                continue;
            }

            if (currentSection == "H.")
            {
                var name = cells.GetText(row, 2);
                if (!string.IsNullOrEmpty(name))
                {
                    floorRows[SynagogueText.NormalizeKey(name)] =
                        new SummaryRow(SynagogueText.NormalizeSpacing(name), row, GetOptionValueMap(cells, row, options));
                }
            }
        }

        return (roots, summaryRows, floorRows);
    }

    private static SpaceNode MakeNode(
        int level, string marker, string name, int row, IExcelCellsAccessor cells, IReadOnlyList<SpaceAllocationOption> options)
        => new()
        {
            Level = level,
            Marker = marker,
            Name = SynagogueText.NormalizeSpacing(name),
            Row = row,
            Values = GetOptionValueMap(cells, row, options),
        };

    private static Dictionary<string, OptionValue> GetOptionValueMap(
        IExcelCellsAccessor cells, int row, IReadOnlyList<SpaceAllocationOption> options)
    {
        var map = new Dictionary<string, OptionValue>();
        foreach (var option in options)
        {
            var occupants = NumericParsing.ToFloat(cells.GetValue(row, option.OccupantCol));
            var area = NumericParsing.ToFloat(cells.GetValue(row, option.AreaCol));
            map[option.Key] = new OptionValue(occupants, area);
        }

        return map;
    }

    /// <summary>
    /// Recursive rollup for one option: level-1 nodes with children always sum children; deeper
    /// nodes prefer their own value if &gt;0, else the sum of children; prunes nodes that resolve
    /// to zero area/occupants with no children. Direct port of compute_node_metrics.
    /// </summary>
    public static SpaceSnapshotNode? ComputeNodeMetrics(SpaceNode node, string optionKey)
    {
        var children = new List<SpaceSnapshotNode>();
        foreach (var child in node.Children)
        {
            var childView = ComputeNodeMetrics(child, optionKey);
            if (childView is not null)
            {
                children.Add(childView);
            }
        }

        node.Values.TryGetValue(optionKey, out var ownValue);
        var ownArea = ownValue.Area ?? 0.0;
        var ownOccupants = ownValue.Occupants ?? 0.0;

        var childArea = children.Sum(c => c.Area);
        var childOccupants = children.Sum(c => c.Occupants);

        double area;
        double occupants;
        if (node.Level == 1 && children.Count > 0)
        {
            area = childArea;
            occupants = childOccupants;
        }
        else if (children.Count > 0)
        {
            area = ownArea > 0 ? ownArea : childArea;
            occupants = ownOccupants > 0 ? ownOccupants : childOccupants;
        }
        else
        {
            area = ownArea;
            occupants = ownOccupants;
        }

        if (area <= 0 && occupants <= 0 && children.Count == 0)
        {
            return null;
        }

        return new SpaceSnapshotNode(node.Level, node.Marker, node.Name, area, occupants, children);
    }

    public static IReadOnlyList<SpaceSnapshotNode> BuildSnapshotRoots(ParsedWorkbook parsed, string optionKey)
    {
        var roots = new List<SpaceSnapshotNode>();
        foreach (var root in parsed.Roots)
        {
            var rootView = ComputeNodeMetrics(root, optionKey);
            if (rootView is not null && rootView.Area > 0)
            {
                roots.Add(rootView);
            }
        }

        return roots;
    }

    /// <summary>
    /// Tries each alias in order against summaryRows, returning the first non-null area found
    /// for optionKey - direct port of get_summary_area, including the "blank cell means try the
    /// next alias" fallback semantics.
    /// </summary>
    public static double? GetSummaryArea(IReadOnlyDictionary<string, SummaryRow> summaryRows, string optionKey, IEnumerable<string> aliases)
    {
        foreach (var alias in aliases)
        {
            if (!summaryRows.TryGetValue(alias, out var summary))
            {
                continue;
            }

            if (summary.Values.TryGetValue(optionKey, out var value) && value.Area is { } area)
            {
                return area;
            }
        }

        return null;
    }
}
