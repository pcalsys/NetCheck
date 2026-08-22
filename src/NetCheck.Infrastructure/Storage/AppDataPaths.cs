namespace NetCheck.Infrastructure.Storage;

public sealed class AppDataPaths
{
    public AppDataPaths(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NetCheck");
    }

    public string RootDirectory { get; }

    public string ReportsDirectory => Path.Combine(RootDirectory, "Reports");

    public string ActivitiesDirectory => Path.Combine(RootDirectory, "Activities");

    public string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    public string LogFile => Path.Combine(RootDirectory, "NetCheck.log");
}
