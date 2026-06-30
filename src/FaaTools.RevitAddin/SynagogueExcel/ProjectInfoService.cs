using Autodesk.Revit.DB;

namespace FaaTools.RevitAddin.SynagogueExcel;

/// <summary>Ports get_project_name plus the model-saved/cloud guards from script.py.</summary>
internal static class ProjectInfoService
{
    public static string GetProjectName(Document doc)
    {
        try
        {
            var name = doc.ProjectInformation?.Name;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }
        }
        catch
        {
            // fall through
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(doc.Title))
            {
                return doc.Title.Trim();
            }
        }
        catch
        {
            // fall through
        }

        return "Unnamed Project";
    }

    public static string? GetProjectNumber(Document doc)
    {
        try
        {
            var number = doc.ProjectInformation?.Number;
            return string.IsNullOrWhiteSpace(number) ? null : number.Trim();
        }
        catch
        {
            return null;
        }
    }

    public static bool IsModelSavedLocally(Document doc)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(doc.PathName);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Defensive try/catch around IsModelInCloud, matching the existing scripts - this property's
    /// availability has historically varied across Revit versions.
    /// </summary>
    public static bool IsCloudModel(Document doc)
    {
        try
        {
            return doc.IsModelInCloud;
        }
        catch
        {
            return false;
        }
    }
}
