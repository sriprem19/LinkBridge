using System.Diagnostics;
using System.IO;

namespace LinkBridge.Milestone0;

public sealed class AdbClientService
{
    private readonly string _adbPath;
    private readonly TimeSpan _defaultTimeout;

    public AdbClientService(string? adbPath = null, TimeSpan? defaultTimeout = null)
    {
        _adbPath = string.IsNullOrWhiteSpace(adbPath) ? "adb" : adbPath;
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(10);
    }

    public async Task<AdbCommandResult> RunAsync(IEnumerable<string> args, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var effectiveTimeout = timeout ?? _defaultTimeout;
        var psi = new ProcessStartInfo
        {
            FileName = _adbPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Process? process = null;
        try
        {
            process = new Process { StartInfo = psi };
            if (!process.Start())
            {
                sw.Stop();
                try { process.Dispose(); } catch { }
                return new AdbCommandResult(AdbCommandOutcome.ExecutableNotFound, null, string.Empty, "Failed to start adb process.", false, sw.Elapsed);
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            try { process?.Dispose(); } catch { }
            return new AdbCommandResult(AdbCommandOutcome.ExecutableNotFound, null, string.Empty, ex.Message, false, sw.Elapsed);
        }

        try
        {
        using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

        bool timedOut = false;
        bool cancelledByCaller = false;
        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
            {
                cancelledByCaller = true;
            }
            else
            {
                timedOut = true;
            }
            try { process.Kill(entireProcessTree: true); } catch { }
            try { await process.WaitForExitAsync(CancellationToken.None); } catch { }
        }

        string stdout = string.Empty;
        string stderr = string.Empty;
        try
        {
            var allRead = Task.WhenAll(stdoutTask, stderrTask);
            var completed = await Task.WhenAny(allRead, Task.Delay(2000));
            if (completed == allRead)
            {
                stdout = await stdoutTask;
                stderr = await stderrTask;
            }
        }
        catch
        {
            // best effort; keep whatever was captured
        }
        sw.Stop();
        int? exitCode = null;
        try { if (process.HasExited) exitCode = process.ExitCode; } catch { }

        if (cancelledByCaller)
        {
            return new AdbCommandResult(AdbCommandOutcome.Cancelled, exitCode, stdout, stderr, false, sw.Elapsed);
        }
        if (timedOut)
        {
            return new AdbCommandResult(AdbCommandOutcome.Timeout, exitCode, stdout, stderr, true, sw.Elapsed);
        }
        if (exitCode != 0)
        {
            return new AdbCommandResult(AdbCommandOutcome.NonZeroExit, exitCode, stdout, stderr, false, sw.Elapsed);
        }
        return new AdbCommandResult(AdbCommandOutcome.Success, exitCode, stdout, stderr, false, sw.Elapsed);
        }
        finally
        {
            try { process?.Dispose(); } catch { }
        }
    }

    public async Task<(AdbCommandResult Result, IReadOnlyList<AdbDevice> Devices)> GetDevicesAsync(CancellationToken ct = default)
    {
        var result = await RunAsync(new[] { "devices", "-l" }, null, ct);
        if (result.Outcome != AdbCommandOutcome.Success)
        {
            return (result, Array.Empty<AdbDevice>());
        }

        var (devices, malformed, malformedSnippet) = ParseDevicesOutput(result.Stdout);

        if (malformed)
        {
            var diag = string.IsNullOrEmpty(malformedSnippet) ? result.Stderr : $"{result.Stderr}\nMalformed line: {malformedSnippet}";
            var malformedResult = new AdbCommandResult(AdbCommandOutcome.MalformedOutput, result.ExitCode, result.Stdout, diag, false, result.Duration);
            return (malformedResult, devices);
        }

        return (result, devices);
    }

    public static (IReadOnlyList<AdbDevice> Devices, bool Malformed, string? MalformedSnippet) ParseDevicesOutput(string stdout)
    {
        var devices = new List<AdbDevice>();
        bool malformed = false;
        string? malformedSnippet = null;
        using var reader = new StringReader(stdout);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            if (trimmed.StartsWith("List of devices attached", StringComparison.OrdinalIgnoreCase)) continue;

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                malformed = true;
                malformedSnippet = trimmed;
                continue;
            }
            var serial = parts[0];
            var state = parts[1];
            if (state.Contains(':'))
            {
                malformed = true;
                malformedSnippet = trimmed;
                continue;
            }

            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 2; i < parts.Length; i++)
            {
                var token = parts[i];
                var idx = token.IndexOf(':');
                if (idx > 0)
                {
                    metadata[token[..idx]] = token[(idx + 1)..];
                }
            }

            devices.Add(new AdbDevice(serial, state, metadata.Count > 0 ? metadata : null));
        }

        return (devices, malformed, malformedSnippet);
    }

    public async Task<AdbDeviceState> GetDeviceStateAsync(string serial, CancellationToken ct = default)
    {
        var (result, devices) = await GetDevicesAsync(ct);
        if (result.Outcome != AdbCommandOutcome.Success) return AdbDeviceState.Unknown;
        foreach (var d in devices)
        {
            if (string.Equals(d.Serial, serial, StringComparison.Ordinal))
            {
                return d.State.Equals("device", StringComparison.OrdinalIgnoreCase)
                    ? AdbDeviceState.Present
                    : AdbDeviceState.Absent;
            }
        }
        return AdbDeviceState.Absent;
    }
}

public enum AdbDeviceState
{
    Present,
    Absent,
    Unknown
}
