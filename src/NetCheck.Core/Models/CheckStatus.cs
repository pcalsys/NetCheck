namespace NetCheck.Core.Models;

public enum CheckStatus
{
    Pending = 0,
    Running = 1,
    Passed = 2,
    Warning = 3,
    Failed = 4,
    Skipped = 5
}

