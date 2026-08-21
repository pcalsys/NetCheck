using NetCheck.Core.Models;

namespace NetCheck.Core.Abstractions;

public interface ISettingsStore
{
    Task<DiagnosticOptions> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DiagnosticOptions settings, CancellationToken cancellationToken = default);
}

