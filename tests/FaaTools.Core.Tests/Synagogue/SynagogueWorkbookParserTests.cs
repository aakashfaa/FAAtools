using FaaTools.Core.Synagogue;

namespace FaaTools.Core.Tests.Synagogue;

/// <summary>
/// Seeds a fake worksheet mirroring the real Synagogue Master Program spreadsheet's structure
/// (confirmed via a live read of the actual file: row 6 = "Space Allocation" header with
/// EXISTING/OPTION N labels, A./B./... section letters, 1./2. and a./b. sub-markers, G. summary
/// rows) so the parser can be exercised without Excel installed.
/// </summary>
public class SynagogueWorkbookParserTests
{
    private static FakeExcelCellsAccessor BuildSampleWorkbook()
    {
        var cells = new FakeExcelCellsAccessor { UsedRowCount = 14, UsedColumnCount = 14 };

        cells.Set(1, 1, "Test Synagogue Project");

        // Space Allocation header row: EXISTING label at col 6, OPTION 1 label at col 10 -
        // the parser discovers option data columns at 7+4*index / 9+4*index regardless of which
        // column the label itself sits in.
        cells.Set(6, 1, "Space Allocation");
        cells.Set(6, 6, "EXISTING");
        cells.Set(6, 10, "OPTION 1");
        cells.Set(6, 14, "NOTES:");

        // Section A - Worship Program (level 1 root)
        cells.Set(9, 1, "A.");
        cells.Set(9, 2, "Worship Program");

        // 1. Sanctuary (level 2)
        cells.Set(10, 2, "1.");
        cells.Set(10, 3, "Sanctuary");
        cells.Set(10, 7, 250.0); // EXISTING occupants (col 7)
        cells.Set(10, 9, 4000.0); // EXISTING area (col 9)
        cells.Set(10, 11, 400.0); // OPTION 1 occupants (col 11)
        cells.Set(10, 13, 6000.0); // OPTION 1 area (col 13)

        // a. Main Hall (level 3, under Sanctuary)
        cells.Set(11, 3, "a.");
        cells.Set(11, 4, "Main Hall");
        cells.Set(11, 7, 250.0);
        cells.Set(11, 9, 4000.0);
        cells.Set(11, 11, 400.0);
        cells.Set(11, 13, 6000.0);

        // Section B - Social Hall (level 1 root, no children, direct values)
        cells.Set(12, 1, "B.");
        cells.Set(12, 2, "Social Hall");
        cells.Set(12, 7, 50.0);
        cells.Set(12, 9, 1500.0);
        cells.Set(12, 11, 60.0);
        cells.Set(12, 13, 1800.0);

        // Section G - summary rows
        cells.Set(13, 1, "G.");
        cells.Set(14, 2, "Net Program Area");
        cells.Set(14, 9, 5500.0);
        cells.Set(14, 13, 7800.0);

        return cells;
    }

    [Fact]
    public void Parse_discovers_existing_and_option_columns_dynamically()
    {
        var parsed = SynagogueWorkbookParser.Parse(BuildSampleWorkbook());

        Assert.Equal(2, parsed.Options.Count);
        Assert.Equal("EXISTING", parsed.Options[0].Key);
        Assert.Equal(7, parsed.Options[0].OccupantCol);
        Assert.Equal(9, parsed.Options[0].AreaCol);
        Assert.Equal("OPTION 1", parsed.Options[1].Key);
        Assert.Equal(11, parsed.Options[1].OccupantCol);
        Assert.Equal(13, parsed.Options[1].AreaCol);
    }

    [Fact]
    public void Parse_builds_three_level_area_hierarchy()
    {
        var parsed = SynagogueWorkbookParser.Parse(BuildSampleWorkbook());

        Assert.Equal(2, parsed.Roots.Count);

        var worship = parsed.Roots[0];
        Assert.Equal("Worship Program", worship.Name);
        var sanctuary = Assert.Single(worship.Children);
        Assert.Equal("Sanctuary", sanctuary.Name);
        var mainHall = Assert.Single(sanctuary.Children);
        Assert.Equal("Main Hall", mainHall.Name);

        var social = parsed.Roots[1];
        Assert.Equal("Social Hall", social.Name);
        Assert.Empty(social.Children);
    }

    [Fact]
    public void BuildSnapshotRoots_rolls_level1_area_up_from_children()
    {
        var parsed = SynagogueWorkbookParser.Parse(BuildSampleWorkbook());
        var roots = SynagogueWorkbookParser.BuildSnapshotRoots(parsed, "EXISTING");

        var worship = roots.Single(r => r.Name == "Worship Program");
        Assert.Equal(4000, worship.Area);
        Assert.Equal(250, worship.Occupants);

        var social = roots.Single(r => r.Name == "Social Hall");
        Assert.Equal(1500, social.Area);
        Assert.Equal(50, social.Occupants);
    }

    [Fact]
    public void BuildSnapshotRoots_uses_the_requested_option_key()
    {
        var parsed = SynagogueWorkbookParser.Parse(BuildSampleWorkbook());
        var roots = SynagogueWorkbookParser.BuildSnapshotRoots(parsed, "OPTION 1");

        var worship = roots.Single(r => r.Name == "Worship Program");
        Assert.Equal(6000, worship.Area);
        Assert.Equal(400, worship.Occupants);
    }

    [Fact]
    public void GetSummaryArea_falls_back_through_aliases_and_returns_null_when_none_match()
    {
        var parsed = SynagogueWorkbookParser.Parse(BuildSampleWorkbook());

        var netArea = SynagogueWorkbookParser.GetSummaryArea(parsed.SummaryRows, "EXISTING", ["does not exist", "net program area"]);
        Assert.Equal(5500, netArea);

        var missing = SynagogueWorkbookParser.GetSummaryArea(parsed.SummaryRows, "EXISTING", ["nope"]);
        Assert.Null(missing);
    }
}
