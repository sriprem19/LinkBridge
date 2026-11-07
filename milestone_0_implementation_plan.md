# LinkBridge Milestone 0 — Implementation Plan (Final)

**Status:** Approved for implementation.  
**Document Location:** `C:\LinkBridge\milestone_0_implementation_plan.md`  
**Working directory for all commands:** `C:\LinkBridge`  
**Scope preserved:** .NET 10, `src/windows/` root, nine source files, no architecture/research document changes, `windows_app/` untouched, scrcpy download/staging deferred.

## 1. Objectives

Build a .NET 10 harness that (1) detects the connected device (`63f2d6ee`) via ADB with correctly classified failures and `adb devices -l` parsing, and (2) launches/tunes/stops scrcpy as a supervised subprocess with race-free lifecycle, atomic duplicate prevention, conservative exit classification, graceful shutdown, atomically coordinated process disposal (lease vs. cleanup), and clean orphan-free cleanup — verified by partitioned unit/integration tests.

## 2. Project structure (buildable)

- Nine source files under `src/windows/LinkBridge.Milestone0/`: Program.cs, AdbCommandResult.cs, AdbDevice.cs, AdbClientService.cs, ScrcpyLaunchOptions.cs, ScrcpyArgumentBuilder.cs, ScrcpySession.cs, ScrcpyExitClassifier.cs, ScrcpyProcessSupervisor.cs.
- Metadata: `LinkBridge.Milestone0.csproj`, `LinkBridge.sln`, `LinkBridge.Milestone0.Tests.csproj`.
- Test project references app via `..\..\src\windows\LinkBridge.Milestone0\LinkBridge.Milestone0.csproj`.

## 3. Corrections applied (summary)

1. Process-exit race prevention.
2. Atomic thread-safe duplicate-session prevention.
3. ADB failures distinguished from zero devices; stderr preserved.
4. Exit code 2 not assumed device disconnection.
5. ProcessStartInfo.ArgumentList for all args; AdditionalArgs removed.
6. --max-size=1080 limits max video dimension, not 1080p guarantee (MaxVideoDimension).
7. Shutdown tests check orphaned scrcpy + scrcpy-owned adb clients without killing shared ADB server.
8. Test partitioning: Unit (SDK only) vs Integration (ADB/scrcpy/device).
9. Exact safe startup ordering.
10. Project structure includes .csproj and .sln; buildable.
11. ADB parser handles `adb devices -l`.
12. Test deps: app zero NuGet; test project requires restore.
13. Startup order exact sequence with cleanup on failures.
14. Graceful shutdown: CloseMainWindow, bounded wait, tree-kill; never kill ADB server; idempotent.
15. Solution/test project explicit ProjectReference path; both in solution.
16. Exited handler defined for Starting/Running/Exiting; idempotent cleanup; no deadlock.
17. Explicit runnable test commands with C:\LinkBridge working directory.
18. Coordinated process disposal lifetime: use-count leases.
19. Atomic key-and-value registry removal.
20. Lease acquisition atomic with cleanup request.

## 4. Component design

### 4.1 ADB
- Execute with timeout via ProcessStartInfo.ArgumentList.
- Capture stdout/stderr; on timeout kill process tree we started; TimedOut=true.
- Outcome: ExecutableNotFound → Timeout → NonZeroExit (stderr preserved) → MalformedOutput → Success (empty list valid).
- Parser (adb devices -l): skip header and blanks; field0 serial, field1 state required; fields 2+ optional key:value metadata; exit 0 + no device lines → Success + empty list.
- Device-state helper GetDeviceStateAsync(serial) → Present | Absent | Unknown.

### 4.2 Launch options & arguments
- ScrcpyLaunchOptions: MaxVideoDimension (int, default 1080), VideoBitRate (string, default "8M"), TunnelHost (string, default "127.0.0.1"), TurnScreenOff (bool), ShutdownTimeout (TimeSpan, default 5s), ExtraArguments (IReadOnlyList<string>). No AdditionalArgs string.
- ScrcpyArgumentBuilder.Build populates ProcessStartInfo.ArgumentList only; Arguments left empty.

### 4.3 Session lifecycle
State machine: Starting / Running / Exiting / Exited / Disposed, per-session lock. Safe ordering per correction 13. Exited handler per correction 16. Process disposal lifetime per corrections 18 & 20: lock-guarded lease protocol (AcquireProcessUse/ReleaseProcessUse/RequestCleanup/FinalizeProcess), redundant WaitForExitAsync monitor independent of Exited event, no disposal while in use, exactly-once disposal, no deadlocks, finalization completes even if Exited event delayed.

Invariants:
- Exited, once set, never overwritten by Running.
- Transitions atomic within per-session lock.
- Registry removal at most once per session, only via RunCleanupOnce().
- Process.Dispose() exactly once, only when _useCount==0 and _cleanupRequested==true, committed atomically under _processUseLock.
- No lease acquired after cleanup begins; no finalization before all leases release.
- No lock held across an await.

### 4.4 Duplicate prevention
Per-device SemaphoreSlim (one slot) serializes start attempts; ConcurrentDictionary<string,ScrcpySession>.TryAdd before Process.Start; concurrent second caller throws without launching; failure removes entry by reference.

### 4.5 Exit classification
Raw exit code always stored. Exit code 2 not assumed disconnection; DeviceDisconnected only with stderr evidence AND ADB device-state confirmation; otherwise AbnormalExit. UserRequestedStop when we initiated stop; NormalExit for code 0.

### 4.6 Graceful shutdown
StopAsync: idempotent entry check → StopRequested/Exiting → CloseMainWindow graceful attempt → bounded WaitForExitAsync (default 5s) → Kill(entireProcessTree:true) fallback if still alive → finally RunCleanupOnce() and await _cleanupTcs.Task (bounded) → Disposed. All Process access within AcquireProcessUse. Never touches ADB server. No lock across awaits.

### 4.7 Atomic registry removal
Removals use ICollection<KeyValuePair<TKey,TValue>>.Remove(KeyValuePair) for both _sessions and _activeByDevice, so entry removed only if key still maps to same session instance.

### 4.8 Shutdown & orphan checks
Integration tests assert: tracked process exited/disposed; zero orphaned scrcpy.exe from this session; adb client process count returns to baseline; ADB server still responsive (never killed).

### 4.9 Test partitioning
- App project: zero external NuGet dependencies.
- Test project: requires Microsoft.NET.Test.Sdk, xunit, xunit.runner.visualstudio (and optional coverlet.collector) via restore.
- [Trait("Category","Unit")] — runs fully offline after restore; no binaries/device.
- [Trait("Category","Integration")] — requires real adb/scrcpy and device 63f2d6ee; skips gracefully when absent.
- Commands (working directory C:\LinkBridge):
  - Restore: dotnet restore tests\LinkBridge.Milestone0.Tests\LinkBridge.Milestone0.Tests.csproj
  - Build: dotnet build src\windows\LinkBridge.sln
  - Unit: dotnet test tests\LinkBridge.Milestone0.Tests\LinkBridge.Milestone0.Tests.csproj --filter "Category=Unit"
  - Integration: dotnet test tests\LinkBridge.Milestone0.Tests\LinkBridge.Milestone0.Tests.csproj --filter "Category=Integration"
  - All: dotnet test tests\LinkBridge.Milestone0.Tests\LinkBridge.Milestone0.Tests.csproj

## 5. Acceptance criteria

1. Unit tests pass with only .NET 10 SDK (after package restore).
2. detect distinguishes zero devices from ADB failure, with stderr preserved.
3. adb devices -l lines with trailing metadata parse successfully.
4. Concurrent StartMirroringAsync for same serial launches exactly one process.
5. A process exiting within milliseconds of start is observed (no stale session); Exited never overwritten by Running.
6. Exit code 2 not auto-reported as device disconnection without evidence.
7. All arguments arrive as discrete tokens (no manual quoting); no AdditionalArgs string.
8. --max-size=1080 documented as max dimension, not 1080p guarantee.
9. Shutdown: graceful CloseMainWindow attempted first, bounded wait, then tree-kill fallback; no orphaned scrcpy or scrcpy-owned adb processes; ADB server survives.
10. Exited handler and StopAsync never deadlock or duplicate-remove registry entries; cleanup idempotent.
11. Process.Dispose() runs exactly once, only after all in-flight leases release; finalization completes even if Exited event delayed.
12. Lease acquisition atomic with cleanup request: no lease after cleanup begins; no finalization before leases release; no lock across awaits.
13. Registry removal atomic by key-and-value; newer session never removed by stale owner.
14. Build succeeds via dotnet build src\windows\LinkBridge.sln (both projects).
15. Test project resolves app project via ..\..\src\windows\LinkBridge.Milestone0\LinkBridge.Milestone0.csproj.

## 6. Deferred (unchanged)

- Scrcpy binary download/staging into tools/scrcpy/.
- WinUI 3 shell, Android companion, networking, audio, clipboard, file transfer.
- Architecture/research document edits.

