namespace LinkBridge.Milestone0;

public sealed record AdbDevice(string Serial, string State, IReadOnlyDictionary<string, string>? Metadata = null);
