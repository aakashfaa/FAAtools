using System.Windows;
using System.Windows.Controls;
using FaaTools.Core.SynagogueExcel;

namespace FaaTools.RevitAddin.SynagogueExcel;

/// <summary>
/// Prompt-before-generate wizard, modeled on MSBA Excel's UX pattern (a form shown before
/// writing) but rendering whatever fields TargetFieldSchema defines instead of hardcoding one
/// panel per field - the schema is expected to grow over time without needing UI changes here.
/// </summary>
public partial class SynagogueExcelWindow : Window
{
    private readonly TargetFieldSchema _schema;
    private readonly Dictionary<string, TextBox> _fieldInputs = [];

    public IReadOnlyDictionary<string, string>? Values { get; private set; }

    public SynagogueExcelWindow(TargetFieldSchema schema, IReadOnlyDictionary<string, string> initialValues)
    {
        InitializeComponent();
        _schema = schema;
        BuildFields(initialValues);
    }

    private void BuildFields(IReadOnlyDictionary<string, string> initialValues)
    {
        foreach (var field in _schema.Fields)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = field.Required ? $"{field.Label} *" : field.Label,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            var textBox = new TextBox
            {
                Height = 26,
                VerticalContentAlignment = VerticalAlignment.Center,
                Text = initialValues.TryGetValue(field.Key, out var existing) ? existing : string.Empty,
            };
            Grid.SetColumn(textBox, 1);
            row.Children.Add(textBox);

            _fieldInputs[field.Key] = textBox;
            FieldsPanel.Children.Add(row);
        }
    }

    private void OnGenerate(object sender, RoutedEventArgs e)
    {
        var values = new Dictionary<string, string>();
        var missing = new List<string>();

        foreach (var field in _schema.Fields)
        {
            var text = _fieldInputs[field.Key].Text?.Trim() ?? string.Empty;
            if (field.Required && string.IsNullOrEmpty(text))
            {
                missing.Add(field.Label);
                continue;
            }

            if (!string.IsNullOrEmpty(text))
            {
                values[field.Key] = text;
            }
        }

        if (missing.Count > 0)
        {
            MessageBox.Show(
                this,
                "Please fill in:\n- " + string.Join("\n- ", missing),
                "Synagogue Excel",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Values = values;
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
