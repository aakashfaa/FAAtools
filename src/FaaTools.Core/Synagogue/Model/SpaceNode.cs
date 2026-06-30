namespace FaaTools.Core.Synagogue.Model;

/// <summary>
/// Per-option occupant/area pair for one row. Nullable because a blank cell means "no data for
/// this option here" (distinct from an explicit zero) - this distinction matters for summary-row
/// alias fallback in SynagogueWorkbookParser.GetSummaryArea. Direct port of to_float's
/// None-on-blank semantics.
/// </summary>
public readonly record struct OptionValue(double? Occupants, double? Area);

/// <summary>
/// One row in the parsed area hierarchy (level 1 = section A-F, level 2 = numbered sub-item,
/// level 3 = lettered sub-sub-item). Mutable (Children is appended to during parsing) - direct
/// port of make_node's dict shape.
/// </summary>
public sealed class SpaceNode
{
    public required int Level { get; init; }

    public required string Marker { get; init; }

    public required string Name { get; init; }

    public required int Row { get; init; }

    public List<SpaceNode> Children { get; } = [];

    public Dictionary<string, OptionValue> Values { get; init; } = [];
}
