# LinkBridge System Architecture Specification

**Project:** LinkBridge (Windows 11 + Android Ecosystem Integration)  
**Document Version:** 2.0.0 (Post-Audit Finalized Architecture)  
**Date:** 2026-11-05  
**Document Location:** `C:\LinkBridge\architecture\linkbridge_system_architecture.md`  
**Target Environment:** Windows 11 (Desktop) & Android 14+ (Test Device: OnePlus Nord CE 2 Lite 5G, Snapdragon 695 5G)  
**Primary Philosophy:** Local-First, Zero-Cloud Dependency, Pragmatic Simplicity, Standard Open Technologies.

---

## 1. System Overview

### 1.1 High-Level Architecture Diagram

```
+===================================================================================================+
|                                    WINDOWS 11 WORKSTATION                                         |
|                                                                                                   |
|  +---------------------------------------------------------------------------------------------+  |
|  |                            LinkBridge Windows Shell (WinUI 3 / C#)                          |  |
|  |  +---------------------+   +--------------------+   +------------------------------------+  |  |
|  |  | Floating Bubble UX  |   | Radial Menu Overlay|   | Independent Feature Windows        |  |  |
|  |  | - Drag & Edge Snap  |   | - 8-slice launcher |   | - Notifications, Messages, Files   |  |  |
|  |  | - Live Battery Ring |   | - Status badges    |   | - Settings, Audio / Call Widget    |  |  |
|  |  +----------+----------+   +---------+----------+   +-----------------+------------------+  |  |
|  +-------------|------------------------|--------------------------------|---------------------+  |
|                v                        v                                v                        |
|  +---------------------------------------------------------------------------------------------+  |
|  |                             LinkBridge Desktop Core (Background Daemon)                     |  |
|  |  +-------------------------+   +--------------------------+   +--------------------------+  |  |
|  |  | Connection & Transport  |   | Session State Manager    |   | Scrcpy Process Supervisor|  |  |
|  |  | - USB / ADB Forwarding  |   | - SQLite Local Cache     |   | - Subprocess Execution   |  |  |
|  |  | - Local WebSocket Hub   |   | - Reactive Event Bus     |   | - Lifecycle & Watchdog   |  |  |
|  |  +------------+------------+   +--------------------------+   +------------+-------------+  |  |
|  +---------------|------------------------------------------------------------|----------------+  |
|                  |                                                            |                   |
|                  | (Single Local WebSocket - Port 38241)                      | (Subprocess CLI)  |
|                  | - Text Frames: JSON-RPC (Telemetry, Controls)              |                   |
|                  | - Binary Frames: Chunked File Transfer                     |                   |
|                  v                                                            v                   |
|  +--------------------------------+                         +----------------------------------+  |
|  | ADB Port Forward / Network     |                         | scrcpy.exe (External Win32 / SDL)|  |
|  | - 127.0.0.1:38241 (USB loopback|                         | - Low-latency H.264/H.265 Decode |  |
|  | - mDNS / Wi-Fi LAN (Phase 3)   |                         | - Mouse, Touch & Keyboard Send   |  |
|  +---------------+----------------+                         +----------------+-----------------+  |
+==================|===========================================================|====================+
                   |                                                           |
       (Local USB Cable / ADB Tunnel)                               (ADB Port Forwarding / USB)
                   |                                                           |
+==================|===========================================================|====================+
|                  v                                                           v                    |
|  +--------------------------------+                         +----------------------------------+  |
|  | LinkBridge Android Companion   |                         | scrcpy-server.jar (Transient)    |  |
|  | (Kotlin / Android 14+ / AOSP)  |                         | - Runs under ADB Shell UID 2000  |  |
|  |                                |                         | - SurfaceControl Hardware Encode |  |
|  |  +--------------------------+  |                         | - /dev/uhid Event Injection      |  |
|  |  | Foreground Service       |  |                         +----------------------------------+  |
|  |  | (connectedDevice type)   |  |                                                               |
|  |  +-------------+------------+  |                                                               |
|  |                |               |                                                               |
|  |  +-------------v------------+  |                                                               |
|  |  | Feature Modules (AOSP)   |  |                                                               |
|  |  | - NotificationListener   |  |                                                               |
|  |  | - Telephony / SMS Engine |  |                                                               |
|  |  | - MediaStore Content Obs |  |                                                               |
|  |  | - Battery & Power State  |  |                                                               |
|  |  +--------------------------+  |                                                               |
|  +--------------------------------+                                                               |
|                                   ANDROID DEVICE (OnePlus Nord CE 2 Lite)                         |
+===================================================================================================+
```

### 1.2 Component Breakdown
The system consists of three distinct runtime components:
1. **LinkBridge Windows Shell & Daemon:** A native Windows 11 application providing the user interface, local data store, device discovery, and session supervisor.
2. **LinkBridge Android Companion App:** A dedicated Android app running standard AOSP background services to collect system state (battery, notifications, SMS, storage) and communicate over local network or USB.
3. **Scrcpy Subsystem (Transient Helper):** Unmodified upstream open-source scrcpy binary executed by the Windows daemon to provide dedicated high-framerate screen mirroring and remote input injection without re-implementing video pipelines.

---

## 2. Windows Application Design

### 2.1 Technology Selection & Evaluation: WinUI 3 with C# / .NET
* **WinUI 3 (Windows App SDK):** Native Windows 11 visual design (Mica, acrylic, rounded geometry, modern typography). Runs out-of-process with hardware-accelerated composition.
* **C# / .NET 10 or .NET 8 LTS:** Modern asynchronous task runtime, excellent memory safety, zero-overhead interop with Windows Runtime (CsWinRT), and robust socket networking.
* **Architecture Pattern:** Clean separation between **Presentation Layer** (WinUI 3 MVVM) and **Core Domain Service Layer** (.NET Background Service / In-Process Host).

### 2.2 Shell UI Components
* **Floating Bubble UX:**
  * Borderless, transparent, topmost window (`WS_EX_TOPMOST`, `WS_EX_TOOLWINDOW`).
  * Square top-level Win32 window with a transparent background (`SystemBackdrop = null`), rendering the circular bubble inside the XAML canvas. Mouse hit testing is restricted to the circular geometry (`WM_NCHITTEST` handling) so clicks outside pass through to desktop windows.
  * Drag physics: Smooth movement and edge snapping to left/right margins.
  * Circumferential battery ring around the bubble perimeter.
* **Radial Menu Overlay:**
  * Triggered upon clicking the floating bubble.
  * Circular radial dial presenting 8 quick-action slices:
    1. *Screen Mirror* (Launches Scrcpy supervisor)
    2. *Notifications* (Opens Notification History window)
    3. *Messages* (Opens SMS thread window)
    4. *Calls / Dialer* (Opens Call Control HUD)
    5. *Files / Photos* (Opens File Transfer hub)
    6. *Clipboard* (Toggles Auto-Sync)
    7. *Find My Phone* (Triggers remote alert sound)
    8. *Settings* (Opens Configuration window)
* **Independent Feature Windows:**
  * Individual independent lightweight WinUI 3 windows rather than a single monolithic dashboard. Each feature window can be opened, repositioned, docked, or closed independently without affecting the floating bubble or background connection.
* **System Tray Companion:**
  * Provides quick-access menu (reconnect, mute, exit) if the user hides the floating bubble.

### 2.3 Scrcpy Subprocess Orchestration
* **Isolation Guarantee [CONFIRMED DESIGN PRINCIPLE]:** Scrcpy is executed strictly as an independent external child process (`System.Diagnostics.Process`). It is **not** embedded into WinUI 3 via Win32 `SetParent`.
* **Execution & Lifecycle:**
  * LinkBridge generates tailored CLI invocation strings:
    ```bash
    scrcpy.exe -s <device_serial> \
               --window-title="LinkBridge Screen Mirror - <DeviceName>" \
               --window-borderless \
               --max-size=1080 \
               --video-bit-rate=8M \
               --stay-awake \
               --tunnel-host=127.0.0.1
    ```
  * LinkBridge monitors standard output/error pipes asynchronously for state changes (`[INFO] Device disconnected`, `[ERROR] Demuxer error`).
  * Process tracking: LinkBridge retains the child process handle. If the user clicks "Close Mirror" from the radial menu, LinkBridge cleanly closes the process (`CloseMainWindow()` -> `Kill()`). If the process crashes or closes, the radial menu state immediately reverts to "Idle".

---

## 3. Android Companion App Design

### 3.1 Technology Stack & Platform Compatibility
* **Language & Runtime:** Kotlin, target SDK 34/35, minimum SDK 29 (Android 10+).
* **Architecture:** Modular AOSP Architecture using Kotlin Coroutines, Flow, Jetpack Room, and Androidx Lifecycle.

### 3.2 Service & Background Architecture: Constraints & Realities
* **Foreground Service (`LinkBridgeConnectionService`):**
  * Declared with `android:foregroundServiceType="connectedDevice"` (Android 14 requirement).
  * Holds an unobtrusive ongoing status notification showing *"Connected to Windows Workstation"* with a quick disconnect action.
  * Manages the active local WebSocket connection over USB/ADB port forward or Wi-Fi.
* **OEM Battery Optimization & Survival Reality [CONFIRMED PLATFORM CONSTRAINT]:**
  * A foreground service **does not guarantee immunity** from being killed on OnePlus / OxygenOS / ColorOS devices when the screen is off for extended periods.
  * **Mitigations:**
    1. Onboarding UI instructs the user to disable OxygenOS "App auto-freeze" and allow background activity in system battery settings.
    2. Companion requests `REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`.
    3. `WorkManager` is scheduled for periodic background checks. *(Note: WorkManager provides deferrable background execution, not a real-time persistent watchdog, but serves as a recovery mechanism if the foreground service is terminated while disconnected).*
* **Removal of AccessibilityService:**
  * Scrcpy handles all remote input injection during mirroring via ADB `shell` UID (`2000`).
  * The proposed `AccessibilityService` for input has been **removed** from the LinkBridge companion to eliminate unnecessary permissions and avoid Android 13/14 "Restricted Settings" friction.

---

## 4. Connection, Communication & Security

### 4.1 Connection Transport Models

| Mode | Transport Pathway | Throughput & Latency | Best Used For |
|---|---|---|---|
| **USB (Wired)** | ADB Port Forwarding (`adb forward tcp:38241 tcp:38241`) over USB cable | Maximum speed, lowest latency (<5ms network jitter), zero Wi-Fi traffic | Desk docked setup, initial onboarding, reliable baseline |
| **Wi-Fi (Wireless LAN)** | Direct local TCP / WebSocket over local IP subnet (`192.168.x.x:38241`) | High speed (15-50 Mbps), dependent on router/5GHz channel | Wire-free everyday use around home/office (Phase 3) |

### 4.2 Application Protocol: Single WebSocket Architecture
To eliminate protocol overhead and dual-socket synchronization bugs:
* **Single Connection (Port 38241):** LinkBridge runs a single WebSocket connection per session.
* **Multiplexed Message Framing:**
  1. **Text Frames (JSON-RPC 2.0):** Used for all control, commands, status updates, and telemetry:
     - `telemetry.battery` (battery level, charging status)
     - `notification.posted` / `notification.dismissed` / `notification.reply`
     - `sms.sync_threads` / `sms.send`
     - `clipboard.update`
     - `session.ping` / `session.pong`
  2. **Binary Frames (Chunked File Transfer):** Used for streaming files and images without base64 JSON bloat.
     - **Frame Header:** 16-byte binary header `[uint32 fileId, uint32 chunkIndex, uint32 totalChunks, uint32 payloadLength]` followed by the chunk payload.
     - **Chunk Size:** Configurable (default `64 KB` for balanced throughput and memory usage).
     - **Flow Control:** Credit-based windowing (receiver sends ACK every N chunks to prevent buffer bloat).
* **Heartbeat & Watchdog:** Ping/pong every 5 seconds. If no pong arrives within 15 seconds, connection enters reconnecting state with exponential backoff (2s, 5s, 10s).

### 4.3 Pragmatic Security Architecture
* **USB MVP Security [ACCEPTED AUDIT REVISION]:**
  * Communication over USB relies on **ADB authorization** (the physical USB debugging prompt *"Always allow from this computer"* establishes the trust boundary).
  * Traffic runs across local loopback `127.0.0.1` via `adb forward`.
  * An ephemeral **application session token** (UUIDv4 generated by Windows upon launch and passed to the companion via ADB intent or handshake) authenticates the WebSocket connection.
* **Wi-Fi Security (Phase 3 Design):**
  * Avoid inventing custom cryptographic protocols.
  * Use **standard TLS 1.3** with self-signed certificate fingerprint verification pinned via a QR code or numeric PIN displayed on screen during first-time pairing.
  * No public certificate authorities (CAs), complex mTLS, or application-layer double encryption.

---

## 5. Scrcpy Integration Strategy

### 5.1 Scrcpy Ownership vs. LinkBridge Ownership

```
+------------------------------------------+------------------------------------------+
|            SCRCPY OWNS                   |            LINKBRIDGE OWNS               |
+------------------------------------------+------------------------------------------+
| - Video encoding pipeline (MediaCodec)   | - Floating Bubble & Radial Menu UI       |
| - Low-latency H.264/H.265 video decoding | - Notifications, SMS, and Call UI        |
| - SDL3 video presentation window         | - Battery & Network telemetry            |
| - Raw mouse, touch & keyboard injection  | - Device discovery & pairing             |
| - UHID / AOA hardware simulation         | - Encrypted WebSocket communication bus  |
| - Screen-off capture execution           | - Bi-directional visual File Hub         |
+------------------------------------------+------------------------------------------+
```

### 5.2 Subprocess Management Architecture
* **Binary Staging:** Precompiled, official x64 scrcpy binaries placed under `tools/scrcpy/`.
* **Execution Flags:**
  ```bash
  scrcpy.exe -s <device_serial> \
             --window-title="LinkBridge Screen Mirror - <DeviceName>" \
             --window-borderless \
             --max-size=1080 \
             --video-bit-rate=8M \
             --stay-awake \
             --tunnel-host=127.0.0.1
  ```
* **Process Watchdog & Exit Mapping:**
  - Exit code `0`: Normal closure by user.
  - Exit code `1` or error output: Map known error strings (e.g., "Device disconnected", "Screen off deadlock") to user-facing Windows toast alerts.

### 5.3 Licensing & Distribution Compliance
* **Scrcpy Core:** **Apache License 2.0**.
* **FFmpeg Dependency:** Dynamically linked (`libavcodec.dll`, etc.) under **LGPL v2.1+**.
* **Compliance Requirement:** Distribute scrcpy binaries unmodified in a dedicated subfolder, preserving Apache 2.0 and LGPL copyright notices.

---

## 6. Feature Modules Specification

### 6.1 Screen Mirroring & Remote Input
* **Windows Role:** Launches and monitors the scrcpy child window.
* **Android Role:** Managed by `scrcpy-server` (executing via ADB shell).
* **Communication:** Dedicated video and control TCP sockets established over ADB tunnel.
* **Known Limitations:** Protected banking apps (`FLAG_SECURE`) display a black screen; physical screen unlock requires screen-on or UHID input.

### 6.2 Notifications Sync
* **Windows Role:** Displays native Windows 11 toast notifications; maintains an in-app Notification History list; provides inline reply boxes for chat apps.
* **Android Role:** `NotificationListenerService` captures incoming notifications and dispatches `RemoteInput` replies back to the messaging app.
* **Communication:** WebSocket JSON-RPC events (`notification.posted`, `notification.dismissed`, `notification.reply`).
* **Permissions:** `android.permission.BIND_NOTIFICATION_LISTENER_SERVICE`.
* **Known Limitations:** Dismissing a notification on Windows can clear it on Android, but some persistent system notifications cannot be dismissed.

### 6.3 Messaging (SMS/MMS) & Chat Apps
* **Windows Role:** Conversation thread viewer, contact search, incoming message popup, and compose interface.
* **Android Role:** Reads historical SMS threads via `Telephony.Sms` content provider; sends outbound messages via `SmsManager`.
* **Communication:** WebSocket JSON-RPC (`sms.sync_threads`, `sms.send`).
* **Permissions:** `READ_SMS`, `SEND_SMS`, `RECEIVE_SMS`, `READ_CONTACTS`.
* **Critical Platform Constraint [CONFIRMED]:** Android strictly restricts third-party access to RCS messages sent via Google Messages. RCS messages do not write to the public SMS database.
  * **LinkBridge Strategy:** The SMS module handles standard SMS/MMS. For RCS and third-party chat apps (WhatsApp, Signal, Telegram), LinkBridge relies on the `NotificationListenerService` inline reply (`RemoteInput`) mechanism.

### 6.4 Calling & Telephony Controls
* **Windows Role:** In-call notification toast showing caller ID, Answer/Decline buttons, and dial pad.
* **Android Role:** Monitors call state via `TelephonyManager`; triggers outbound calls via `Intent.ACTION_CALL`.
* **Call Answering/Declining Reality [CONFIRMED PLATFORM CONSTRAINT]:**
  - Answering and declining calls programmatically from third-party apps on modern Android (API 29+) requires the `ANSWER_PHONE_CALLS` permission, and on some devices requires being the active `InCallService` or default phone app.
  - **LinkBridge MVP Strategy:** Telephony support is scoped to **Call State Monitoring** (incoming call toasts, caller ID) and triggering outbound calls (`Intent.ACTION_CALL`). Answering/rejecting calls programmatically will be tested and enabled only where device permissions allow without requiring LinkBridge to replace the system dialer.
* **Cellular Call Audio Conduit:** **Deferred to Experimental Research**. Two-way cellular voice call audio cannot be captured over IP sockets due to Android OS security boundaries. Standard Bluetooth HFP audio routing will be investigated as a future spike.

### 6.5 Media Audio Streaming
* **Windows Role:** Plays phone audio (YouTube, music, games) through PC speakers.
* **Android Role:** Handled natively by Scrcpy's audio forwarding (`REMOTE_SUBMIX` via Opus over ADB).
* **Requirements:** Android 12+ (fully supported on our Android 14 test phone).

### 6.6 Clipboard Synchronization
* **Windows Role:** Listens for Windows clipboard changes via Win32 `AddClipboardFormatListener`; pushes text updates to Android.
* **Android Role:** Listens via `ClipboardManager.OnPrimaryClipChangedListener`; updates Android clipboard.
* **Communication:** WebSocket payload (`clipboard.update { text: "..." }`).
* **Privacy Guard:** Option to disable clipboard sync or ignore password-manager clips.

### 6.7 File Transfer & Gallery Access
* **Windows Role:** Drag-and-drop file drop zone; photo gallery viewer.
* **Android Role:** Writes incoming files to `/sdcard/Download/LinkBridge/`; exposes recent camera photos via `MediaStore.Images`.
* **Communication:** Chunked binary WebSocket frames with credit-based flow control and SHA-256 validation.

### 6.8 Battery & Device Telemetry
* **Windows Role:** Displays battery percentage and charging status on the floating bubble ring and radial menu.
* **Android Role:** Relays `ACTION_BATTERY_CHANGED` updates to Windows.

---

## 7. Performance & Resource Allocation Strategy

### 7.1 Process & Thread Isolation
* **UI Thread Isolation:** The WinUI 3 DispatcherQueue handles *only* rendering and user interaction.
* **I/O & Socket Threading:** Network sockets and file transfers execute on dedicated background thread pool workers (`ThreadPool` / `IAsyncEnumerable`).
* **Video Isolation:** The video rendering surface runs in scrcpy's native process, fully utilizing hardware video decoding without impacting the .NET Garbage Collector or WinUI render loop.

### 7.2 Proposed Benchmarking Targets (OnePlus Nord CE 2 Lite / Snapdragon 695)
*These are target engineering goals to validate during testing, not guaranteed claims:*
* **Mirroring Latency Target (USB):** ~35-60 ms.
* **Windows Idle CPU Usage:** < 1.0% when idle; < 5.0% during active mirroring.
* **Windows Memory Footprint:** < 120 MB for LinkBridge daemon + shell.
* **Android Battery Drain:** < 4% per hour on background standby; < 12% per hour during continuous Wi-Fi screen mirroring.

---

## 8. Architectural Fact, Decision, and Assumption Registry

| Topic | Classification | Current Status / Basis |
|---|---|---|
| **WinUI 3 Independent Windows** | **Confirmed Fact** | WinUI 3 natively supports multiple top-level Win32 `AppWindow` instances. |
| **Scrcpy Independence** | **Confirmed Fact** | Scrcpy runs without APK installation under ADB shell UID 2000. |
| **Android 14 Foreground Service** | **Confirmed Fact** | `FOREGROUND_SERVICE_CONNECTED_DEVICE` is mandatory on Android 14. |
| **RCS Database Inaccessibility** | **Confirmed Fact** | Android's `Telephony.Sms` does not store RCS chat messages. |
| **Single WebSocket Protocol** | **Design Decision** | Eliminates dual-socket state synchronization issues for MVP. |
| **USB MVP Security via ADB** | **Design Decision** | Uses ADB authorization + local loopback session token for MVP. |
| **Removal of AccessibilityService** | **Design Decision** | Delegates 100% of remote input to Scrcpy; avoids Android restricted settings. |
| **Bluetooth HFP Audio Feasibility** | **Unverified / Risk** | Third-party Windows WinRT programmatic HFP call routing is unverified; deferred to R&D. |
| **OxygenOS Background Survival** | **Assumption to Test** | Must benchmark how long the companion service survives screen-off on the OnePlus Nord CE 2 Lite. |
