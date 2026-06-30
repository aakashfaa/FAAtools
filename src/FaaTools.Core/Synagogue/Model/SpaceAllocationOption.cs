namespace FaaTools.Core.Synagogue.Model;

/// <summary>
/// One discovered "EXISTING" / "OPTION N" column group in the Space Allocation header row.
/// OccupantCol/AreaCol are 1-based Excel columns, computed as 7 + 4*index / 9 + 4*index by
/// SynagogueWorkbookParser - the mechanism that lets users add more options to the workbook
/// (the parser just discovers however many are present).
/// </summary>
public sealed record SpaceAllocationOption(string Key, string Label, int OccupantCol, int AreaCol);
