namespace FaaTools.Core.Synagogue.Model;

/// <summary>Result of SynagogueWorkbookParser.Parse - the full raw parse of one workbook.</summary>
public sealed record ParsedWorkbook(
    string ProjectName,
    IReadOnlyList<SpaceAllocationOption> Options,
    IReadOnlyList<SpaceNode> Roots,
    IReadOnlyDictionary<string, SummaryRow> SummaryRows,
    IReadOnlyDictionary<string, SummaryRow> FloorRows);

/// <summary>
/// A node rolled up for one specific option (e.g. "EXISTING"), produced by
/// SynagogueWorkbookParser.ComputeNodeMetrics/BuildSnapshotRoots. Unlike SpaceNode, Area and
/// Occupants are concrete resolved numbers (missing values already defaulted to 0), and this is
/// what the layout engine and renderers consume.
/// </summary>
public sealed record SpaceSnapshotNode(
    int Level,
    string Marker,
    string Name,
    double Area,
    double Occupants,
    IReadOnlyList<SpaceSnapshotNode> Children);
