using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;
using LinkBridge.Milestone0;

namespace LinkBridge.Milestone0.Tests;

public class IntegrationTests
{
    private static string? FindTool(string name)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(dir, name + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static bool HasDevice(AdbClientService adb, string serial)
    {
        try
        {
            var (result, devices) = adb.GetDevicesAsync().GetAwaiter().GetResult();
            return result.Outcome == AdbCommandOutcome.Success && devices.Any(d => d.Serial == serial && d.State == "device");
        }
        catch
        {
            return false;
        }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Integration_StartStop_NoOrphans()
    {
        var adbPath = FindTool("adb");
        var scrcpyPath = FindTool("scrcpy");
        Skip.If(adbPath == null, "adb not found on PATH");
        Skip.If(scrcpyPath == null, "scrcpy not found on PATH");

        var adb = new AdbClientService(adbPath);
        Skip.If(!HasDevice(adb, "63f2d6ee"), "device 63f2d6ee not connected");

        // Baseline PIDs (scoped to test-owned processes).
        var baselineScrcpyPids = Process.GetProcessesByName("scrcpy").Select(p => p.Id).ToHashSet();
        var baselineAdbPids = Process.GetProcessesByName("adb").Select(p => p.Id).ToHashSet();

        var sup = new ScrcpyProcessSupervisor(adb);
        var options = new ScrcpyLaunchOptions();
        var session = sup.StartMirroringAsync("63f2d6ee", options).GetAwaiter().GetResult();
        Assert.NotNull(session);

        sup.StopAsync(session, options).GetAwaiter().GetResult();

        // Orphan assertions: no new scrcpy or adb client PIDs beyond baseline.
        var afterScrcpyPids = Process.GetProcessesByName("scrcpy").Select(p => p.Id).ToHashSet();
        var afterAdbPids = Process.GetProcessesByName("adb").Select(p => p.Id).ToHashSet();
        Assert.True(afterScrcpyPids.IsSubsetOf(baselineScrcpyPids), "Orphaned scrcpy process detected.");
        Assert.True(afterAdbPids.IsSubsetOf(baselineAdbPids), "Orphaned adb process detected.");

        // ADB server still responsive.
        var (result, _) = adb.GetDevicesAsync().GetAwaiter().GetResult();
        Assert.Equal(AdbCommandOutcome.Success, result.Outcome);
    }
}
