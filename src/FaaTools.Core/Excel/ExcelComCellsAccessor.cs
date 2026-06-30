using System.Globalization;

namespace FaaTools.Core.Excel;

/// <summary>
/// Live Excel COM-backed implementation of <see cref="IExcelCellsAccessor"/>, wrapping a
/// worksheet's Cells collection. Also exposes SetValue for writers (TargetTemplateWriter) -
/// reading and writing share the same cell-addressing logic, ported from
/// get_cell_value/cell_text and the write-side com_set(com_item(cells, row, col), "Value2", ...)
/// pattern in the existing scripts.
/// </summary>
public sealed class ExcelComCellsAccessor : IExcelCellsAccessor
{
    private readonly object _cells;

    internal ExcelComCellsAccessor(object cells, int usedRowCount, int usedColumnCount)
    {
        _cells = cells;
        UsedRowCount = usedRowCount;
        UsedColumnCount = usedColumnCount;
    }

    public int UsedRowCount { get; }

    public int UsedColumnCount { get; }

    public object? GetValue(int row, int col)
    {
        try
        {
            var cell = ExcelInterop.Item(_cells, row, col);
            return cell is null ? null : ExcelInterop.Get(cell, "Value2");
        }
        catch
        {
            return null;
        }
    }

    public string GetText(int row, int col)
    {
        var value = GetValue(row, col);
        if (value is null)
        {
            return string.Empty;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return text.Trim();
    }

    public void SetValue(int row, int col, object? value)
    {
        var cell = ExcelInterop.Item(_cells, row, col)
            ?? throw new InvalidOperationException($"Could not address cell (row {row}, col {col}).");
        ExcelInterop.Set(cell, "Value2", value);
    }
}
