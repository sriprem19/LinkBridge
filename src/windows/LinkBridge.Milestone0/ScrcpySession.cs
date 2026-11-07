using System.Diagnostics;

namespace LinkBridge.Milestone0;

public enum ScrcpySessionState
{
    Starting,
    Running,
    Exiting,
    Exited,
    Disposed
}

public sealed class ScrcpySession : IDisposable
{
    private readonly object _stateLock = new();
    private readonly object _processUseLock = new();
    private ScrcpySessionState _state = ScrcpySessionState.Starting;
    private int _useCount;
    private bool _cleanupRequested;
    private bool _finalized;
    private int _cleanupDone;
    private int _exitedEventRaised;
    private int _processDisposed;
    private readonly TaskCompletionSource _cleanupTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Guid SessionId { get; }
    public string DeviceSerial { get; }
    public Process? Process { get; set; }
    public int? RawExitCode { get; private set; }
    public ScrcpyExitClassification Classification { get; set; } = ScrcpyExitClassification.Unknown;
    public string StderrTail { get; set; } = string.Empty;
    public bool StopRequested { get; set; }
    public Task CleanupCompleted => _cleanupTcs.Task;

    public ScrcpySessionState State
    {
        get { lock (_stateLock) { return _state; } }
        private set { lock (_stateLock) { _state = value; } }
    }

    public bool IsActive
    {
        get
        {
            lock (_stateLock)
            {
                return _state == ScrcpySessionState.Starting || _state == ScrcpySessionState.Running;
            }
        }
    }

    public ScrcpySession(Guid sessionId, string deviceSerial)
    {
        SessionId = sessionId;
        DeviceSerial = deviceSerial;
    }

    public IDisposable AcquireProcessUse()
    {
        lock (_processUseLock)
        {
            if (_finalized || _cleanupRequested)
            {
                throw new ObjectDisposedException(nameof(ScrcpySession));
            }
            _useCount++;
        }
        return new Lease(this);
    }

    private void ReleaseProcessUse()
    {
        bool shouldFinalize = false;
        lock (_processUseLock)
        {
            if (_useCount > 0) _useCount--;
            if (_useCount == 0 && _cleanupRequested && !_finalized)
            {
                _finalized = true;
                shouldFinalize = true;
            }
        }
        if (shouldFinalize)
        {
            FinalizeProcess();
        }
    }

    public void RequestCleanup()
    {
        bool shouldFinalize = false;
        lock (_processUseLock)
        {
            _cleanupRequested = true;
            if (_useCount == 0 && !_finalized)
            {
                _finalized = true;
                shouldFinalize = true;
            }
        }
        if (shouldFinalize)
        {
            FinalizeProcess();
        }
    }

    private void FinalizeProcess()
    {
        if (Interlocked.Exchange(ref _processDisposed, 1) != 0) return;
        try
        {
            Process?.Dispose();
        }
        catch
        {
            // best effort
        }
        finally
        {
            _cleanupTcs.TrySetResult();
        }
    }

    private sealed class Lease : IDisposable
    {
        private readonly ScrcpySession _owner;
        private int _disposed;
        public Lease(ScrcpySession owner) { _owner = owner; }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _owner.ReleaseProcessUse();
        }
    }

    public void TransitionToRunningIfStarting()
    {
        lock (_stateLock)
        {
            if (_state == ScrcpySessionState.Starting)
            {
                _state = ScrcpySessionState.Running;
            }
        }
    }

    public void TransitionToExited(int? exitCode)
    {
        lock (_stateLock)
        {
            if (RawExitCode == null && exitCode.HasValue) RawExitCode = exitCode;
            if (_state == ScrcpySessionState.Disposed) return;
            if (_state == ScrcpySessionState.Exited)
            {
                return;
            }
            if (_state == ScrcpySessionState.Starting || _state == ScrcpySessionState.Running || _state == ScrcpySessionState.Exiting)
            {
                _state = ScrcpySessionState.Exited;
                if (exitCode.HasValue) RawExitCode = exitCode;
            }
        }
    }

    public void MarkExiting()
    {
        lock (_stateLock)
        {
            if (_state == ScrcpySessionState.Starting || _state == ScrcpySessionState.Running)
            {
                _state = ScrcpySessionState.Exiting;
            }
        }
    }

    public void MarkDisposed()
    {
        lock (_stateLock)
        {
            _state = ScrcpySessionState.Disposed;
        }
    }

    public void HandleExit(int? capturedExitCode = null)
    {
        int? code = capturedExitCode;
        if (code == null)
        {
            try
            {
                using (AcquireProcessUse())
                {
                    if (Process != null && Process.HasExited)
                    {
                        code = Process.ExitCode;
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        TransitionToExited(code);
        RequestCleanup();
        RunCleanupOnce();
    }

    public void RunCleanupOnce()
    {
        if (Interlocked.Exchange(ref _cleanupDone, 1) != 0) return;

        try
        {
            if (Interlocked.Exchange(ref _exitedEventRaised, 1) == 0)
            {
                Exited?.Invoke(this, EventArgs.Empty);
            }
        }
        catch
        {
            // An Exited subscriber must not prevent cleanup.
        }

        RequestCleanup();

        lock (_stateLock)
        {
            if (_state == ScrcpySessionState.Exited)
            {
                _state = ScrcpySessionState.Disposed;
            }
        }
    }

    public event EventHandler? Exited;

    public void Dispose()
    {
        RequestCleanup();
    }
}
