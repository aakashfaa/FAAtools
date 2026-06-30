using System.IO;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FaaTools.Core.Excel;
using FaaTools.Core.Logging;
using FaaTools.Core.Naming;
using FaaTools.Core.RevitFiles;
using FaaTools.Core.SynagogueExcel;
using FaaTools.RevitAddin.Resources;
using FaaTools.RevitAddin.SynagogueExcel;
using FaaTools.RevitAddin.Wpf;

namespace FaaTools.RevitAddin.Commands;

/// <summary>
/// "Synagogue Excel" (renamed from "Custom Excel"): creates a per-project starter copy of the
/// Synagogue Master Program workbook in &lt;ProjectRoot&gt;\01 PROG-PD\Programming, with header
/// fields filled in. Direct port of Custom Excel.pushbutton\script.py's strict project-root flow,
/// combined with MSBA Excel's prompt-before-generate wizard UX pattern.
/// </summary>
[Transaction(TransactionMode.ReadOnly)]
public class SynagogueExcelCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uiApp = commandData.Application;
        var doc = uiApp.ActiveUIDocument?.Document;
        if (doc is null)
        {
            TaskDialog.Show("Synagogue Excel", "No active document.");
            return Result.Cancelled;
        }

        if (!ProjectInfoService.IsModelSavedLocally(doc))
        {
            TaskDialog.Show("Synagogue Excel", "Please save the project file first.");
            return Result.Cancelled;
        }

        if (ProjectInfoService.IsCloudModel(doc))
        {
            TaskDialog.Show("Synagogue Excel", "Not available for cloud models.");
            return Result.Cancelled;
        }

        var projectRoot = ProjectRootLocator.FindProjectRoot(doc.PathName);
        if (projectRoot is null)
        {
            TaskDialog.Show(
                "Synagogue Excel",
                $"Could not find a \"{ProjectRootLocator.DefaultSentinelFolder}\" folder above the project file.\n\n" +
                $"Expected structure:\n<Project Root>\\{ProjectRootLocator.DefaultSentinelFolder}\\...\n\n" +
                $"Searched up to {ProjectRootLocator.DefaultMaxLevels} levels above:\n{doc.PathName}");
            return Result.Cancelled;
        }

        var outputFolder = ProjectRootLocator.EnsureOutputFolder(projectRoot);

        string templatePath;
        try
        {
            templatePath = TemplateResourceProvider.ExtractSynagogueMasterProgramTemplate();
        }
        catch (Exception ex)
        {
            Logger.Error("Synagogue Excel: could not extract embedded template", ex);
            TaskDialog.Show("Synagogue Excel", $"Could not extract the workbook template.\n\n{ex.Message}");
            return Result.Failed;
        }

        var schema = TargetFieldSchemaProvider.LoadDefault();
        var projectName = ProjectInfoService.GetProjectName(doc);
        var defaults = new Dictionary<string, string>
        {
            ["ProjectName"] = projectName,
            ["ExportedOn"] = FileNaming.FormatDateForCell(DateTime.Now),
        };
        var projectNumber = ProjectInfoService.GetProjectNumber(doc);
        if (!string.IsNullOrEmpty(projectNumber))
        {
            defaults["JobNumber"] = projectNumber;
        }

        var window = new SynagogueExcelWindow(schema, defaults);
        new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;
        var dialogResult = window.ShowDialog();
        if (dialogResult != true || window.Values is null)
        {
            return Result.Cancelled;
        }

        var values = window.Values;
        var finalProjectName = values.TryGetValue("ProjectName", out var editedName) && !string.IsNullOrWhiteSpace(editedName)
            ? editedName
            : projectName;

        var destFileName = $"Synagogue Master Program - {FileNaming.SanitizeFilename(finalProjectName)}.xls";
        var destPath = Path.Combine(outputFolder, destFileName);

        if (File.Exists(destPath))
        {
            var overwrite = TaskDialog.Show(
                "Synagogue Excel",
                $"\"{destFileName}\" already exists in:\n{outputFolder}\n\nOverwrite it?",
                TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
            if (overwrite != TaskDialogResult.Yes)
            {
                return Result.Cancelled;
            }

            try
            {
                File.Delete(destPath);
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "Synagogue Excel",
                    $"Could not overwrite the existing file. Close it if it's open and try again.\n\n{ex.Message}");
                return Result.Failed;
            }
        }

        try
        {
            File.Copy(templatePath, destPath);
        }
        catch (Exception ex)
        {
            Logger.Error("Synagogue Excel: failed to copy template", ex);
            TaskDialog.Show("Synagogue Excel", $"Failed to copy the workbook template.\n\n{ex.Message}");
            return Result.Failed;
        }

        try
        {
            using var session = ExcelSession.Start();
            TargetTemplateWriter.Write(session, destPath, schema, values);
        }
        catch (Exception ex)
        {
            Logger.Error("Synagogue Excel: Excel write failed", ex);
            TaskDialog.Show(
                "Synagogue Excel",
                "The workbook was created, but writing the header values failed.\n\n" +
                $"File:\n{destPath}\n\nError:\n{ex.Message}\n\n" +
                "Most common causes:\n- Excel is not installed\n- Excel is blocked by policy/security\n- Another process has the file locked");
            return Result.Failed;
        }

        var completion = new CompletionDialog($"Created:\n{destPath}", destPath);
        new WindowInteropHelper(completion).Owner = uiApp.MainWindowHandle;
        completion.ShowDialog();

        return Result.Succeeded;
    }
}
