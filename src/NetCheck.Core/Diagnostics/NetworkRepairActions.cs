using NetCheck.Core.Models;

namespace NetCheck.Core.Diagnostics;

public static class NetworkRepairActions
{
    public static NetworkRepairAction Get(NetworkRepairActionId id) => id switch
    {
        NetworkRepairActionId.FlushDnsCache => new NetworkRepairAction
        {
            Id = id,
            Title = "Clear DNS cache",
            Description = "Removes cached DNS answers so Windows requests fresh name-resolution data.",
            RequiresElevation = true
        },
        NetworkRepairActionId.RenewDhcpLease => new NetworkRepairAction
        {
            Id = id,
            Title = "Renew IP address",
            Description = "Releases and requests a new DHCP address for connected network adapters.",
            RequiresElevation = true
        },
        NetworkRepairActionId.ClearArpCache => new NetworkRepairAction
        {
            Id = id,
            Title = "Refresh local network cache",
            Description = "Clears stale address mappings used to communicate with the local router.",
            RequiresElevation = true
        },
        NetworkRepairActionId.ResetUserProxy => new NetworkRepairAction
        {
            Id = id,
            Title = "Turn off the current proxy",
            Description = "Disables the current user’s manual proxy and automatic proxy script. Managed-network users should confirm this with their administrator first."
        },
        NetworkRepairActionId.ResetWinsockCatalog => new NetworkRepairAction
        {
            Id = id,
            Title = "Reset Windows network sockets",
            Description = "Restores the Windows Sockets catalog used by applications for network access.",
            RequiresElevation = true,
            RequiresRestart = true
        },
        NetworkRepairActionId.ResetTcpIpStack => new NetworkRepairAction
        {
            Id = id,
            Title = "Reset the TCP/IP stack",
            Description = "Restores core Windows TCP/IP components to their default state.",
            RequiresElevation = true,
            RequiresRestart = true
        },
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown network repair action.")
    };
}
