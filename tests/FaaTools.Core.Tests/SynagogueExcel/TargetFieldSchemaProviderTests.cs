using FaaTools.Core.SynagogueExcel;

namespace FaaTools.Core.Tests.SynagogueExcel;

public class TargetFieldSchemaProviderTests
{
    [Fact]
    public void LoadDefault_reads_the_embedded_schema()
    {
        var schema = TargetFieldSchemaProvider.LoadDefault();

        Assert.Equal("Area Tabulation", schema.SheetName);
        Assert.Contains(schema.Fields, f => f.Key == "ProjectName" && f.Row == 1 && f.Col == 1 && f.Required);
        Assert.Contains(schema.Fields, f => f.Key == "ExportedOn" && f.Kind == TargetFieldKind.DateCombinedText && f.Row == 4 && f.Col == 1);
        Assert.Contains(schema.Fields, f => f.Key == "JobNumber" && !f.Required);
    }
}
