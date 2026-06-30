using System.Diagnostics;
using System.Windows;
using FaaTools.Core.Logging;

namespace FaaTools.RevitAddin.Wpf;

/// <summary>
/// Shared "Open File / Open Folder / Copy Path / Done" completion menu, replacing pyRevit's
/// forms.CommandSwitchWindow used at the end of Custom Excel's flow. Reused by both Synagogue
/// Excel and (where relevant) Synagogue Space so the post-generate UX matches across features.
/// </summary>
public partial class CompletionDialog : Window
{
    private readonly string _filePath;

    public CompletionDialog(string message, string filePath)
    {
        InitializeComponent();
        _filePath = filePath;
        MessageText.Text = message;
    }

    private void OnOpenFile(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_filePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Warn($"CompletionDialog: could not open file '{_filePath}' - {ex.Message}");
        }
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_filePath}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Warn($"CompletionDialog: could not open folder for '{_filePath}' - {ex.Message}");
        }
    }

    private void OnCopyPath(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_filePath);
        }
        catch (Exception ex)
        {
            Logger.Warn($"CompletionDialog: could not copy path - {ex.Message}");
        }
    }

    private void OnDone(object sender, RoutedEventArgs e) => Close();
}
