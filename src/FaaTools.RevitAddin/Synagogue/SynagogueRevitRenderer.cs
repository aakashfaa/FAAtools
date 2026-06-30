using Autodesk.Revit.DB;
using FaaTools.Core.Synagogue;
using FaaTools.Core.Synagogue.Layout;
using FaaTools.Core.Synagogue.Model;
using FaaTools.Core.Synagogue.Rendering;
using static FaaTools.Core.Synagogue.Layout.SynagogueLayoutConstants;
using RevitColor = Autodesk.Revit.DB.Color;
using RevitLine = Autodesk.Revit.DB.Line;

namespace FaaTools.RevitAddin.Synagogue;

/// <summary>
/// Creates Revit drafting views (FilledRegion + TextNote, replicating the same treemap layout as
/// the PDF) for each requested snapshot/root combination. Direct port of script.py lines
/// ~1381-1638 (get_drafting_view_type through create_revit_section_views). Consumes the same
/// LayoutNode tree LayoutEngine produces for the PDF renderer.
///
/// Only "Filled Region" output is implemented, matching current behavior - "Unbounded Room" and
/// "Massing blocks" are left out of the wizard's combo box entirely rather than raising a runtime
/// not-implemented error.
/// </summary>
internal static class SynagogueRevitRenderer
{
    public static IReadOnlyList<ViewDrafting> CreateSectionViews(
        Document doc,
        ParsedWorkbook parsed,
        string selectedOptionLabel,
        bool includeExisting,
        bool includeRecommended,
        string revitOutputChoice)
    {
        if (revitOutputChoice != "Filled Region")
        {
            throw new InvalidOperationException($"\"{revitOutputChoice}\" is not implemented yet. Use \"Filled Region\" for now.");
        }

        var snapshots = SynagogueSnapshotBuilder.BuildRequestedSnapshots(parsed, selectedOptionLabel, includeExisting, includeRecommended);

        var draftingViewType = GetDraftingViewType(doc)
            ?? throw new InvalidOperationException("No drafting view type was found in this Revit project.");
        var regionType = GetFilledRegionType(doc)
            ?? throw new InvalidOperationException("No filled region type was found in this Revit project.");
        var textType = GetTextNoteType(doc)
            ?? throw new InvalidOperationException("No text note type was found in this Revit project.");

        var createdViews = new List<ViewDrafting>();

        using var transaction = new Transaction(doc, "Create Synagogue Space Summary Views");
        transaction.Start();
        try
        {
            foreach (var snapshot in snapshots)
            {
                foreach (var root in snapshot.Roots)
                {
                    var viewName = UniqueViewName(doc, $"Synagogue Space - {snapshot.Title} - {root.Name}");
                    var view = ViewDrafting.Create(doc, draftingViewType.Id);
                    view.Name = viewName;

                    var forestWidth = FrameWidth - 68.0;
                    var forestHeight = FrameHeight - 156.0;
                    var layout = LayoutEngine.LayoutForest([root], forestWidth, forestHeight);
                    var laidOutRoot = layout.Roots[0];

                    var targetCenterX = forestWidth / 2.0;
                    var currentCenterX = laidOutRoot.BoxX + (laidOutRoot.BoxSize / 2.0);
                    var deltaX = targetCenterX - currentCenterX;
                    var (minX, maxX, _, _) = LayoutEngine.GetTreeBounds(laidOutRoot);
                    if (minX + deltaX < 0.0)
                    {
                        deltaX += -(minX + deltaX);
                    }

                    if (maxX + deltaX > forestWidth)
                    {
                        deltaX -= maxX + deltaX - forestWidth;
                    }

                    LayoutEngine.OffsetTree(laidOutRoot, deltaX + FrameLeft + 34.0, FrameTop + 120.0);

                    RenderRevitTree(doc, view, regionType, textType.Id, laidOutRoot, laidOutRoot.Name);
                    createdViews.Add(view);
                }
            }

            transaction.Commit();
        }
        catch
        {
            if (transaction.HasStarted() && !transaction.HasEnded())
            {
                transaction.RollBack();
            }

            throw;
        }

        return createdViews;
    }

    private static ViewFamilyType? GetDraftingViewType(Document doc)
        => new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .FirstOrDefault(vt => vt.ViewFamily == ViewFamily.Drafting);

    private static string UniqueViewName(Document doc, string baseName)
    {
        var existing = new FilteredElementCollector(doc).OfClass(typeof(ViewDrafting)).Cast<ViewDrafting>()
            .Select(v => v.Name)
            .ToHashSet();

        var name = baseName;
        var index = 1;
        while (existing.Contains(name))
        {
            name = $"{baseName} {index}";
            index++;
        }

        return name;
    }

    private static FilledRegionType? GetFilledRegionType(Document doc)
        => new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>().FirstOrDefault();

    private static TextNoteType? GetTextNoteType(Document doc)
        => new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).Cast<TextNoteType>().FirstOrDefault();

    /// <summary>
    /// The literal BuiltInParameter the original script falls back to (FOREGROUND_PATTERN_ID)
    /// doesn't exist in the current Revit API - FOREGROUND_ANY_PATTERN_ID_PARAM is the closest
    /// equivalent and is used here instead. In practice this fallback is rarely reached since
    /// FilledRegionType.ForegroundPatternId (the primary path) is a valid property on current API
    /// versions.
    /// </summary>
    private static ElementId GetRegionPatternId(FilledRegionType regionType)
    {
        try
        {
            return regionType.ForegroundPatternId;
        }
        catch
        {
            // fall through
        }

        try
        {
            var parameter = regionType.get_Parameter(BuiltInParameter.FOREGROUND_ANY_PATTERN_ID_PARAM);
            if (parameter is not null)
            {
                return parameter.AsElementId();
            }
        }
        catch
        {
            // fall through
        }

        return ElementId.InvalidElementId;
    }

    /// <summary>
    /// The original script also attempted SetProjectionFillPatternId/SetProjectionFillColor as a
    /// fallback if the "surface foreground" overrides failed - those methods don't exist on
    /// OverrideGraphicSettings in the current Revit API (likely obsolete pre-2018 API names), so
    /// that fallback is dropped; SetSurfaceForegroundPattern* is the correct, current API.
    /// </summary>
    private static void ApplyRegionColor(View view, FilledRegionType regionType, ElementId regionId, string hexColor)
    {
        var ogs = new OverrideGraphicSettings();
        var patternId = GetRegionPatternId(regionType);
        if (patternId is not null && patternId != ElementId.InvalidElementId)
        {
            try
            {
                ogs.SetSurfaceForegroundPatternId(patternId);
            }
            catch
            {
                // best-effort, matches the original script
            }
        }

        var wpfColor = WpfColorHelper.ColorFromHex(hexColor);
        var revitColor = new RevitColor(wpfColor.R, wpfColor.G, wpfColor.B);
        try
        {
            ogs.SetSurfaceForegroundPatternColor(revitColor);
        }
        catch
        {
            // best-effort
        }

        view.SetElementOverrides(regionId, ogs);
    }

    private static XYZ PageToRevitXy(double pageX, double pageY)
        => new(pageX * RevitScale, -pageY * RevitScale, 0.0);

    private static double GetShortCurveTolerance(Document doc)
    {
        try
        {
            return doc.Application.ShortCurveTolerance;
        }
        catch
        {
            return 0.003;
        }
    }

    private static double PageSegmentLength(double x1, double y1, double x2, double y2)
        => Math.Sqrt(((x2 - x1) * (x2 - x1)) + ((y2 - y1) * (y2 - y1))) * RevitScale;

    private static bool IsSegmentLongEnough(Document doc, double x1, double y1, double x2, double y2, double factor = 1.05)
        => PageSegmentLength(x1, y1, x2, y2) > (GetShortCurveTolerance(doc) * factor);

    private static void AddDetailLine(Document doc, View view, double x1, double y1, double x2, double y2)
    {
        if (!IsSegmentLongEnough(doc, x1, y1, x2, y2))
        {
            return;
        }

        var curve = RevitLine.CreateBound(PageToRevitXy(x1, y1), PageToRevitXy(x2, y2));
        doc.Create.NewDetailCurve(view, curve);
    }

    /// <summary>
    /// Port of add_revit_text - the original script's font_size parameter was accepted but never
    /// actually applied (text size is governed by the TextNoteType), so it's dropped here rather
    /// than carried forward as a no-op argument.
    /// </summary>
    private static void AddRevitText(Document doc, View view, double x, double y, double width, string text, ElementId textTypeId)
    {
        var options = new TextNoteOptions(textTypeId) { HorizontalAlignment = HorizontalTextAlignment.Center };
        TextNote.Create(doc, view.Id, PageToRevitXy(x, y), width * RevitScale, text, options);
    }

    private static void CreateRevitBox(Document doc, View view, FilledRegionType regionType, LayoutNode node, string rootName, ElementId textTypeId)
    {
        var x1 = node.BoxX;
        var y1 = node.BoxY;
        var x2 = x1 + node.BoxSize;
        var y2 = y1 + node.BoxSize;

        if (IsSegmentLongEnough(doc, x1, y1, x2, y1)
            && IsSegmentLongEnough(doc, x2, y1, x2, y2)
            && IsSegmentLongEnough(doc, x2, y2, x1, y2)
            && IsSegmentLongEnough(doc, x1, y2, x1, y1))
        {
            var loop = new CurveLoop();
            loop.Append(RevitLine.CreateBound(PageToRevitXy(x1, y1), PageToRevitXy(x2, y1)));
            loop.Append(RevitLine.CreateBound(PageToRevitXy(x2, y1), PageToRevitXy(x2, y2)));
            loop.Append(RevitLine.CreateBound(PageToRevitXy(x2, y2), PageToRevitXy(x1, y2)));
            loop.Append(RevitLine.CreateBound(PageToRevitXy(x1, y2), PageToRevitXy(x1, y1)));

            var loops = new List<CurveLoop> { loop };
            var region = FilledRegion.Create(doc, regionType.Id, view.Id, loops);
            ApplyRegionColor(view, regionType, region.Id, ColorAndLabelCatalog.GetRootColorHex(rootName));
        }

        var visual = node.Visual;
        var textLines = new List<string>();
        if (visual is not { TitleOutside: true })
        {
            textLines.Add(node.Name);
        }

        if (!string.IsNullOrEmpty(visual?.CountText))
        {
            textLines.Add(visual.CountText);
        }

        textLines.Add(visual?.AreaText ?? $"{NumberFormatting.FormatNumber(node.Area)} SF");
        var text = string.Join("\n", textLines);

        AddRevitText(doc, view, x1 + 2.0, y1 + (node.BoxSize * 0.35), Math.Max(12.0, node.BoxSize - 4.0), text, textTypeId);

        if (visual is { TitleOutside: true })
        {
            var outsideWidth = visual.OutsideWidth;
            var labelX = x1 + (node.BoxSize / 2.0) - (outsideWidth / 2.0);
            var labelY = visual.OutsidePosition == "above"
                ? y1 - visual.OutsideHeight - OutsideLabelGap - 2.0
                : y2 + OutsideLabelGap;
            AddRevitText(doc, view, labelX, labelY, outsideWidth, node.Name, textTypeId);
        }
    }

    private static void RenderRevitTree(Document doc, View view, FilledRegionType regionType, ElementId textTypeId, LayoutNode node, string rootName)
    {
        CreateRevitBox(doc, view, regionType, node, rootName, textTypeId);
        if (node.Children.Count == 0)
        {
            return;
        }

        var parentCenterX = node.BoxX + (node.BoxSize / 2.0);
        var parentBottomY = node.BoxY + node.BoxSize;
        var joinY = parentBottomY + (LevelVerticalGap.GetValueOrDefault(node.Level, 56.0) / 2.0);
        var childCenters = node.Children.Select(c => c.BoxX + (c.BoxSize / 2.0)).ToList();

        AddDetailLine(doc, view, parentCenterX, parentBottomY, parentCenterX, joinY);
        if (childCenters.Count > 1)
        {
            AddDetailLine(doc, view, childCenters.Min(), joinY, childCenters.Max(), joinY);
        }

        foreach (var child in node.Children)
        {
            var childCenterX = child.BoxX + (child.BoxSize / 2.0);
            AddDetailLine(doc, view, childCenterX, joinY, childCenterX, child.BoxY);
            RenderRevitTree(doc, view, regionType, textTypeId, child, rootName);
        }
    }
}
