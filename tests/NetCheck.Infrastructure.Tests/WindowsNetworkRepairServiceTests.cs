using NetCheck.Core.Models;
using NetCheck.Infrastructure.Network;

namespace NetCheck.Infrastructure.Tests;

public sealed class WindowsNetworkRepairServiceTests
{
    [Fact]
    public void IsHelperInvocation_RequiresExactSwitchAndOperationId()
    {
        var operationId = Guid.NewGuid().ToString("D");

        Assert.True(WindowsNetworkRepairService.IsHelperInvocation(
            [WindowsNetworkRepairService.HelperSwitch, operationId]));
        Assert.False(WindowsNetworkRepairService.IsHelperInvocation(
            [WindowsNetworkRepairService.HelperSwitch, "not-a-guid"]));
        Assert.False(WindowsNetworkRepairService.IsHelperInvocation(
            [WindowsNetworkRepairService.HelperSwitch, operationId, "extra"]));
    }

    [Fact]
    public async Task ExecuteAsync_RejectsEmptyPlanBeforeStartingHelper()
    {
        var service = new WindowsNetworkRepairService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.ExecuteAsync(NetworkRepairPlan.Empty));

        Assert.Contains("no actions", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
