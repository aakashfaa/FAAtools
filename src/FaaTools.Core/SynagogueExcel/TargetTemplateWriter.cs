using FaaTools.Core.Excel;

namespace FaaTools.Core.SynagogueExcel;

/// <summary>
/// Writes a set of field values into a copied Synagogue Master Program workbook, per the schema
/// in TargetFieldSchema.json. Direct port of excel_write_headers's open/write/save/close
/// discipline (the caller is expected to run this inside the same try/finally-guaranteed
/// ExcelSession disposal that the existing scripts use).
/// </summary>
public static class TargetTemplateWriter
{
    public static void Write(
        ExcelSession session,
        string workbookPath,
        TargetFieldSchema schema,
        IReadOnlyDictionary<string, string> values)
    {
        using var workbook = session.Open(workbookPath, readOnly: false);
        var cells = workbook.GetSheet(schema.SheetName);

        foreach (var field in schema.Fields)
        {
            if (!values.TryGetValue(field.Key, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
            {
                if (field.Required)
                {
                    throw new InvalidOperationException($"Missing required field: {field.Label}");
                }

                continue;
            }

            var textToWrite = field.Kind == TargetFieldKind.DateCombinedText
                ? string.Format(field.TextFormat ?? "{0}", rawValue)
                : rawValue;

            cells.SetValue(field.Row, field.Col, textToWrite);
        }

        workbook.Save();
        // workbook.Dispose() (end of using) closes without re-saving - already saved above.
    }
}
