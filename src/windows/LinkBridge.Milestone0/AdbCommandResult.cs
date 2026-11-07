namespace LinkBridge.Milestone0;

public enum AdbCommandOutcome
{
    Success,
    NonZeroExit,
    Timeout,
    Cancelled,
    MalformedOutput,
    ExecutableNotFound
}

public sealed record AdbCommandResult(
    AdbCommandOutcome Outcome,
    int? ExitCode,
    string Stdout,
    string Stderr,
    bool TimedOut,
    TimeSpan Duration);
