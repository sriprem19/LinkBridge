namespace LinkBridge.Milestone0;

public enum ScrcpyExitClassification
{
    NormalExit,
    UserRequestedStop,
    DeviceDisconnected,
    AbnormalExit,
    Unknown
}

public static class ScrcpyExitClassifier
{
    public static ScrcpyExitClassification Classify(
        int? rawExitCode,
        bool stopRequested,
        string stderrTail,
        Func<string, AdbDeviceState> deviceStateLookup,
        string deviceSerial)
    {
        if (stopRequested)
        {
            return ScrcpyExitClassification.UserRequestedStop;
        }

        if (rawExitCode == 0)
        {
            return ScrcpyExitClassification.NormalExit;
        }

        // Exit code 2 is NOT assumed to mean device disconnection.
        // Only classify as DeviceDisconnected with stderr evidence AND device-state confirmation.
        if (rawExitCode == 2)
        {
            var stderrLower = (stderrTail ?? string.Empty).ToLowerInvariant();
            bool stderrEvidence = stderrLower.Contains("device") ||
                                  stderrLower.Contains("disconnected") ||
                                  stderrLower.Contains("not found") ||
                                  stderrLower.Contains("offline");
            if (stderrEvidence)
            {
                var state = deviceStateLookup(deviceSerial);
                if (state == AdbDeviceState.Absent)
                {
                    return ScrcpyExitClassification.DeviceDisconnected;
                }
            }
            return ScrcpyExitClassification.AbnormalExit;
        }

        return ScrcpyExitClassification.Unknown;
    }
}
