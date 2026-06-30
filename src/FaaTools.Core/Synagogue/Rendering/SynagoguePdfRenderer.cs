using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using FaaTools.Core.Synagogue.Layout;
using FaaTools.Core.Synagogue.Model;
using static FaaTools.Core.Synagogue.Layout.SynagogueLayoutConstants;

namespace FaaTools.Core.Synagogue.Rendering;

/// <summary>
/// Builds the multi-page PDF export (one overview page + one page per root section, per
/// requested snapshot) and prints it via the "Microsoft Print to PDF" XPS print queue. Direct
/// port of script.py lines ~882-1379 (create_page through print_document_to_pdf).
///
/// Not ported: should_split_snapshot_page / count_tree_nodes - present in script.py but never
/// actually called from build_document, so they have no observable effect on current behavior
/// and are dropped rather than carried forward as dead code.
/// </summary>
public static class SynagoguePdfRenderer
{
    private const string BorderStrokeHex = "#555555";

    public static FixedDocument BuildDocument(
        ParsedWorkbook parsed, string selectedOptionLabel, bool includeDistribution, bool includeExisting, bool includeRecommended)
    {
        var snapshots = SynagogueSnapshotBuilder.BuildRequestedSnapshots(parsed, selectedOptionLabel, includeExisting, includeRecommended);

        var document = new FixedDocument();
        foreach (var snapshot in snapshots)
        {
            AppendPage(document, BuildSnapshotOverviewPage(snapshot, parsed.ProjectName, includeDistribution));
            foreach (var root in snapshot.Roots)
            {
                AppendPage(document, BuildSnapshotSectionPage(snapshot, parsed.ProjectName, root));
            }
        }

        return document;
    }

    /// <summary>
    /// Prints to PDF via the "Microsoft Print to PDF" XPS queue. Improvement over the original
    /// script's exact-name match: also matches by driver name (case-insensitive "contains"), so
    /// minor display-name variations don't cause a false "not found".
    /// </summary>
    public static void PrintDocumentToPdf(FixedDocument document)
    {
        var server = new LocalPrintServer();
        var queues = server.GetPrintQueues();

        var pdfQueue = queues.FirstOrDefault(q => string.Equals(q.Name, "Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase))
            ?? queues.FirstOrDefault(q => q.QueueDriver?.Name?.Contains("Microsoft Print To PDF", StringComparison.OrdinalIgnoreCase) == true);

        if (pdfQueue is null)
        {
            throw new InvalidOperationException(
                "\"Microsoft Print to PDF\" was not found on this machine. " +
                "Enable it under Windows Settings > Apps > Optional Features > Microsoft Print to PDF.");
        }

        var ticket = pdfQueue.DefaultPrintTicket;
        try
        {
            ticket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA3);
            ticket.PageOrientation = PageOrientation.Landscape;
        }
        catch
        {
            // best-effort, matches the original script's try/except around ticket customization
        }

        var writer = PrintQueue.CreateXpsDocumentWriter(pdfQueue);
        writer.Write(document, ticket);
    }

    private static void AppendPage(FixedDocument document, FixedPage page)
    {
        var pageContent = new PageContent { Child = page };
        document.Pages.Add(pageContent);
    }

    private static FixedPage CreatePage() => new() { Width = PageWidth, Height = PageHeight };

    private static void AddPageContentBorder(FixedPage page)
    {
        var border = new Border
        {
            Width = FrameWidth,
            Height = FrameHeight,
            BorderBrush = WpfColorHelper.BrushFromHex(BorderHex),
            BorderThickness = new Thickness(1),
        };
        FixedPage.SetLeft(border, FrameLeft);
        FixedPage.SetTop(border, FrameTop);
        page.Children.Add(border);
    }

    private static void AddPageHeader(FixedPage page, string projectName)
    {
        var title = new TextBlock
        {
            Text = "Space Programming",
            FontSize = HeaderTitleSize,
            FontWeight = FontWeights.Bold,
            Foreground = WpfColorHelper.BrushFromHex(TextHex),
        };
        FixedPage.SetLeft(title, MarginLeft);
        FixedPage.SetTop(title, MarginTop);
        page.Children.Add(title);

        var subtitle = new TextBlock
        {
            Text = SynagogueText.NormalizeSpacing(projectName).ToUpperInvariant(),
            FontSize = HeaderSubtitleSize,
            Foreground = WpfColorHelper.BrushFromHex(TextHex),
            FontWeight = FontWeights.Medium,
        };
        FixedPage.SetLeft(subtitle, MarginLeft);
        FixedPage.SetTop(subtitle, MarginTop + 72.0);
        page.Children.Add(subtitle);
    }

    private static void AddFooter(FixedPage page)
    {
        var footer = new TextBlock
        {
            Text = "FINEGOLD ALEXANDER ARCHITECTS",
            FontSize = FooterTextSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = WpfColorHelper.BrushFromHex(FaaOrangeHex),
            TextAlignment = TextAlignment.Right,
            Width = 360.0,
        };
        FixedPage.SetLeft(footer, PageWidth - MarginRight - footer.Width);
        FixedPage.SetTop(footer, PageHeight - 42.0);
        page.Children.Add(footer);
    }

    private static void AddSnapshotSummary(FixedPage page, string snapshotTitle, double netArea, double grossArea)
    {
        var title = new TextBlock
        {
            Text = snapshotTitle,
            FontSize = SummaryTitleSize,
            FontWeight = FontWeights.Bold,
            Foreground = WpfColorHelper.BrushFromHex(TextHex),
        };
        FixedPage.SetLeft(title, FrameLeft + FrameWidth - 350.0);
        FixedPage.SetTop(title, FrameTop + 46.0);
        page.Children.Add(title);

        var detail = new TextBlock
        {
            Text = $"NET PROGRAM AREA: {NumberFormatting.FormatNumber(netArea)} SF\n" +
                   $"GROSS FLOOR AREA: {NumberFormatting.FormatNumber(grossArea)} SF",
            FontSize = SummaryTextSize,
            Foreground = WpfColorHelper.BrushFromHex(TextHex),
            TextWrapping = TextWrapping.Wrap,
            Width = 300.0,
        };
        FixedPage.SetLeft(detail, FrameLeft + FrameWidth - 346.0);
        FixedPage.SetTop(detail, FrameTop + 108.0);
        page.Children.Add(detail);
    }

    private static void AddSectionHeading(FixedPage page, string sectionName)
    {
        var heading = new TextBlock
        {
            Text = sectionName,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = WpfColorHelper.BrushFromHex(TextHex),
            Width = 760.0,
            TextWrapping = TextWrapping.Wrap,
        };
        FixedPage.SetLeft(heading, FrameLeft + 42.0);
        FixedPage.SetTop(heading, FrameTop + 44.0);
        page.Children.Add(heading);
    }

    private static void AddDistributionBar(FixedPage page, IReadOnlyList<SpaceSnapshotNode> roots, double netArea)
    {
        var totalArea = netArea > 0 ? netArea : roots.Sum(r => r.Area);
        if (totalArea <= 0)
        {
            return;
        }

        const double barLeft = FrameLeft + 90.0;
        const double barTop = FrameTop + 140.0;
        const double barWidth = 590.0;
        const double barHeight = 48.0;
        var currentX = barLeft;

        foreach (var root in roots)
        {
            var ratio = totalArea > 0 ? root.Area / totalArea : 0.0;
            var segmentWidth = Math.Max(1.0, barWidth * ratio);

            var segment = new Border
            {
                Width = segmentWidth,
                Height = barHeight,
                Background = WpfColorHelper.BrushFromHex(ColorAndLabelCatalog.GetRootColorHex(root.Name)),
                BorderBrush = WpfColorHelper.BrushFromHex(BorderStrokeHex),
                BorderThickness = new Thickness(0.75),
            };
            FixedPage.SetLeft(segment, currentX);
            FixedPage.SetTop(segment, barTop);
            page.Children.Add(segment);

            var percent = new TextBlock
            {
                Text = NumberFormatting.FormatPercent(ratio * 100.0),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = WpfColorHelper.BrushFromHex(TextHex),
                TextAlignment = TextAlignment.Center,
                Width = segmentWidth,
            };
            FixedPage.SetLeft(percent, currentX);
            FixedPage.SetTop(percent, barTop + 10.0);
            page.Children.Add(percent);

            var labelWidth = Math.Max(segmentWidth + 24.0, 120.0);
            var label = new TextBlock
            {
                Text = root.Name,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = WpfColorHelper.BrushFromHex(TextHex),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Width = labelWidth,
            };
            FixedPage.SetLeft(label, currentX - ((labelWidth - segmentWidth) / 2.0));
            FixedPage.SetTop(label, barTop - 46.0);
            page.Children.Add(label);

            currentX += segmentWidth;
        }
    }

    private static void AddLine(FixedPage page, double x1, double y1, double x2, double y2, double thickness)
    {
        var line = new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = WpfColorHelper.BrushFromHex(BorderStrokeHex),
            StrokeThickness = thickness,
        };
        page.Children.Add(line);
    }

    private static TextBlock CreateBoxTextBlock(string text, double fontSize, bool bold, double? width = null)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Regular,
            Foreground = WpfColorHelper.BrushFromHex(TextHex),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        if (width is { } w)
        {
            block.Width = w;
        }

        return block;
    }

    private static void AddOutsideTitle(FixedPage page, LayoutNode node)
    {
        if (node.Visual is not { TitleOutside: true } visual)
        {
            return;
        }

        var label = new Border
        {
            Width = visual.OutsideWidth,
            Height = visual.OutsideHeight + 4.0,
            Background = Brushes.White,
            Child = CreateBoxTextBlock(node.Name, visual.OutsideFont, true, visual.OutsideWidth - 4.0),
        };

        var labelX = node.BoxX + (node.BoxSize / 2.0) - (visual.OutsideWidth / 2.0);
        labelX = Math.Max(FrameLeft + 6.0, Math.Min(labelX, FrameLeft + FrameWidth - visual.OutsideWidth - 6.0));

        var labelY = visual.OutsidePosition == "above"
            ? node.BoxY - visual.OutsideHeight - OutsideLabelGap - 2.0
            : node.BoxY + node.BoxSize + OutsideLabelGap;
        labelY = Math.Max(FrameTop + 6.0, Math.Min(labelY, FrameTop + FrameHeight - label.Height - 6.0));

        FixedPage.SetLeft(label, labelX);
        FixedPage.SetTop(label, labelY);
        page.Children.Add(label);
    }

    private static void AddNodeBox(FixedPage page, LayoutNode node, string rootName)
    {
        var size = node.BoxSize;
        var visual = node.Visual;

        var border = new Border
        {
            Width = size,
            Height = size,
            Background = WpfColorHelper.BrushFromHex(ColorAndLabelCatalog.GetRootColorHex(rootName)),
            BorderBrush = WpfColorHelper.BrushFromHex(BorderStrokeHex),
            BorderThickness = new Thickness(1),
        };

        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(visual?.Padding ?? NodeTextPadding),
        };

        if (visual is not { TitleOutside: true })
        {
            stack.Children.Add(CreateBoxTextBlock(node.Name, visual?.TitleFont ?? 10.0, true, visual?.TextWidth));
        }

        if (!string.IsNullOrEmpty(visual?.CountText))
        {
            stack.Children.Add(CreateBoxTextBlock(visual.CountText, visual.ValueFont, false, visual.TextWidth));
        }

        stack.Children.Add(CreateBoxTextBlock(
            visual?.AreaText ?? $"{NumberFormatting.FormatNumber(node.Area)} SF", visual?.ValueFont ?? 9.0, false, visual?.TextWidth));

        border.Child = stack;
        FixedPage.SetLeft(border, node.BoxX);
        FixedPage.SetTop(border, node.BoxY);
        page.Children.Add(border);
        AddOutsideTitle(page, node);
    }

    private static void RenderTree(FixedPage page, LayoutNode node, string rootName)
    {
        AddNodeBox(page, node, rootName);
        if (node.Children.Count == 0)
        {
            return;
        }

        var parentCenterX = node.BoxX + (node.BoxSize / 2.0);
        var parentBottomY = node.BoxY + node.BoxSize;
        var joinY = parentBottomY + (LevelVerticalGap.GetValueOrDefault(node.Level, 56.0) / 2.0);

        var childCenters = node.Children.Select(c => c.BoxX + (c.BoxSize / 2.0)).ToList();

        AddLine(page, parentCenterX, parentBottomY, parentCenterX, joinY, 1.0);
        AddLine(page, childCenters.Min(), joinY, childCenters.Max(), joinY, 1.0);

        foreach (var child in node.Children)
        {
            var childCenterX = child.BoxX + (child.BoxSize / 2.0);
            AddLine(page, childCenterX, joinY, childCenterX, child.BoxY, 1.0);
            RenderTree(page, child, rootName);
        }
    }

    private static void AddForest(
        FixedPage page,
        IReadOnlyList<SpaceSnapshotNode> roots,
        bool includeDistribution,
        double? forestTop = null,
        double? forestHeight = null,
        bool centerOnRoot = false)
    {
        var forestLeft = FrameLeft + 34.0;
        var forestWidth = FrameWidth - 68.0;
        var top = forestTop ?? (includeDistribution ? ForestTopWithBar : ForestTopNoBar);
        var height = forestHeight ?? (includeDistribution ? ForestHeightWithBar : ForestHeightNoBar);

        var layout = LayoutEngine.LayoutForest(roots, forestWidth, height);
        var laidOutRoots = layout.Roots;

        if (centerOnRoot && laidOutRoots.Count == 1)
        {
            var root = laidOutRoots[0];
            var targetCenterX = forestWidth / 2.0;
            var currentCenterX = root.BoxX + (root.BoxSize / 2.0);
            var deltaX = targetCenterX - currentCenterX;
            var (minX, maxX, _, _) = LayoutEngine.GetTreeBounds(root);
            if (minX + deltaX < 0.0)
            {
                deltaX += -(minX + deltaX);
            }

            if (maxX + deltaX > forestWidth)
            {
                deltaX -= maxX + deltaX - forestWidth;
            }

            LayoutEngine.OffsetTree(root, deltaX, 0.0);
        }

        foreach (var root in laidOutRoots)
        {
            LayoutEngine.OffsetTree(root, forestLeft, top);
            RenderTree(page, root, root.Name);
        }
    }

    private static FixedPage BuildSnapshotOverviewPage(SnapshotData snapshot, string projectName, bool includeDistribution)
    {
        var page = CreatePage();
        AddPageHeader(page, projectName);
        AddPageContentBorder(page);
        AddSnapshotSummary(page, snapshot.Title, snapshot.NetArea, snapshot.GrossArea);
        AddSectionHeading(page, "Program Overview");
        if (includeDistribution)
        {
            AddDistributionBar(page, snapshot.Roots, snapshot.NetArea);
        }

        AddFooter(page);
        return page;
    }

    private static FixedPage BuildSnapshotSectionPage(SnapshotData snapshot, string projectName, SpaceSnapshotNode root)
    {
        var page = CreatePage();
        AddPageHeader(page, projectName);
        AddPageContentBorder(page);
        AddSnapshotSummary(page, snapshot.Title, snapshot.NetArea, snapshot.GrossArea);
        AddSectionHeading(page, root.Name);
        AddForest(page, [root], false, FrameTop + 120.0, FrameHeight - 156.0, centerOnRoot: true);
        AddFooter(page);
        return page;
    }
}
