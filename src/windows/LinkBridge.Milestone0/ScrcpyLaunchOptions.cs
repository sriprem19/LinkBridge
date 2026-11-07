namespace LinkBridge.Milestone0;

public sealed class ScrcpyLaunchOptions
{
    /// <summary>
    /// Maximum video dimension (largest side) passed to scrcpy via --max-size.
    /// This limits the maximum dimension; it does NOT guarantee a 1080p output resolution.
    /// </summary>
    public int MaxVideoDimension { get; set; } = 1080;

    public string VideoBitRate { get; set; } = "8M";

    public string TunnelHost { get; set; } = "127.0.0.1";

    public bool TurnScreenOff { get; set; }

    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Pre-tokenized extra arguments. Never a raw string (avoids ambiguous splitting).
    /// </summary>
    public IReadOnlyList<string> ExtraArguments { get; set; } = Array.Empty<string>();
}
