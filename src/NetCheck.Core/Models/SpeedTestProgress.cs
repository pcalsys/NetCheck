namespace NetCheck.Core.Models;

public sealed record SpeedTestProgress(
    SpeedTestPhase Phase,
    int Percentage,
    double CurrentMegabitsPerSecond,
    long BytesTransferred,
    long ExpectedBytes);
