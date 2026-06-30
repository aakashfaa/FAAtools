namespace FaaTools.Core.Excel;

/// <summary>
/// Disposable wrapper around a late-bound Excel.Application COM session. Mirrors the existing
/// scripts' open/close discipline exactly: Visible=false, DisplayAlerts=false, and a guaranteed
/// Quit + FinalReleaseComObject cleanup (via Dispose, called from a finally block by callers)
/// even when something throws mid-session.
/// </summary>
public sealed class ExcelSession : IDisposable
{
    private object? _excel;
    private object? _workbooks;
    private bool _disposed;

    private ExcelSession(object excel, object workbooks)
    {
        _excel = excel;
        _workbooks = workbooks;
    }

    /// <summary>
    /// Starts a new hidden Excel instance. Throws if Excel isn't installed - callers should
    /// surface this as a clear, actionable message (mirrors "Excel not installed or
    /// Excel.Application ProgID unavailable." from the existing scripts).
    /// </summary>
    public static ExcelSession Start()
    {
        var excelType = Type.GetTypeFromProgID("Excel.Application")
            ?? throw new InvalidOperationException("Excel is not installed, or the Excel.Application ProgID is unavailable.");

        object excel;
        try
        {
            excel = Activator.CreateInstance(excelType)
                ?? throw new InvalidOperationException("Could not start an Excel.Application instance.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Could not start Excel.", ex);
        }

        try
        {
            ExcelInterop.Set(excel, "Visible", false);
            ExcelInterop.Set(excel, "DisplayAlerts", false);
            var workbooks = ExcelInterop.Get(excel, "Workbooks")
                ?? throw new InvalidOperationException("Could not access Excel's Workbooks collection.");
            return new ExcelSession(excel, workbooks);
        }
        catch
        {
            ExcelInterop.Release(excel);
            throw;
        }
    }

    /// <summary>Opens a workbook. Caller must Dispose the returned handle (it does not auto-save).</summary>
    public ExcelWorkbookHandle Open(string path, bool readOnly = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var workbook = ExcelInterop.Call(_workbooks!, "Open", path, Type.Missing, readOnly)
            ?? throw new InvalidOperationException($"Excel could not open workbook: {path}");
        return new ExcelWorkbookHandle(workbook);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (_excel is not null)
            {
                ExcelInterop.Call(_excel, "Quit");
            }
        }
        catch
        {
            // best-effort; we still release the COM references below
        }

        ExcelInterop.Release(_workbooks);
        ExcelInterop.Release(_excel);
        _workbooks = null;
        _excel = null;
    }
}
