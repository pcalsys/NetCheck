namespace NetCheck.App.Services;

public interface IFileDialogService
{
    string? ShowReportSaveDialog(string suggestedFileName);

    string? ShowSupportBundleSaveDialog(string suggestedFileName) => null;
}
