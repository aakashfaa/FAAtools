namespace FaaTools.Core.Synagogue.Layout;

/// <summary>
/// Geometry/scale constants, ported verbatim from script.py (lines ~59-117). Shared by
/// LayoutEngine (sizing/positioning) and the PDF/Revit renderers (page/frame layout).
/// </summary>
public static class SynagogueLayoutConstants
{
    public const double RevitScale = 0.02;

    // ISO A3 landscape at 96 DPI
    public const double PageWidth = 1587.4;
    public const double PageHeight = 1122.5;
    public const double MarginLeft = 62.0;
    public const double MarginRight = 62.0;
    public const double MarginTop = 48.0;
    public const double MarginBottom = 54.0;

    public const double FrameLeft = 60.0;
    public const double FrameTop = 160.0;
    public const double FrameWidth = PageWidth - (2.0 * FrameLeft);
    public const double FrameHeight = PageHeight - FrameTop - 64.0;

    public const double HeaderTitleSize = 54;
    public const double HeaderSubtitleSize = 24;
    public const double SummaryTitleSize = 36;
    public const double SummaryTextSize = 18;
    public const double FooterTextSize = 14;

    public const double ForestTopWithBar = FrameTop + 340.0;
    public const double ForestTopNoBar = FrameTop + 150.0;
    public const double ForestHeightWithBar = FrameHeight - 380.0;
    public const double ForestHeightNoBar = FrameHeight - 180.0;

    public const double RootGap = 34.0;
    public const double SiblingGap = 16.0;

    public static readonly IReadOnlyDictionary<int, double> LevelVerticalGap = new Dictionary<int, double>
    {
        [1] = 78.0,
        [2] = 64.0,
    };

    public static readonly IReadOnlyDictionary<int, (double Lower, double Upper)> SizeBounds =
        new Dictionary<int, (double, double)>
        {
            [1] = (79.2, 162.8),
            [2] = (44.0, 129.8),
            [3] = (30.8, 101.2),
        };

    public const double NodeTextPadding = 4.0;
    public const double NodeTextGap = 3.0;
    public const double OutsideLabelGap = 6.0;
    public const double OutsideLabelMaxWidth = 200.0;

    public const string FaaOrangeHex = "#F26F21";
    public const string TextHex = "#2E3345";
    public const string BorderHex = "#AEB4BF";
}
