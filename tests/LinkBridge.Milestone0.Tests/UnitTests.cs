using System;
using System.Diagnostics;
using System.IO;
using Xunit;
using LinkBridge.Milestone0;

namespace LinkBridge.Milestone0.Tests;

public class ArgumentBuilderTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Build_UsesArgumentList_NotArguments()
    {
        var psi = new ProcessStartInfo { FileName = "scrcpy" };
        var options = new ScrcpyLaunchOptions { MaxVideoDimension = 1080, VideoBitRate = "8M", TunnelHost = "127.0.0.1" };
        ScrcpyArgumentBuilder.Build(psi, options);

        Assert.True(psi.ArgumentList.Count > 0);
        Assert.Equal(string.Empty, psi.Arguments);
        Assert.Contains("--max-size", psi.ArgumentList);
        Assert.Contains("1080", psi.ArgumentList);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Build_ExtraArguments_AreIndividualTokens()
    {
        var psi = new ProcessStartInfo { FileName = "scrcpy" };
        var options = new ScrcpyLaunchOptions { ExtraArguments = new[] { "--foo", "bar baz" } };
        ScrcpyArgumentBuilder.Build(psi, options);

        Assert.Contains("--foo", psi.ArgumentList);
        Assert.Contains("bar baz", psi.ArgumentList);
    }
}

public class ExitClassifierTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ExitCode2_WithoutDeviceEvidence_IsAbnormal()
    {
        var result = ScrcpyExitClassifier.Classify(2, false, "some error", _ => AdbDeviceState.Present, "63f2d6ee");
        Assert.Equal(ScrcpyExitClassification.AbnormalExit, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ExitCode2_WithEvidenceAndDeviceAbsent_IsDeviceDisconnected()
    {
        var result = ScrcpyExitClassifier.Classify(2, false, "device disconnected", _ => AdbDeviceState.Absent, "63f2d6ee");
        Assert.Equal(ScrcpyExitClassification.DeviceDisconnected, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StopRequested_IsUserRequestedStop()
    {
        var result = ScrcpyExitClassifier.Classify(1, true, "", _ => AdbDeviceState.Present, "63f2d6ee");
        Assert.Equal(ScrcpyExitClassification.UserRequestedStop, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ExitCode0_IsNormal()
    {
        var result = ScrcpyExitClassifier.Classify(0, false, "", _ => AdbDeviceState.Present, "63f2d6ee");
        Assert.Equal(ScrcpyExitClassification.NormalExit, result);
    }
}

public class ExitClassifierEdgeTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ExitCode2_EvidenceButDevicePresent_IsAbnormal()
    {
        var result = ScrcpyExitClassifier.Classify(2, false, "device disconnected", _ => AdbDeviceState.Present, "63f2d6ee");
        Assert.Equal(ScrcpyExitClassification.AbnormalExit, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UnknownCode_IsUnknown()
    {

public class SessionSubscriberExceptionTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void SubscriberException_DoesNotPreventCleanup()
    {
        var session = new ScrcpySession(Guid.NewGuid(), "63f2d6ee");
        session.Exited += (s, e) => throw new InvalidOperationException("boom");
        session.RunCleanupOnce();
        Assert.Equal(ScrcpySessionState.Disposed, session.State);
        Assert.True(session.CleanupCompleted.IsCompleted);
    }
}


public class SessionLifecycleTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Lease_AcquireAfterCleanupRequested_Throws()
    {
        var session = new ScrcpySession(Guid.NewGuid(), "63f2d6ee");
        session.RequestCleanup();
        Assert.Throws<ObjectDisposedException>(() => session.AcquireProcessUse());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Lease_FinalizeCompletesCleanupTask()
    {
        var session = new ScrcpySession(Guid.NewGuid(), "63f2d6ee");
        var task = session.CleanupCompleted;
        Assert.False(task.IsCompleted);
        session.RequestCleanup();
        Assert.True(task.IsCompleted);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void HandleExit_IsIdempotent()
    {
        var session = new ScrcpySession(Guid.NewGuid(), "63f2d6ee");
        session.HandleExit(0);
        var firstState = session.State;
        session.HandleExit(1);
        Assert.Equal(firstState, session.State);
        Assert.Equal(0, session.RawExitCode); // first code preserved
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RunCleanupOnce_RaisesExitedOnce()
    {
        var session = new ScrcpySession(Guid.NewGuid(), "63f2d6ee");
        int count = 0;
        session.Exited += (s, e) => count++;
        session.RunCleanupOnce();
        session.RunCleanupOnce();
        Assert.Equal(1, count);
    }
}

        var result = ScrcpyExitClassifier.Classify(42, false, "", _ => AdbDeviceState.Present, "63f2d6ee");
        Assert.Equal(ScrcpyExitClassification.Unknown, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ExitCode1_NoStop_IsUnknown()
    {
        var result = ScrcpyExitClassifier.Classify(1, false, "", _ => AdbDeviceState.Present, "63f2d6ee");
        Assert.Equal(ScrcpyExitClassification.Unknown, result);
    }
}

public class AdbParsingTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ParseMetadataLines()
    {
        var meta = new System.Collections.Generic.Dictionary<string, string> { ["model"] = "foo" };
        var d = new AdbDevice("abc", "device", meta);
        Assert.Equal("abc", d.Serial);
        Assert.Equal("device", d.State);
        Assert.NotNull(d.Metadata);
    }
}

public class AdbParserTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ParseDevicesOutput_ParsesMetadata()
    {
        var stdout = "List of devices attached\n63f2d6ee device product:foo model:bar device:baz transport_id:1\n\n";
        var (devices, malformed, snippet) = AdbClientService.ParseDevicesOutput(stdout);
        Assert.False(malformed);
        Assert.Single(devices);
        Assert.Equal("63f2d6ee", devices[0].Serial);
        Assert.Equal("device", devices[0].State);
        Assert.NotNull(devices[0].Metadata);
        Assert.Equal("bar", devices[0].Metadata!["model"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseDevicesOutput_EmptyList()
    {
        var stdout = "List of devices attached\n\n";
        var (devices, malformed, snippet) = AdbClientService.ParseDevicesOutput(stdout);
        Assert.False(malformed);
        Assert.Empty(devices);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ParseDevicesOutput_MalformedLine()
    {
        var stdout = "List of devices attached\nonlyserial\n";
        var (devices, malformed, snippet) = AdbClientService.ParseDevicesOutput(stdout);
        Assert.True(malformed);
        Assert.NotNull(snippet);
    }
}

public class SupervisorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task StartupFailure_CleansRegistries()
    {
        var adb = new AdbClientService();
        var sup = new ScrcpyProcessSupervisor(adb);
        var options = new ScrcpyLaunchOptions();
        try
        {
            await sup.StartMirroringAsync("63f2d6ee", options);
        }
        catch
        {
            // expected: scrcpy not installed
        }
        Assert.Empty(sup.Sessions);
        Assert.Empty(sup.ActiveByDevice);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RestartAfterFailure_SucceedsInRegistration()
    {
        var adb = new AdbClientService();
        var sup = new ScrcpyProcessSupervisor(adb);
        var options = new ScrcpyLaunchOptions();
        try { await sup.StartMirroringAsync("63f2d6ee", options); } catch { }
        try { await sup.StartMirroringAsync("63f2d6ee", options); } catch { }
        Assert.Empty(sup.ActiveByDevice);
    }
}

public class SupervisorNormalExitTests
{
    private static string CreateTempBat(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bat");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task NormalExit_RemovesRegistries()
    {
        var bat = CreateTempBat("@echo off\r\nexit 0\r\n");
        try
        {
            var adb = new AdbClientService();
            var sup = new ScrcpyProcessSupervisor(adb, bat);
            var options = new ScrcpyLaunchOptions();
            var session = await sup.StartMirroringAsync("63f2d6ee", options);
            await session.CleanupCompleted.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Empty(sup.Sessions);
            Assert.Empty(sup.ActiveByDevice);
        }
        finally
        {
            try { File.Delete(bat); } catch { }
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RestartAfterCompletedSession_Succeeds()
    {
        var bat = CreateTempBat("@echo off\r\nexit 0\r\n");
        try
        {
            var adb = new AdbClientService();
            var sup = new ScrcpyProcessSupervisor(adb, bat);
            var options = new ScrcpyLaunchOptions();
            var s1 = await sup.StartMirroringAsync("63f2d6ee", options);
            await s1.CleanupCompleted.WaitAsync(TimeSpan.FromSeconds(10));
            var s2 = await sup.StartMirroringAsync("63f2d6ee", options);
            await s2.CleanupCompleted.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Empty(sup.ActiveByDevice);
        }
        finally
        {
            try { File.Delete(bat); } catch { }
        }
    }


public class ConcurrencyTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ConcurrentStarts_OnlyOneRegisters()
    {
        var bat = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bat");
        File.WriteAllText(bat, "@echo off\r\nping -n 30 127.0.0.1 >nul\r\nexit 0\r\n");
        try
        {
            var adb = new AdbClientService();
            var sup = new ScrcpyProcessSupervisor(adb, bat);
            var options = new ScrcpyLaunchOptions();
            var tasks = new[]
            {
                sup.StartMirroringAsync("63f2d6ee", options),
                sup.StartMirroringAsync("63f2d6ee", options)
            };
            var results = await Task.WhenAll(tasks).ContinueWith(t =>
            {
                if (t.IsFaulted) return t.Exception;
                return null;
            });
            // At least one task should have faulted with InvalidOperationException.
            int faulted = tasks.Count(t => t.IsFaulted);
            Assert.True(faulted >= 1, "Expected at least one concurrent start to be rejected.");
            // Cleanup any successful session.
            foreach (var t in tasks)
            {
                if (t.Status == TaskStatus.RanToCompletion)
                {
                    await sup.StopAsync(t.Result, options);
                }
            }
        }
        finally
        {
            try { File.Delete(bat); } catch { }
        }
    }
}

    [Fact]
    [Trait("Category", "Unit")]
    public async Task StopAsync_LongRunning_ProcessTerminates()
    {
        var bat = CreateTempBat("@echo off\r\nping -n 30 127.0.0.1 >nul\r\nexit 0\r\n");
        try
        {
            var adb = new AdbClientService();
            var sup = new ScrcpyProcessSupervisor(adb, bat);
            var options = new ScrcpyLaunchOptions { ShutdownTimeout = TimeSpan.FromMilliseconds(200) };
            var session = await sup.StartMirroringAsync("63f2d6ee", options);
            await sup.StopAsync(session, options);
            Assert.True(session.CleanupCompleted.IsCompleted);
            Assert.Empty(sup.ActiveByDevice);
        }
        finally
        {
            try { File.Delete(bat); } catch { }
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task StopAsync_Cancellation_Throws()
    {
        var bat = CreateTempBat("@echo off\r\nping -n 30 127.0.0.1 >nul\r\nexit 0\r\n");
        try
        {
            var adb = new AdbClientService();
            var sup = new ScrcpyProcessSupervisor(adb, bat);
            var options = new ScrcpyLaunchOptions { ShutdownTimeout = TimeSpan.FromSeconds(5) };
            var session = await sup.StartMirroringAsync("63f2d6ee", options);
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await sup.StopAsync(session, options, cts.Token));
        }
        finally
        {
            try { File.Delete(bat); } catch { }
        }
    }
}

