namespace FaaTools.Core.Excel;

/// <summary>
/// Read-only view over a single worksheet's cells (1-based row/col, matching Excel's own
/// addressing). Exists so SynagogueWorkbookParser can be unit-tested against a fake
/// implementation without needing a live Excel COM session; production code uses
/// <see cref="ExcelComCellsAccessor"/>.
/// </summary>
public interface IExcelCellsAccessor
{
    int UsedRowCount { get; }

    int UsedColumnCount { get; }

    /// <summary>Raw cell value (Value2 semantics: numbers as double, text as string, empty as null).</summary>
    object? GetValue(int row, int col);

    /// <summary>Trimmed string form of the cell value, or "" if empty. Mirrors cell_text().</summary>
    string GetText(int row, int col);
}
