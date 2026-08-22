namespace NetCheck.Core.Models;

public enum SpeedTestFailure
{
    NetworkUnavailable,
    TimedOut,
    UnexpectedResponse
}

public sealed class SpeedTestException : Exception
{
    public SpeedTestException(SpeedTestFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    public SpeedTestException(SpeedTestFailure failure, string message, Exception innerException)
        : base(message, innerException)
    {
        Failure = failure;
    }

    public SpeedTestFailure Failure { get; }
}
