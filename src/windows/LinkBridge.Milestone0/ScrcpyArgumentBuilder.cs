using System.Diagnostics;

namespace LinkBridge.Milestone0;

public static class ScrcpyArgumentBuilder
{
    public static void Build(ProcessStartInfo psi, ScrcpyLaunchOptions options)
    {
        psi.ArgumentList.Add("--max-size");
        psi.ArgumentList.Add(options.MaxVideoDimension.ToString());

        psi.ArgumentList.Add("--video-bit-rate");
        psi.ArgumentList.Add(options.VideoBitRate);

        psi.ArgumentList.Add("--tunnel-host");
        psi.ArgumentList.Add(options.TunnelHost);

        if (options.TurnScreenOff)
        {
            psi.ArgumentList.Add("--turn-screen-off");
        }

        foreach (var arg in options.ExtraArguments)
        {
            psi.ArgumentList.Add(arg);
        }
    }
}
