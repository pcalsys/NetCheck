namespace NetCheck.App.Services;

public interface IMessageService
{
    void ShowError(string title, string message);

    void ShowInformation(string title, string message);

    bool Confirm(string title, string message);
}

