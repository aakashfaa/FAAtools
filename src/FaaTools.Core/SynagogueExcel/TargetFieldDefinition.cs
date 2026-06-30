namespace FaaTools.Core.SynagogueExcel;

/// <summary>
/// How a field's value should be written into its target cell. Text writes the raw value;
/// DateCombinedText formats the value into <see cref="TargetFieldDefinition.TextFormat"/>
/// (e.g. "Date: {0}"), since the Synagogue Master Program workbook keeps date text in a single
/// combined cell rather than split label/value cells.
/// </summary>
public enum TargetFieldKind
{
    Text,
    DateCombinedText,
}

/// <summary>
/// One field in the Synagogue Excel wizard, and where it lands in the workbook. Kept data-driven
/// (loaded from the embedded TargetFieldSchema.json) rather than hardcoded, since these fields
/// are expected to evolve toward Synagogue-specific targets over time - adding a field should be
/// a schema edit, not a recompile.
/// </summary>
public sealed record TargetFieldDefinition(
    string Key,
    string Label,
    TargetFieldKind Kind,
    int Row,
    int Col,
    bool Required,
    string? TextFormat = null,
    string? Notes = null);

public sealed record TargetFieldSchema(string SheetName, IReadOnlyList<TargetFieldDefinition> Fields);
