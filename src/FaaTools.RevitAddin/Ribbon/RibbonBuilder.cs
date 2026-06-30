using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using FaaTools.RevitAddin.Commands;

namespace FaaTools.RevitAddin.Ribbon;

/// <summary>
/// Builds the native ribbon, replacing pyRevit's bundle.yaml tree. Mirrors the original panel/
/// pulldown names ("Connect To Excel" > "1. Create Targets", "Visual Aid" > "Space Summary") so
/// the migrated buttons sit where users already expect them, even though only one button is
/// migrated into each pulldown so far - the rest of those pulldowns' siblings stay on pyRevit
/// for now (future phases).
/// </summary>
internal static class RibbonBuilder
{
    private const string TabName = "FAA Tools (Native)";

    public static void BuildRibbon(UIControlledApplication application)
    {
        application.CreateRibbonTab(TabName);

        var connectToExcelPanel = application.CreateRibbonPanel(TabName, "Connect To Excel");
        AddSingleButtonPulldown(
            connectToExcelPanel,
            pulldownName: "CreateTargetsPulldown",
            pulldownText: "1. Create Targets",
            iconResourceName: "SynagogueExcel.png",
            buttonName: "SynagogueExcelButton",
            buttonText: "Synagogue\nExcel",
            commandTypeFullName: typeof(SynagogueExcelCommand).FullName!,
            tooltip: "Creates a starter copy of the Synagogue Master Program workbook for this project.");

        var visualAidPanel = application.CreateRibbonPanel(TabName, "Visual Aid");
        AddSingleButtonPulldown(
            visualAidPanel,
            pulldownName: "SpaceSummaryPulldown",
            pulldownText: "Space Summary",
            iconResourceName: "SynagogueSpace.png",
            buttonName: "SynagogueSpaceButton",
            buttonText: "Synagogue\nSpace",
            commandTypeFullName: typeof(SynagogueSpaceCommand).FullName!,
            tooltip: "Generates the synagogue space programming PDF/Revit views from a filled-in master program workbook.");
    }

    private static void AddSingleButtonPulldown(
        RibbonPanel panel,
        string pulldownName,
        string pulldownText,
        string iconResourceName,
        string buttonName,
        string buttonText,
        string commandTypeFullName,
        string tooltip)
    {
        var icon = LoadEmbeddedIcon(iconResourceName);

        var pulldownData = new PulldownButtonData(pulldownName, pulldownText)
        {
            LargeImage = icon,
        };
        var pulldown = (PulldownButton)panel.AddItem(pulldownData);

        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var buttonData = new PushButtonData(buttonName, buttonText, assemblyLocation, commandTypeFullName)
        {
            ToolTip = tooltip,
            LargeImage = icon,
        };
        pulldown.AddPushButton(buttonData);
    }

    private static BitmapImage? LoadEmbeddedIcon(string iconFileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"FaaTools.RevitAddin.Resources.Icons.{iconFileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
