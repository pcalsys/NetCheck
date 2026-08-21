using NetCheck.Core.Models;

namespace NetCheck.Core.Abstractions;

public interface INetworkSnapshotProvider
{
    Task<NetworkSnapshot> CaptureAsync(CancellationToken cancellationToken);
}

