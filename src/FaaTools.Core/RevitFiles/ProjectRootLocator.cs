using System.IO;

namespace FaaTools.Core.RevitFiles;

/// <summary>
/// Ports find_project_root_from_model_path / safe_mkdir from Custom Excel.pushbutton\script.py.
/// </summary>
public static class ProjectRootLocator
{
    public const string DefaultSentinelFolder = "01 PROG-PD";
    public const int DefaultMaxLevels = 8;
    private const string DefaultOutputSubfolder = "01 PROG-PD\\Programming";

    /// <summary>
    /// Walks up from the Revit model's folder (up to maxLevels parents) looking for a directory
    /// that directly contains the sentinel folder. Returns that directory (the project root), or
    /// null if not found - callers should hard-abort with guidance, matching the existing
    /// strict Custom Excel behavior (no fallback to the model's own folder).
    /// </summary>
    public static string? FindProjectRoot(
        string modelPath,
        string sentinelFolder = DefaultSentinelFolder,
        int maxLevels = DefaultMaxLevels)
    {
        var current = Path.GetDirectoryName(modelPath);
        for (var level = 0; level < maxLevels && current is not null; level++)
        {
            if (Directory.Exists(Path.Combine(current, sentinelFolder)))
            {
                return current;
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    public static string EnsureOutputFolder(string projectRoot, string relativeSubfolder = DefaultOutputSubfolder)
    {
        var outputFolder = Path.Combine(projectRoot, relativeSubfolder);
        Directory.CreateDirectory(outputFolder);
        return outputFolder;
    }
}
