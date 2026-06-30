namespace FaaTools.Core.Synagogue.Model;

/// <summary>
/// A row from section G. ("Net Program Area", "Gross Floor Area", etc.) or section H. (floor
/// rows), keyed by SynagogueText.NormalizeKey(name) in ParsedWorkbook.SummaryRows/FloorRows.
/// </summary>
public sealed record SummaryRow(string Name, int Row, IReadOnlyDictionary<string, OptionValue> Values);
