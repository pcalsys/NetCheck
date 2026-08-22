using Microsoft.Win32;
using NetCheck.App.Localization;

namespace NetCheck.App.Services;

public sealed class FileDialogService(LocalizationService text) : IFileDialogService
{
    private readonly LocalizationService _text = text ?? throw new ArgumentNullException(nameof(text));

    public string? ShowReportSaveDialog(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = _text.Translate("Export NetCheck report"),
            FileName = suggestedFileName,
            DefaultExt = ".html",
            AddExtension = true,
            OverwritePrompt = true,
            Filter = string.Join('|',
                _text.Translate("Web report (*.html)"), "*.html",
                _text.Translate("JSON data (*.json)"), "*.json",
                _text.Translate("Plain text (*.txt)"), "*.txt"),
            FilterIndex = 1
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSupportBundleSaveDialog(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = _text.Translate("Create anonymized support bundle"),
            FileName = suggestedFileName,
            DefaultExt = ".zip",
            AddExtension = true,
            OverwritePrompt = true,
            Filter = $"{_text.Translate("ZIP archive (*.zip)")}|*.zip",
            FilterIndex = 1
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
