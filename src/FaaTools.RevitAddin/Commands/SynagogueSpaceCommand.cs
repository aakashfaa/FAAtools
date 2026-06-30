using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FaaTools.Core.Logging;
using FaaTools.Core.Synagogue.Rendering;
using FaaTools.RevitAddin.Resources;
using FaaTools.RevitAddin.Synagogue;

namespace FaaTools.RevitAddin.Commands;

/// <summary>
/// "Synagogue Space": shows the 2-step wizard, then (depending on the chosen export option)
/// builds the PDF and/or creates Revit drafting views from the parsed workbook. Direct port of
/// the SynagogueSpaceWindow.on_generate flow from script.py, split so Revit API calls happen here
/// in Execute rather than inside the dialog's button handler.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class SynagogueSpaceCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uiApp = commandData.Application;
        var doc = uiApp.ActiveUIDocument?.Document;

        string defaultExcelPath;
        try
        {
            defaultExcelPath = TemplateResourceProvider.ExtractSynagogueMasterProgramTemplate();
        }
        catch (Exception ex)
        {
            Logger.Error("Synagogue Space: could not extract embedded template", ex);
            defaultExcelPath = string.Empty;
        }

        var window = new SynagogueWizardWindow(defaultExcelPath);
        new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;
        var dialogResult = window.ShowDialog();
        if (dialogResult != true || window.ParsedData is null || window.SelectedSpaceAllocation is null)
        {
            return Result.Cancelled;
        }

        var parsed = window.ParsedData;
        var selectedOption = window.SelectedSpaceAllocation;
        var includeDistribution = window.IncludeDistribution;
        var includeExisting = window.IncludeExisting;
        var includeRecommended = window.IncludeRecommended;
        var exportChoice = window.ExportChoice;
        var revitOutputChoice = window.RevitOutputChoice;

        var messages = new List<string>();

        try
        {
            if (exportChoice is "PDF" or "Both PDF and Revit")
            {
                var document = SynagoguePdfRenderer.BuildDocument(parsed, selectedOption, includeDistribution, includeExisting, includeRecommended);
                SynagoguePdfRenderer.PrintDocumentToPdf(document);
                messages.Add("PDF exported");
            }

            if (exportChoice is "Revit" or "Both PDF and Revit")
            {
                if (doc is null)
                {
                    TaskDialog.Show("Synagogue Space", "No active document - cannot create Revit views.");
                    return Result.Cancelled;
                }

                var createdViews = SynagogueRevitRenderer.CreateSectionViews(
                    doc, parsed, selectedOption, includeExisting, includeRecommended, revitOutputChoice ?? "Filled Region");
                messages.Add($"{createdViews.Count} Revit view(s) created");
            }

            TaskDialog.Show("Synagogue Space", string.Join(", ", messages) + ".");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            Logger.Error("Synagogue Space: export failed", ex);
            TaskDialog.Show("Synagogue Space", $"Export failed:\n{ex.Message}");
            return Result.Failed;
        }
    }
}
