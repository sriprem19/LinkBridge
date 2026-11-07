using System.Collections.Concurrent;
using System.Diagnostics;

namespace LinkBridge.Milestone0;

public sealed class ScrcpyProcessSupervisor
{
    private readonly AdbClientService _adb;
    private readonly ConcurrentDictionary<Guid, ScrcpySession> _sessions = new();
    private readonly ConcurrentDictionary<string, ScrcpySession> _activeByDevice = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _deviceGates = new();

    private readonly string _scrcpyFileName;
    public ScrcpyProcessSupervisor(AdbClientService adb, string scrcpyFileName = "scrcpy")
    {
        _adb = adb;
        _scrcpyFileName = scrcpyFileName;
    }

    public async Task<ScrcpySession> StartMirroringAsync(string deviceSerial, ScrcpyLaunchOptions options, CancellationToken ct = default)
    {
        var gate = _deviceGates.GetOrAdd(deviceSerial, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        ScrcpySession? session = null;
        try
        {
            if (_activeByDevice.TryGetValue(deviceSerial, out var existing) && existing.IsActive)
            {
                throw new InvalidOperationException($"Mirroring already active for device {deviceSerial}.");
            }

            session = new ScrcpySession(Guid.NewGuid(), deviceSerial);

            // Register global session.
            if (!_sessions.TryAdd(session.SessionId, session))
            {
                throw new InvalidOperationException("Failed to register session.");
            }

            // Subscribe to Exited for registry cleanup (normal + kill paths).
            session.Exited += (s, e) =>
            {
                ((ICollection<KeyValuePair<Guid, ScrcpySession>>)_sessions).Remove(new KeyValuePair<Guid, ScrcpySession>(session.SessionId, session));
                ((ICollection<KeyValuePair<string, ScrcpySession>>)_activeByDevice).Remove(new KeyValuePair<string, ScrcpySession>(deviceSerial, session));
            };

            // Reserve device atomically.
            if (!_activeByDevice.TryAdd(deviceSerial, session))
            {
                // Remove global entry by reference (atomic KVP removal).
                ((ICollection<KeyValuePair<Guid, ScrcpySession>>)_sessions).Remove(new KeyValuePair<Guid, ScrcpySession>(session.SessionId, session));
                throw new InvalidOperationException($"Mirroring already active for device {deviceSerial}.");
            }
        }
        finally
        {
            gate.Release();
        }

        // Now create process, attach handler, register (already registered), start.
        Process? process = null;
        try
        {
            process = new Process();
            session.Process = process;

            var psi = new ProcessStartInfo
            {
                FileName = _scrcpyFileName,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            ScrcpyArgumentBuilder.Build(psi, options);
            process.StartInfo = psi;
            process.EnableRaisingEvents = true;

            // Attach Exited handler before Start.
            process.Exited += (s, e) => session.HandleExit();

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start scrcpy process.");
            }

            // After successful start: transition Starting -> Running only if not already Exited.
            session.TransitionToRunningIfStarting();

            // Start redundant monitor (independent of Exited event) after successful start.
            _ = MonitorProcessExitAsync(session, process);

            // Begin async reads for stderr tail.
            _ = Task.Run(async () =>
            {
                try
                {
                    var stderr = await process.StandardError.ReadToEndAsync();
                    var lines = stderr.Split('\n');
                    var tail = lines.Length <= 20 ? stderr : string.Join('\n', lines[^20..]);
                    session.StderrTail = tail;
                }
                catch
                {
                    // best effort
                }
            });
        }
        catch
        {
            await CleanupAfterStartFailureAsync(session, deviceSerial);
            throw;
        }
        return session;
    }

    private async Task MonitorProcessExitAsync(ScrcpySession session, Process process)
    {
        int? code = null;
        try
        {
            using (var lease = session.AcquireProcessUse())
            {
                try { await process.WaitForExitAsync(); } catch { }
                try { if (process.HasExited) code = process.ExitCode; } catch { }
            }
        }
        catch (ObjectDisposedException)
        {
            // cleanup already requested; do not access Process
        }
        catch (InvalidOperationException)
        {
            // cleanup already requested; do not access Process
        }

        session.HandleExit(code);
    }

    public async Task StopAsync(ScrcpySession session, ScrcpyLaunchOptions? options = null, CancellationToken ct = default)
    {
        var timeout = options?.ShutdownTimeout ?? TimeSpan.FromSeconds(5);

        // Idempotent entry.
        if (session.State == ScrcpySessionState.Exited || session.State == ScrcpySessionState.Disposed)
        {
            session.RunCleanupOnce();
            try
            {
                await session.CleanupCompleted.WaitAsync(timeout, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                if (!session.CleanupCompleted.IsCompleted)
                {
                    throw new TimeoutException("Cleanup did not complete within the timeout.");
                }
            }
            if (!session.CleanupCompleted.IsCompleted)
            {
                throw new TimeoutException("Cleanup did not complete within the timeout.");
            }
            return;
        }

        session.StopRequested = true;
        session.MarkExiting();

        int? capturedExitCode = null;
        try
        {
            using (var lease = session.AcquireProcessUse())
            {
                var process = session.Process;
                if (process != null && !process.HasExited)
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        try { process.CloseMainWindow(); } catch { /* best effort */ }
                    }
                }

                if (process != null)
                {
                    try
                    {
                        using var timeoutCts = new CancellationTokenSource(timeout);
                        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
                        await process.WaitForExitAsync(linked.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                process.Kill(entireProcessTree: true);
                            }
                        }
                        catch
                        {
                            // best effort
                        }
                        try
                        {
                            using var finalCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                            await process.WaitForExitAsync(finalCts.Token);
                        }
                        catch
                        {
                            // best effort
                        }
                    }
                }

                if (process != null)
                {
                    try { if (process.HasExited) capturedExitCode = process.ExitCode; } catch { }
                }

                // Request cleanup while the lease is still held.
                session.RequestCleanup();
            }
        }
        catch (ObjectDisposedException)
        {
            // process already disposed; skip
        }
        catch (InvalidOperationException)
        {
            // cleanup already requested; skip
        }

        session.HandleExit(capturedExitCode);

        try
        {
            await session.CleanupCompleted.WaitAsync(timeout, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            if (!session.CleanupCompleted.IsCompleted)
            {
                throw new TimeoutException("Cleanup did not complete within the timeout.");
            }
        }
    }

    private async Task CleanupAfterStartFailureAsync(ScrcpySession session, string deviceSerial)
    {
        // Use the same idempotent cleanup/event path as normal exit.
        session.HandleExit(null);
        await Task.CompletedTask;
    }

    public IReadOnlyDictionary<Guid, ScrcpySession> Sessions => _sessions;

    public IReadOnlyDictionary<string, ScrcpySession> ActiveByDevice => _activeByDevice;
}
