namespace FaaTools.Core.Excel;

/// <summary>
/// A single open workbook within an <see cref="ExcelSession"/>. Tracks every COM object handed
/// out via <see cref="GetSheet"/> so Dispose can release them all, mirroring close_excel_workbook's
/// reverse-order release of cells/used_range/sheet/sheets/workbook.
/// </summary>
public sealed class ExcelWorkbookHandle : IDisposable
{
    private readonly object _workbook;
    private readonly object _sheets;
    private readonly List<object> _trackedComObjects = [];
    private bool _disposed;
    private bool _closed;

    internal ExcelWorkbookHandle(object workbook)
    {
        _workbook = workbook;
        _sheets = ExcelInterop.Get(workbook, "Sheets")
            ?? throw new InvalidOperationException("Could not access the workbook's Sheets collection.");
    }

    /// <summary>
    /// Resolves a worksheet by name, falling back to the first sheet if the named sheet doesn't
    /// exist - direct port of open_excel_workbook's try sheet-by-name / except sheet-index-1 logic.
    /// </summary>
    public ExcelComCellsAccessor GetSheet(string preferredSheetName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        object sheet;
        try
        {
            sheet = ExcelInterop.Item(_sheets, preferredSheetName)
                ?? throw new InvalidOperationException("Sheet lookup by name returned null.");
        }
        catch
        {
            sheet = ExcelInterop.Item(_sheets, 1)
                ?? throw new InvalidOperationException("Workbook has no sheets.");
        }
        _trackedComObjects.Add(sheet);

        var usedRange = ExcelInterop.Get(sheet, "UsedRange")
            ?? throw new InvalidOperationException("Could not read the worksheet's UsedRange.");
        _trackedComObjects.Add(usedRange);

        var rows = ExcelInterop.Get(usedRange, "Rows")!;
        var columns = ExcelInterop.Get(usedRange, "Columns")!;
        _trackedComObjects.Add(rows);
        _trackedComObjects.Add(columns);

        var usedRowCount = Convert.ToInt32(ExcelInterop.Get(rows, "Count"));
        var usedColumnCount = Convert.ToInt32(ExcelInterop.Get(columns, "Count"));

        var cells = ExcelInterop.Get(sheet, "Cells")
            ?? throw new InvalidOperationException("Could not access the worksheet's Cells collection.");
        _trackedComObjects.Add(cells);

        return new ExcelComCellsAccessor(cells, usedRowCount, usedColumnCount);
    }

    public void Save() => ExcelInterop.Call(_workbook, "Save");

    /// <summary>Closes the workbook. Safe to call more than once (e.g. once explicitly, once via Dispose).</summary>
    public void Close(bool saveChanges = false)
    {
        if (_closed)
        {
            return;
        }

        try
        {
            ExcelInterop.Call(_workbook, "Close", saveChanges);
        }
        finally
        {
            _closed = true;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Close(saveChanges: false);

        for (var i = _trackedComObjects.Count - 1; i >= 0; i--)
        {
            ExcelInterop.Release(_trackedComObjects[i]);
        }
        _trackedComObjects.Clear();

        ExcelInterop.Release(_sheets);
        ExcelInterop.Release(_workbook);
    }
}
