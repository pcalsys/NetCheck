using NetCheck.Core.Models;

namespace NetCheck.Core.Abstractions;

public interface INetworkRepairPlanner
{
    NetworkRepairPlan CreatePlan(DiagnosticReport report);
}
