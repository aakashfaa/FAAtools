using Autodesk.Revit.UI;
using FaaTools.Core.Logging;
using FaaTools.RevitAddin.Ribbon;

namespace FaaTools.RevitAddin;

/// <summary>
/// Add-in entry point. Replaces pyRevit's bundle.yaml-driven ribbon with an imperative one built
/// by RibbonBuilder, on a distinctly-named tab so it doesn't collide with pyRevit's "FAA [NEW]"
/// tab while both run side-by-side during the migration.
/// </summary>
public class FaaToolsApplication : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            RibbonBuilder.BuildRibbon(application);
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            Logger.Error("FaaToolsApplication.OnStartup failed", ex);
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
}
