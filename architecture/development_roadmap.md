# LinkBridge Milestone Development Roadmap (v2.0)

**Project:** LinkBridge (Windows 11 + Android Ecosystem Integration)  
**Document Version:** 2.0.0 (Post-Audit Revised Sequence)  
**Date:** 2026-11-05  
**Document Location:** `C:\LinkBridge\architecture\development_roadmap.md`  
**Development Philosophy:** Rigorous Incremental Progression. Core communication and mirroring over USB must be proven before building rich floating UI layers.

---

## Roadmap Overview

```
+---------------------------------------------------------------------------------------------------+
| PHASE 1: CORE COMMUNICATIONS & MIRRORING BASELINE (USB)                                           |
| - Milestone 0: Scrcpy Subprocess Supervisor & ADB Device Detection Harness                        |
| - Milestone 1: Minimal Android Companion (Foreground Service + USB WebSocket Loopback)            |
| - Milestone 2: Core Telemetry (Live Battery % & Connectivity Status verified end-to-end)          |
+---------------------------------------------------------------------------------------------------+
                                                  |
                                                  v
+---------------------------------------------------------------------------------------------------+
| PHASE 2: SIGNATURE WINDOWS 11 UX SHELL                                                            |
| - Milestone 3: WinUI 3 Floating Circular Bubble (Dragging, Edge-snapping, Battery Ring)           |
| - Milestone 4: Radial Menu Overlay (8 slices, triggering Scrcpy & feature windows)                |
+---------------------------------------------------------------------------------------------------+
                                                  |
                                                  v
+---------------------------------------------------------------------------------------------------+
| PHASE 3: WIRELESS CONNECTIVITY & SYNCHRONIZATION                                                  |
| - Milestone 5: Local Wi-Fi Discovery (mDNS) & Secure Pinned Pairing (TLS 1.3)                      |
| - Milestone 6: Real-Time Notifications (`NotificationListenerService` + Win11 Toasts + Reply)     |
| - Milestone 7: Bi-directional Clipboard Sync & Drag-and-Drop File Transfer                        |
+---------------------------------------------------------------------------------------------------+
                                                  |
                                                  v
+---------------------------------------------------------------------------------------------------+
| PHASE 4: ADVANCED FEATURES & EXPERIMENTAL R&D                                                     |
| - Milestone 8: SMS Thread Viewer & Telephony Call Controls                                        |
| - Milestone 9: Bluetooth Call Audio Routing Spike & Performance Benchmarks (Snapdragon 695)       |
+---------------------------------------------------------------------------------------------------+
```

---

## Detailed Milestone Specifications

### Milestone 0: Scrcpy Subprocess Supervisor & ADB Detection (Coding Milestone 1)
* **Goal:** Establish a working desktop baseline where LinkBridge can detect the connected OnePlus phone over USB and launch a tuned scrcpy mirroring session.
* **Exact Scope:**
  * Configure solution structure: `src/windows/LinkBridge.sln` (.NET 8/10 WinUI 3 project) and `tools/scrcpy/` (prebuilt binary staging).
  * Build an internal `AdbClientService` to query device state (`adb devices`, serial detection for `63f2d6ee`).
  * Implement `ScrcpyProcessSupervisor` to launch `scrcpy.exe` with command-line arguments, monitor process output, handle unexpected terminations, and cleanly stop the process.
* **Dependencies:** None.
* **Acceptance Criteria:**
  1. Executing the test runner detects connected device serial `63f2d6ee` in `device` state.
  2. Launching Scrcpy from the supervisor successfully displays the borderless 1080p mirror window.
  3. The supervisor captures Scrcpy process state transitions (running, stopped, crashed) asynchronously without blocking the UI thread.
  4. Closing the mirror window cleanly releases process handles without leaving zombie `scrcpy.exe` or `adb.exe` processes running.

---

### Milestone 1: Minimal Android Companion (Foreground Service & USB WebSocket Loopback)
* **Goal:** Build the minimal Android companion app and establish bidirectional communication between Windows and Android over USB.
* **Exact Scope:**
  * Initialize Android project (`src/android/LinkBridgeCompanion`) in Kotlin.
  * Implement `LinkBridgeConnectionService` as an Android 14+ `FOREGROUND_SERVICE_CONNECTED_DEVICE`.
  * Establish an unencrypted local WebSocket connection over ADB port forwarding (`adb forward tcp:38241 tcp:38241`).
  * Implement simple JSON-RPC ping/pong and session handshake with an application session token.
* **Dependencies:** Milestone 0.
* **Acceptance Criteria:**
  1. Android companion launches and starts a foreground service displaying an ongoing notification.
  2. Establishing `adb forward tcp:38241 tcp:38241` connects the Windows client to the Android WebSocket server (or vice-versa) on `127.0.0.1:38241`.
  3. Windows sends `session.ping` and receives `session.pong` with latency < 5ms over the USB tunnel.
  4. Disconnecting the USB cable triggers clean disconnect state handling on both ends.

---

### Milestone 2: End-to-End Core Telemetry Verification
* **Goal:** Verify that real device telemetry flows continuously from Android to Windows over the USB WebSocket tunnel.
* **Exact Scope:**
  * Android companion registers a `BroadcastReceiver` for `Intent.ACTION_BATTERY_CHANGED`.
  * Telemetry updates (`telemetry.battery { level: int, isCharging: bool }`) are dispatched over the WebSocket.
  * Windows daemon receives the event, stores it in state cache, and logs real-time updates.
* **Dependencies:** Milestone 1.
* **Acceptance Criteria:**
  1. Windows console / test window displays the exact live battery percentage of device `63f2d6ee`.
  2. Plugging/unplugging phone charger updates the charging status on Windows within 1 second.
  3. Companion survives 30 minutes of screen-off time over USB without connection drop.

---

### Milestone 3: Windows 11 Floating Circular Bubble UX
* **Goal:** Create the signature Windows 11 floating circular bubble with drag-and-drop physics and live battery visualization.
* **Exact Scope:**
  * Build a borderless, transparent, topmost WinUI 3 window.
  * Implement Win32 hit testing (`WM_NCHITTEST`) restricting clicks strictly to the circular geometry.
  * Implement drag physics with smooth edge-snapping to left/right screen borders.
  * Bind the circumferential progress ring to the live battery telemetry from Milestone 2.
* **Dependencies:** Milestone 2.
* **Acceptance Criteria:**
  1. Bubble can be dragged anywhere on Windows 11 desktop and snaps smoothly to the nearest display margin upon release.
  2. Transparent window corners pass through mouse clicks to background desktop apps.
  3. Circumferential ring accurately reflects the phone's live battery percentage.

---

### Milestone 4: Radial Menu Overlay & Feature Window Framework
* **Goal:** Build the 8-slice radial menu launcher and independent feature window manager.
* **Exact Scope:**
  * Clicking the floating bubble expands an animated circular radial dial with 8 slices.
  * Wire the *Screen Mirror* slice directly to the Milestone 0 Scrcpy supervisor.
  * Implement window docking, hide/show behaviors, and system tray integration.
* **Dependencies:** Milestone 3.
* **Acceptance Criteria:**
  1. Clicking the floating bubble smoothly displays the 8-slice radial menu.
  2. Clicking "Screen Mirror" launches the Scrcpy mirror window; closing Scrcpy updates the radial menu badge state.
  3. System tray icon provides fallback controls (show/hide bubble, exit application).

---

### Milestone 5: Local Wi-Fi Discovery (mDNS) & Secure Pinned Pairing (TLS 1.3)
* **Goal:** Enable wire-free operation over local Wi-Fi without cloud dependencies.
* **Exact Scope:**
  * Implement mDNS advertisement (`_linkbridge._tcp`) on Android and discovery on Windows.
  * Implement one-time pairing using a 6-digit PIN or QR code displaying self-signed TLS certificate fingerprints.
  * Transition WebSocket from `ws://127.0.0.1` to encrypted `wss://` over local LAN IP.
* **Dependencies:** Milestone 4.
* **Acceptance Criteria:**
  1. Windows automatically discovers the OnePlus phone on the same Wi-Fi subnet.
  2. First-time connection requires entering a 6-digit PIN or scanning a QR code; subsequent connections connect silently via pinned keys.
  3. Unplugging the USB cable transfers telemetry and control seamlessly to Wi-Fi.

---

### Milestone 6: Real-Time Notifications Sync & Inline Reply
* **Goal:** Intercept phone notifications and display native Windows 11 toast alerts with inline reply support.
* **Exact Scope:**
  * Android implements `NotificationListenerService`.
  * Forwards notification post/dismiss events over WebSocket JSON-RPC.
  * Windows displays native toast notifications and maintains an in-app Notification History list.
  * Inline replies trigger `RemoteInput.sendIntent()` on Android.
* **Dependencies:** Milestone 5.
* **Acceptance Criteria:**
  1. Incoming notification on the phone triggers a native Windows 11 toast within 500ms.
  2. Dismissing a toast on Windows clears the notification on Android.
  3. Replying to a WhatsApp/SMS notification from the Windows toast sends the message without unlocking the phone.

---

### Milestone 7: Bi-directional Clipboard Sync & Chunked File Transfer
* **Goal:** Synchronize clipboard text and provide drag-and-drop file transfers.
* **Exact Scope:**
  * Windows Win32 `AddClipboardFormatListener` and Android `ClipboardManager` listener for text synchronization.
  * Dragging files onto the bubble/window initiates chunked binary WebSocket transfer (64 KB chunks with credit-based flow control) to `/sdcard/Download/LinkBridge/`.
* **Dependencies:** Milestone 5.
* **Acceptance Criteria:**
  1. Copying text on Windows makes it immediately pasteable on Android (and vice-versa).
  2. Dragging a 50 MB test file onto the floating bubble completes transfer quickly with an accurate progress bar.
  3. Received file matches the SHA-256 hash of the source file.

---

### Milestone 8: SMS/MMS Thread Viewer & Telephony Call Controls
* **Goal:** Provide conversation history and incoming phone call management.
* **Exact Scope:**
  * Android queries `content://sms/` and `Telephony.Sms`; transmits conversation list to Windows.
  * Windows renders a modern conversation thread viewer; sends outbound SMS via `SmsManager`.
  * Android monitors `TelephonyManager`; transmits incoming call state (Caller ID) to Windows; Windows shows an incoming call HUD with Answer/Decline buttons.
* **Dependencies:** Milestone 6.
* **Acceptance Criteria:**
  1. User can view historical SMS threads and send text messages directly from Windows.
  2. Incoming phone calls display a live notification on Windows showing caller details.

---

### Milestone 9: Bluetooth Call Audio Routing Spike & Snapdragon 695 Benchmarking
* **Goal:** Conduct exploratory R&D for Bluetooth HFP call audio routing and benchmark overall system performance.
* **Exact Scope:**
  * Investigate Windows Bluetooth API for Hands-Free Profile (HFP) headset pairing to route two-way cellular audio.
  * Measure end-to-end screen mirroring latency, frame drops, CPU/GPU utilization, and battery drain on the OnePlus Nord CE 2 Lite 5G over USB and 5 GHz Wi-Fi.
* **Dependencies:** Milestones 0 through 8.
* **Acceptance Criteria:**
  1. Formal research report detailing Bluetooth HFP audio feasibility on Windows 11.
  2. Benchmark report confirming system performance conforms to target engineering goals.

---

## Coding Milestone 1: Exact Launch Scope

When implementation commences, work will focus strictly on **Milestone 0**:
* **Directory Setup:** Create `src/windows/` and `tools/scrcpy/`.
* **Binary Staging:** Place verified, official x64 `scrcpy.exe` and supporting DLLs into `tools/scrcpy/`.
* **C# Harness:** Build a lightweight CLI / basic test harness implementing:
  1. `AdbDeviceWatcher`: Polls/queries ADB for connected device `63f2d6ee`.
  2. `ScrcpyProcessSupervisor`: Launches Scrcpy with tuned arguments (`--max-size=1080 --video-bit-rate=8M --tunnel-host=127.0.0.1`), tracks process exit codes, and provides clean stop/kill semantics.
* **No Android code, no complex WinUI 3 dragging physics, and no network sockets will be written in Milestone 0.**
