using System.Globalization;
using FaaTools.Core.Excel;

namespace FaaTools.Core.Tests.Synagogue;

/// <summary>
/// In-memory IExcelCellsAccessor for unit tests - exists so SynagogueWorkbookParser can be
/// tested without a live Excel COM session.
/// </summary>
internal sealed class FakeExcelCellsAccessor : IExcelCellsAccessor
{
    private readonly Dictionary<(int Row, int Col), object?> _values = [];

    public required int UsedRowCount { get; init; }

    public required int UsedColumnCount { get; init; }

    public void Set(int row, int col, object? value) => _values[(row, col)] = value;

    public object? GetValue(int row, int col) => _values.GetValueOrDefault((row, col));

    public string GetText(int row, int col)
    {
        var value = GetValue(row, col);
        return value is null ? string.Empty : (Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
    }
}
