using System.Text;

namespace NetCheck.Infrastructure.Logging;

public sealed class FileLogger
{
    private readonly string _logFile;
    private readonly object _sync = new();

    public FileLogger(string logFile)
    {
        _logFile = logFile ?? throw new ArgumentNullException(nameof(logFile));
    }

    public void Error(string message, Exception exception)
    {
        try
        {
            var line = $"{DateTimeOffset.Now:O} [ERROR] {message}{Environment.NewLine}{exception}{Environment.NewLine}";
            lock (_sync)
            {
                var directory = Path.GetDirectoryName(_logFile);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(_logFile, line, Encoding.UTF8);
                TrimIfNeeded();
            }
        }
        catch
        {
            // Logging must never cause a secondary application failure.
        }
    }

    private void TrimIfNeeded()
    {
        var file = new FileInfo(_logFile);
        if (!file.Exists || file.Length <= 2_000_000)
        {
            return;
        }

        var lines = File.ReadAllLines(_logFile);
        File.WriteAllLines(_logFile, lines.Skip(lines.Length / 2), Encoding.UTF8);
    }
}

