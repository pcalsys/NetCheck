namespace NetCheck.Core.Abstractions;

public interface ISupportBundleService
{
    Task CreateAsync(string destinationPath, CancellationToken cancellationToken = default);
}
