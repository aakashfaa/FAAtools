using System.IO;
using System.Reflection;

namespace FaaTools.RevitAddin.Resources;

/// <summary>
/// Extracts the embedded Synagogue Master Program template to a temp file. Both Synagogue Excel
/// (to seed a new project copy) and Synagogue Space (as the wizard's default file) use this -
/// embedding avoids "missing template file" issues if a loose file gets deleted from the install
/// folder. Isolated behind this one class so the extraction strategy can change later (e.g. to a
/// side-by-side file) without touching command logic.
/// </summary>
internal static class TemplateResourceProvider
{
    private const string TemplateResourceName =
        "FaaTools.RevitAddin.Resources.Templates.Synagogue Master Program spreadsheet.xls";

    public static string ExtractSynagogueMasterProgramTemplate()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(TemplateResourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {TemplateResourceName}");

        var tempDir = Path.Combine(Path.GetTempPath(), "FaaTools");
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, "Synagogue Master Program spreadsheet.xls");

        using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
        {
            stream.CopyTo(fileStream);
        }

        return tempPath;
    }
}
