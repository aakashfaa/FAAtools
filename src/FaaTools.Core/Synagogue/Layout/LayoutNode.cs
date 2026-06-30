using FaaTools.Core.Synagogue.Model;

namespace FaaTools.Core.Synagogue.Layout;

/// <summary>
/// Per-node visual sizing computed by LayoutEngine.PrepareNodeVisual: font sizes, whether the
/// title had to move outside the box, and the resulting "outer" padding that label consumes
/// above/below the box. Direct port of the "visual" dict in script.py.
/// </summary>
public sealed class NodeVisual
{
    public double Padding { get; set; }

    public double TextWidth { get; set; }

    public double TitleFont { get; set; }

    public double ValueFont { get; set; }

    public string? CountText { get; set; }

    public string AreaText { get; set; } = string.Empty;

    public bool TitleOutside { get; set; }

    /// <summary>"above" or "below".</summary>
    public string OutsidePosition { get; set; } = "below";

    public double OutsideFont { get; set; }

    public double OutsideWidth { get; set; }

    public double OutsideHeight { get; set; }

    public double OuterTop { get; set; }

    public double OuterBottom { get; set; }
}

/// <summary>
/// Mutable per-layout-pass wrapper around an immutable SpaceSnapshotNode. A fresh LayoutNode
/// tree is built for each LayoutForest call (see LayoutEngine.BuildLayoutNode), which is why this
/// port doesn't need script.py's clone_tree/clone_roots dance - the source data is never mutated.
/// </summary>
public sealed class LayoutNode
{
    public required SpaceSnapshotNode Source { get; init; }

    public List<LayoutNode> Children { get; } = [];

    public int Level => Source.Level;

    public string Name => Source.Name;

    public double Area => Source.Area;

    public double Occupants => Source.Occupants;

    public double BoxSize { get; set; }

    public double SubtreeWidth { get; set; }

    public double SubtreeHeight { get; set; }

    public double BoxX { get; set; }

    public double BoxY { get; set; }

    public NodeVisual? Visual { get; set; }
}

public sealed record LayoutResult(IReadOnlyList<LayoutNode> Roots, double ScaleFactor, double TotalWidth, double TotalHeight);
