using Microsoft.Win32;

namespace NetCheck.App.Services;

public sealed class FileDialogService : IFileDialogService
{
    public string? ShowReportSaveDialog(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export NetCheck report",
            FileName = suggestedFileName,
            DefaultExt = ".html",
            AddExtension = true,
            OverwritePrompt = true,
            Filter = "Web report (*.html)|*.html|JSON data (*.json)|*.json|Plain text (*.txt)|*.txt",
            FilterIndex = 1
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

