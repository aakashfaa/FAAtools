using FaaTools.Core.Synagogue.Model;

namespace FaaTools.Core.Synagogue;

/// <summary>
/// Resolves the set of snapshots to render for a given wizard selection. Shared by the PDF and
/// Revit renderers (both need the same Existing/Recommended snapshot resolution). Direct port of
/// build_snapshot_data / build_requested_snapshots from script.py.
/// </summary>
public static class SynagogueSnapshotBuilder
{
    private static readonly string[] NetAreaAliases = ["net program area"];
    private static readonly string[] GrossAreaAliases = ["gross floor area", "total gsf", "total gsf above grade"];

    public static SnapshotData BuildSnapshotData(ParsedWorkbook parsed, SpaceAllocationOption option, string snapshotTitle)
    {
        var roots = SynagogueWorkbookParser.BuildSnapshotRoots(parsed, option.Key);
        if (roots.Count == 0)
        {
            throw new InvalidOperationException($"No area data was found for \"{option.Label}\".");
        }

        var netArea = SynagogueWorkbookParser.GetSummaryArea(parsed.SummaryRows, option.Key, NetAreaAliases)
            ?? roots.Sum(r => r.Area);
        var grossArea = SynagogueWorkbookParser.GetSummaryArea(parsed.SummaryRows, option.Key, GrossAreaAliases)
            ?? netArea;

        return new SnapshotData(snapshotTitle, option, roots, netArea, grossArea);
    }

    public static IReadOnlyList<SnapshotData> BuildRequestedSnapshots(
        ParsedWorkbook parsed, string selectedOptionLabel, bool includeExisting, bool includeRecommended)
    {
        var optionMap = parsed.Options.ToDictionary(o => o.Label);
        if (!optionMap.ContainsKey(selectedOptionLabel))
        {
            throw new InvalidOperationException("Please select a valid space allocation.");
        }

        var snapshots = new List<SnapshotData>();
        var seenKeys = new HashSet<string>();

        if (includeExisting && optionMap.TryGetValue("EXISTING", out var existingOption))
        {
            snapshots.Add(BuildSnapshotData(parsed, existingOption, "Existing Building"));
            seenKeys.Add(existingOption.Key);
        }

        if (includeRecommended)
        {
            var selectedOption = optionMap[selectedOptionLabel];
            if (!seenKeys.Contains(selectedOption.Key))
            {
                snapshots.Add(BuildSnapshotData(parsed, selectedOption, "Recommended Building"));
            }
            else if (snapshots.Count == 0)
            {
                snapshots.Add(BuildSnapshotData(parsed, selectedOption, "Selected Space Allocation"));
            }
        }

        if (snapshots.Count == 0)
        {
            var selectedOption = optionMap[selectedOptionLabel];
            var defaultTitle = selectedOption.Key == "EXISTING" ? "Existing Building" : "Recommended Building";
            snapshots.Add(BuildSnapshotData(parsed, selectedOption, defaultTitle));
        }

        return snapshots;
    }
}
