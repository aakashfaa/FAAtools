using System.IO;
using System.Windows;
using System.Windows.Controls;
using FaaTools.Core.Excel;
using FaaTools.Core.Synagogue;
using FaaTools.Core.Synagogue.Model;
using Microsoft.Win32;

namespace FaaTools.RevitAddin.Synagogue;

/// <summary>
/// 2-step wizard: Step 1 selects the workbook, Step 2 configures the space allocation and export
/// options. Direct port of window.xaml + the SynagogueSpaceWindow class from script.py, with one
/// behavioral change: this window only collects input and parses the workbook - it does not
/// generate the PDF/Revit output itself (SynagogueSpaceCommand does that after ShowDialog
/// returns), keeping Revit API calls out of dialog event handlers.
/// </summary>
public partial class SynagogueWizardWindow : Window
{
    private static readonly string[] ExportOptions = ["PDF", "Revit", "Both PDF and Revit"];

    // Only "Filled Region" is actually implemented in SynagogueRevitRenderer - left out of the
    // list entirely rather than showing a "not implemented" error if picked, per the plan's UX note.
    private static readonly string[] RevitOutputOptions = ["Filled Region"];

    public ParsedWorkbook? ParsedData { get; private set; }

    public string? SelectedSpaceAllocation { get; private set; }

    public bool IncludeDistribution { get; private set; }

    public bool IncludeExisting { get; private set; }

    public bool IncludeRecommended { get; private set; }

    public string? ExportChoice { get; private set; }

    public string? RevitOutputChoice { get; private set; }

    public SynagogueWizardWindow(string defaultExcelPath)
    {
        InitializeComponent();

        if (File.Exists(defaultExcelPath))
        {
            fileText.Text = defaultExcelPath;
        }

        exportCombo.ItemsSource = ExportOptions;
        exportCombo.SelectedIndex = 0;
        revitOutputCombo.ItemsSource = RevitOutputOptions;
        revitOutputCombo.SelectedIndex = 0;
        exportCombo.SelectionChanged += OnExportChanged;

        secondStepPanel.Visibility = Visibility.Collapsed;
        backButton.Visibility = Visibility.Collapsed;
        generateButton.Visibility = Visibility.Collapsed;
        UpdateExportVisibility();
    }

    private void OnExportChanged(object sender, SelectionChangedEventArgs e) => UpdateExportVisibility();

    private void UpdateExportVisibility()
    {
        var exportChoice = exportCombo.SelectedItem as string;
        var showRevit = exportChoice is "Revit" or "Both PDF and Revit";
        var showPdf = exportChoice is "PDF" or "Both PDF and Revit";

        revitOutputLabel.Visibility = showRevit ? Visibility.Visible : Visibility.Collapsed;
        revitOutputCombo.Visibility = showRevit ? Visibility.Visible : Visibility.Collapsed;
        pdfOptionsLabel.Visibility = showPdf ? Visibility.Visible : Visibility.Collapsed;
        distributionCheck.Visibility = showPdf ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Excel Workbook (*.xls;*.xlsx)|*.xls;*.xlsx",
            Title = "Select Synagogue Master Program workbook",
        };
        if (dialog.ShowDialog(this) == true)
        {
            fileText.Text = dialog.FileName;
        }
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        var excelPath = fileText.Text;
        if (string.IsNullOrWhiteSpace(excelPath) || !File.Exists(excelPath))
        {
            MessageBox.Show(this, "Please select a valid Excel file.", "Synagogue Space", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var session = ExcelSession.Start();
            using var workbook = session.Open(excelPath, readOnly: true);
            var cells = workbook.GetSheet(SynagogueWorkbookParser.DefaultSheetName);
            ParsedData = SynagogueWorkbookParser.Parse(cells);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not read the workbook:\n{ex.Message}", "Synagogue Space", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        projectNameValue.Text = ParsedData.ProjectName;
        var optionLabels = ParsedData.Options.Select(o => o.Label).ToList();
        spaceAllocationCombo.ItemsSource = optionLabels;
        spaceAllocationCombo.SelectedIndex = optionLabels.Count > 0 ? 0 : -1;

        distributionCheck.IsChecked = true;
        existingSnapshotCheck.IsChecked = optionLabels.Contains("EXISTING");
        recommendedSnapshotCheck.IsChecked = optionLabels.Count > 1;

        firstStepPanel.Visibility = Visibility.Collapsed;
        secondStepPanel.Visibility = Visibility.Visible;
        nextButton.Visibility = Visibility.Collapsed;
        backButton.Visibility = Visibility.Visible;
        generateButton.Visibility = Visibility.Visible;
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        secondStepPanel.Visibility = Visibility.Collapsed;
        firstStepPanel.Visibility = Visibility.Visible;
        nextButton.Visibility = Visibility.Visible;
        backButton.Visibility = Visibility.Collapsed;
        generateButton.Visibility = Visibility.Collapsed;
    }

    private void OnGenerate(object sender, RoutedEventArgs e)
    {
        if (ParsedData is null)
        {
            MessageBox.Show(this, "Load a workbook first.", "Synagogue Space", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (spaceAllocationCombo.SelectedItem is not string selectedOption)
        {
            MessageBox.Show(this, "Please select a space allocation.", "Synagogue Space", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var includeExisting = existingSnapshotCheck.IsChecked == true;
        var includeRecommended = recommendedSnapshotCheck.IsChecked == true;
        if (!includeExisting && !includeRecommended)
        {
            MessageBox.Show(this, "Select at least one snapshot option before generating.", "Synagogue Space", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedSpaceAllocation = selectedOption;
        IncludeDistribution = distributionCheck.IsChecked == true;
        IncludeExisting = includeExisting;
        IncludeRecommended = includeRecommended;
        ExportChoice = exportCombo.SelectedItem as string;
        RevitOutputChoice = revitOutputCombo.SelectedItem as string;

        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
