namespace NetCheck.Core.Models;

public enum NetworkRepairActionId
{
    FlushDnsCache = 1,
    RenewDhcpLease = 2,
    ClearArpCache = 3,
    ResetUserProxy = 4,
    ResetWinsockCatalog = 5,
    ResetTcpIpStack = 6
}
