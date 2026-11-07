using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LinkBridge.Milestone0;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: detect | start <serial> | stop");
            return 0;
        }

        var adb = new AdbClientService();
        var supervisor = new ScrcpyProcessSupervisor(adb);

        switch (args[0].ToLowerInvariant())
        {
            case "detect":
                return await RunDetectAsync(adb);
            case "start":
                {
                    var serial = args.Length > 1 ? args[1] : "63f2d6ee";
                    return await RunStartAsync(supervisor, serial);
                }
            case "stop":
                Console.WriteLine("Stop requires a running session; use the library API.");
                return 0;
            default:
                Console.WriteLine($"Unknown command: {args[0]}");
                return 1;
        }
    }

    private static async Task<int> RunDetectAsync(AdbClientService adb)
    {
        var (result, devices) = await adb.GetDevicesAsync();
        Console.WriteLine($"ADB outcome: {result.Outcome}");
        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            Console.WriteLine($"stderr: {result.Stderr}");
        }
        if (result.Outcome != AdbCommandOutcome.Success)
        {
            return 1;
        }
        Console.WriteLine($"Connected devices: {devices.Count}");
        foreach (var d in devices)
        {
            Console.WriteLine($"  {d.Serial} {d.State}");
        }
        return 0;
    }

    private static async Task<int> RunStartAsync(ScrcpyProcessSupervisor supervisor, string serial)
    {
        var options = new ScrcpyLaunchOptions();
        try
        {
            var session = await supervisor.StartMirroringAsync(serial, options);
            Console.WriteLine($"Started session {session.SessionId} for {serial}");
            Console.WriteLine("Press Ctrl+C to stop.");

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
            }

            await supervisor.StopAsync(session, options);
            Console.WriteLine("Stopped.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start: {ex.Message}");
            return 1;
        }
    }
}
