using System.Windows;
using System.Windows.Controls;
using FaaTools.Core.Synagogue.Model;
using static FaaTools.Core.Synagogue.Layout.SynagogueLayoutConstants;

namespace FaaTools.Core.Synagogue.Layout;

/// <summary>
/// Treemap-style layout engine: assigns box sizes to nodes by sqrt(area) scaled into per-level
/// bounds, measures subtree footprints bottom-up, positions a forest of root boxes, then scales
/// the whole forest to fit the available space. Render-target-agnostic (no Revit/PDF-specific
/// code) - both SynagoguePdfRenderer and SynagogueRevitRenderer consume the same LayoutNode tree
/// this produces. Direct port of layout_forest and its helpers (script.py lines ~616-879).
///
/// One WPF dependency survives the port: PrepareNodeVisual measures actual text-wrapping height
/// via TextBlock.Measure, exactly as the original script does, because accurate box auto-sizing
/// needs a real text layout pass, not just an approximation. The Core project already references
/// WPF for the PDF renderer, so this doesn't add a new dependency.
/// </summary>
public static class LayoutEngine
{
    public static LayoutResult LayoutForest(
        IReadOnlyList<SpaceSnapshotNode> snapshotRoots, double availableWidth, double availableHeight)
    {
        if (snapshotRoots.Count == 0)
        {
            return new LayoutResult([], 1.0, 0.0, 0.0);
        }

        var roots = snapshotRoots.Select(BuildLayoutNode).ToList();

        AssignVisualSizes(roots);
        foreach (var root in roots)
        {
            PrepareNodeVisual(root, root.Name);
            MeasureSubtree(root);
            PositionNode(root, 0.0, 0.0);
        }

        var totalWidth = 0.0;
        var totalHeight = 0.0;
        for (var index = 0; index < roots.Count; index++)
        {
            if (index > 0)
            {
                totalWidth += RootGap;
            }

            totalWidth += roots[index].SubtreeWidth;
            totalHeight = Math.Max(totalHeight, roots[index].SubtreeHeight);
        }

        var widthFactor = totalWidth > 0 ? availableWidth / totalWidth : 1.0;
        var heightFactor = totalHeight > 0 ? availableHeight / totalHeight : 1.0;
        var scaleFactor = Math.Min(1.0, Math.Min(widthFactor, heightFactor));

        if (scaleFactor < 1.0)
        {
            foreach (var root in roots)
            {
                ScaleNodeLayout(root, scaleFactor);
            }

            totalWidth = 0.0;
            totalHeight = 0.0;
            for (var index = 0; index < roots.Count; index++)
            {
                if (index > 0)
                {
                    totalWidth += RootGap * scaleFactor;
                }

                totalWidth += roots[index].SubtreeWidth;
                totalHeight = Math.Max(totalHeight, roots[index].SubtreeHeight);
            }
        }

        var startX = Math.Max(0.0, (availableWidth - totalWidth) / 2.0);
        var startY = Math.Max(0.0, (availableHeight - totalHeight) / 2.0);
        var currentX = startX;
        for (var index = 0; index < roots.Count; index++)
        {
            var root = roots[index];
            var offsetX = currentX - root.BoxX;
            OffsetTree(root, offsetX, startY);
            currentX += root.SubtreeWidth;
            if (index < roots.Count - 1)
            {
                currentX += RootGap * scaleFactor;
            }
        }

        return new LayoutResult(roots, scaleFactor, totalWidth, totalHeight);
    }

    public static void OffsetTree(LayoutNode node, double deltaX, double deltaY)
    {
        node.BoxX += deltaX;
        node.BoxY += deltaY;
        foreach (var child in node.Children)
        {
            OffsetTree(child, deltaX, deltaY);
        }
    }

    public static (double MinX, double MaxX, double MinY, double MaxY) GetTreeBounds(LayoutNode node)
    {
        var minX = node.BoxX;
        var maxX = node.BoxX + node.BoxSize;
        var minY = node.BoxY;
        var maxY = node.BoxY + node.BoxSize;

        if (node.Visual is { TitleOutside: true } visual)
        {
            var outsideWidth = visual.OutsideWidth;
            var outsideHeight = visual.OutsideHeight;
            var labelX = node.BoxX + (node.BoxSize / 2.0) - (outsideWidth / 2.0);
            var labelY = visual.OutsidePosition == "above"
                ? node.BoxY - outsideHeight - OutsideLabelGap - 2.0
                : node.BoxY + node.BoxSize + OutsideLabelGap;

            minX = Math.Min(minX, labelX);
            maxX = Math.Max(maxX, labelX + outsideWidth);
            minY = Math.Min(minY, labelY);
            maxY = Math.Max(maxY, labelY + outsideHeight + 4.0);
        }

        foreach (var child in node.Children)
        {
            var (childMinX, childMaxX, childMinY, childMaxY) = GetTreeBounds(child);
            minX = Math.Min(minX, childMinX);
            maxX = Math.Max(maxX, childMaxX);
            minY = Math.Min(minY, childMinY);
            maxY = Math.Max(maxY, childMaxY);
        }

        return (minX, maxX, minY, maxY);
    }

    private static LayoutNode BuildLayoutNode(SpaceSnapshotNode source)
    {
        var node = new LayoutNode { Source = source };
        foreach (var child in source.Children)
        {
            node.Children.Add(BuildLayoutNode(child));
        }

        return node;
    }

    private static IEnumerable<LayoutNode> TraverseNodes(IEnumerable<LayoutNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in TraverseNodes(node.Children))
            {
                yield return child;
            }
        }
    }

    private static void AssignVisualSizes(IReadOnlyList<LayoutNode> roots)
    {
        var areasByLevel = new Dictionary<int, List<double>> { [1] = [], [2] = [], [3] = [] };
        foreach (var node in TraverseNodes(roots))
        {
            if (node.Area > 0)
            {
                areasByLevel[node.Level].Add(Math.Sqrt(node.Area));
            }
        }

        var stats = new Dictionary<int, (double Min, double Max)>();
        foreach (var (level, values) in areasByLevel)
        {
            stats[level] = values.Count > 0 ? (values.Min(), values.Max()) : (1.0, 1.0);
        }

        foreach (var node in TraverseNodes(roots))
        {
            var (minimum, maximum) = stats[node.Level];
            var (lower, upper) = SizeBounds[node.Level];
            var areaValue = Math.Max(node.Area, 1.0);
            var metric = Math.Sqrt(areaValue);

            double size;
            if (Math.Abs(maximum - minimum) < 0.001)
            {
                size = (lower + upper) / 2.0;
            }
            else
            {
                var ratio = (metric - minimum) / (maximum - minimum);
                size = lower + ((upper - lower) * ratio);
            }

            node.BoxSize = size;
        }
    }

    private static double MeasureWrappedTextHeight(string? text, double width, double fontSize, FontWeight? fontWeight = null)
    {
        var block = new TextBlock
        {
            Text = text ?? string.Empty,
            FontSize = fontSize,
            TextWrapping = TextWrapping.Wrap,
            Width = Math.Max(1.0, width),
        };
        if (fontWeight is { } weight)
        {
            block.FontWeight = weight;
        }

        block.Measure(new Size(block.Width, 1000.0));
        return block.DesiredSize.Height;
    }

    private static void PrepareNodeVisual(LayoutNode node, string rootName)
    {
        var size = node.BoxSize;
        var padding = Math.Max(NodeTextPadding, size * 0.05);
        var textWidth = Math.Max(24.0, size - (2.0 * padding));
        var titleFont = Math.Max(7.5, Math.Min(14.5, size * (node.Level == 1 ? 0.12 : 0.11)));
        var valueFont = Math.Max(6.5, Math.Min(12.5, size * 0.10));
        var countText = ColorAndLabelCatalog.GetNodeCountText(node.Occupants, node.Level, rootName);
        var areaText = $"{NumberFormatting.FormatNumber(node.Area)} SF";
        var availableHeight = Math.Max(20.0, size - (2.0 * padding));

        double TotalHeightForFonts(double titleFontSize, double valueFontSize, bool includeTitle)
        {
            var total = 0.0;
            if (includeTitle)
            {
                total += MeasureWrappedTextHeight(node.Name, textWidth, titleFontSize, FontWeights.SemiBold);
            }

            if (!string.IsNullOrEmpty(countText))
            {
                if (total > 0.0)
                {
                    total += NodeTextGap;
                }

                total += MeasureWrappedTextHeight(countText, textWidth, valueFontSize);
            }

            if (total > 0.0)
            {
                total += NodeTextGap;
            }

            total += MeasureWrappedTextHeight(areaText, textWidth, valueFontSize);
            return total;
        }

        while (titleFont > 7.0 && TotalHeightForFonts(titleFont, valueFont, true) > availableHeight)
        {
            titleFont -= 0.5;
        }

        while (valueFont > 6.0 && TotalHeightForFonts(titleFont, valueFont, true) > availableHeight)
        {
            valueFont -= 0.5;
        }

        var titleOutside = TotalHeightForFonts(titleFont, valueFont, true) > availableHeight;
        var outsidePosition = node.Children.Count > 0 ? "above" : "below";
        var outsideFont = Math.Max(7.0, Math.Min(13.0, titleFont));
        var outsideWidth = Math.Min(OutsideLabelMaxWidth, Math.Max(size + 24.0, size * 1.7));
        var outsideHeight = 0.0;

        if (titleOutside)
        {
            while (valueFont > 6.0 && TotalHeightForFonts(titleFont, valueFont, false) > availableHeight)
            {
                valueFont -= 0.5;
            }

            outsideHeight = MeasureWrappedTextHeight(node.Name, outsideWidth, outsideFont, FontWeights.SemiBold);
        }

        node.Visual = new NodeVisual
        {
            Padding = padding,
            TextWidth = textWidth,
            TitleFont = titleFont,
            ValueFont = valueFont,
            CountText = countText,
            AreaText = areaText,
            TitleOutside = titleOutside,
            OutsidePosition = outsidePosition,
            OutsideFont = outsideFont,
            OutsideWidth = outsideWidth,
            OutsideHeight = outsideHeight,
            OuterTop = titleOutside && outsidePosition == "above" ? outsideHeight + OutsideLabelGap : 0.0,
            OuterBottom = titleOutside && outsidePosition == "below" ? outsideHeight + OutsideLabelGap : 0.0,
        };

        foreach (var child in node.Children)
        {
            PrepareNodeVisual(child, rootName);
        }
    }

    private static void MeasureSubtree(LayoutNode node)
    {
        var outerTop = node.Visual?.OuterTop ?? 0.0;
        var outerBottom = node.Visual?.OuterBottom ?? 0.0;

        if (node.Children.Count == 0)
        {
            node.SubtreeWidth = node.BoxSize;
            node.SubtreeHeight = outerTop + node.BoxSize + outerBottom;
            return;
        }

        foreach (var child in node.Children)
        {
            MeasureSubtree(child);
        }

        var childrenTotal = 0.0;
        var maxChildHeight = 0.0;
        for (var index = 0; index < node.Children.Count; index++)
        {
            if (index > 0)
            {
                childrenTotal += SiblingGap;
            }

            var child = node.Children[index];
            childrenTotal += child.SubtreeWidth;
            maxChildHeight = Math.Max(maxChildHeight, child.SubtreeHeight);
        }

        var verticalGap = LevelVerticalGap.GetValueOrDefault(node.Level, 56.0);
        node.SubtreeWidth = Math.Max(node.BoxSize, childrenTotal);
        node.SubtreeHeight = outerTop + node.BoxSize + outerBottom + verticalGap + maxChildHeight;
    }

    private static void PositionNode(LayoutNode node, double left, double top)
    {
        var outerTop = node.Visual?.OuterTop ?? 0.0;
        var outerBottom = node.Visual?.OuterBottom ?? 0.0;
        var centerX = left + (node.SubtreeWidth / 2.0);
        node.BoxX = centerX - (node.BoxSize / 2.0);
        node.BoxY = top + outerTop;

        if (node.Children.Count == 0)
        {
            return;
        }

        var childrenTotal = 0.0;
        for (var index = 0; index < node.Children.Count; index++)
        {
            if (index > 0)
            {
                childrenTotal += SiblingGap;
            }

            childrenTotal += node.Children[index].SubtreeWidth;
        }

        var startX = left + ((node.SubtreeWidth - childrenTotal) / 2.0);
        var verticalGap = LevelVerticalGap.GetValueOrDefault(node.Level, 56.0);
        var childTop = node.BoxY + node.BoxSize + outerBottom + verticalGap;
        var currentX = startX;
        foreach (var child in node.Children)
        {
            PositionNode(child, currentX, childTop);
            currentX += child.SubtreeWidth + SiblingGap;
        }
    }

    private static void ScaleNodeLayout(LayoutNode node, double factor)
    {
        node.BoxSize *= factor;
        node.SubtreeWidth *= factor;
        node.SubtreeHeight *= factor;
        node.BoxX *= factor;
        node.BoxY *= factor;

        if (node.Visual is { } visual)
        {
            visual.Padding *= factor;
            visual.TextWidth *= factor;
            visual.TitleFont *= factor;
            visual.ValueFont *= factor;
            visual.OutsideFont *= factor;
            visual.OutsideWidth *= factor;
            visual.OutsideHeight *= factor;
            visual.OuterTop *= factor;
            visual.OuterBottom *= factor;
        }

        foreach (var child in node.Children)
        {
            ScaleNodeLayout(child, factor);
        }
    }
}
