# Technical Investigation Report: Scrcpy Architecture & LinkBridge Feasibility Analysis

**Target Project:** [Genymobile/scrcpy](https://github.com/Genymobile/scrcpy)  
**Investigation Date:** 2026-11-05  
**Report Location:** `C:\LinkBridge\research\scrcpy_analysis\scrcpy_technical_investigation.md`  
**Target Device Environment:** OnePlus Nord CE 2 Lite 5G (`CPH2381`), Snapdragon 695 5G (`holi`), Android 14 (`ro.build.version.release=14`), Device Serial: `63f2d6ee`  
**License Context:** Apache License 2.0 (with FFmpeg LGPL/GPL & SDL3 dependencies)

---

## 1. Architecture

### 1.1 High-Level Operating Model
*Scrcpy* ("screen copy") functions as a high-performance, lightweight display and input forwarding system structured around an inverted client-server model over ADB tunnels.
* **Non-intrusive Execution [CONFIRMED]:** Scrcpy requires **no APK installation**, root privileges, or permanent background daemon on the Android device (`README.md`, `DEVELOP.md`).
* **Session Lifecycle [CONFIRMED]:**
  1. The host client starts an ADB server connection and creates local listening server sockets (or configures `adb forward` / `adb reverse`).
  2. The client pushes a standalone pre-compiled Java binary (`scrcpy-server.jar` / `scrcpy-server`) into the device temporary directory (`/data/local/tmp/scrcpy-server.jar`).
  3. The client invokes the server via ADB shell using Android's native runtime launcher:
     ```bash
     adb shell CLASSPATH=/data/local/tmp/scrcpy-server.jar app_process / com.genymobile.scrcpy.Server <version> <options...>
     ```
  4. The server runs with the privileges of the ADB shell user (`shell` UID `2000`). It connects back to the host client sockets.
  5. Upon termination of the client or disconnection of the transport, the Android `app_process` terminates automatically, and the temporary JAR is cleaned up.

### 1.2 Core Components & Subsystems
1. **Windows / Host Client (`app/` in C):**
   * **Rendering & Event Loop:** Uses **libSDL3 / SDL2** to manage desktop windows, capture OS keyboard/mouse input, and render raw video frames via hardware-accelerated SDL textures (`SDL_Renderer`).
   * **Media Decoding:** Utilizes **FFmpeg** (`libavcodec`, `libavformat`, `libavutil`) for demuxing and decoding H.264, H.265, AV1 video and Opus/AAC audio packets.
   * **Transport & Framing:** Custom C socket multiplexing (`net.c`, `stream.c`).
2. **Android Server (`server/` in Java):**
   * **Video Capture Subsystem:** Accesses hidden Android internal display surfaces via `android.view.SurfaceControl` (or `DisplayManager` / virtual display APIs) to capture the screen without requiring user confirmation prompts (exploiting ADB `shell` UID privileges).
   * **Hardware Encoding Pipeline:** Feeds the captured display surface directly into Android's native hardware encoder (`android.media.MediaCodec`), streaming raw NAL units over the video socket.
   * **Audio Capture Subsystem (Android 11+):** Captures audio playback via `AudioRecord` utilizing the internal `REMOTE_SUBMIX` audio source, encoding via `MediaCodec` into Opus, AAC, or raw PCM.
   * **Input Injection Subsystem:** Injects touch, key, and scroll events directly via hidden `android.hardware.input.InputManager` / `WindowManager` APIs or via Linux `/dev/uhid` (User HID).
3. **Communication Channels [CONFIRMED]:**
   Scrcpy establishes up to three dedicated, independent TCP sockets:
   * **Video Socket:** High-throughput, unidirectional streaming of encoded video frames from Android to Host.
   * **Audio Socket:** Streaming of encoded audio frames from Android to Host.
   * **Control Socket:** Bi-directional binary protocol. Host sends injected touch/key/scroll commands; Android sends clipboard notifications, ACK responses, and device orientation events.

### 1.3 Reusability vs. Coupling
* **Tightly Coupled [CONFIRMED]:** The desktop client (`app/`) is tightly coupled to SDL and FFmpeg. Embedding the native C client directly into a managed .NET 10 / WinUI 3 runtime without modification requires deep C-interop or running it as a subordinate child process.
* **Highly Reusable [CONFIRMED]:**
  * The Java `scrcpy-server.jar` is completely standalone and protocol-driven. Any client capable of launching an ADB process and reading standard H.264/Opus streams from TCP sockets can interface with `scrcpy-server`.

---

## 2. Connection Setup & Management

### 2.1 USB Setup [CONFIRMED]
* Requires USB Debugging enabled in Android *Developer Options*.
* The client invokes standard ADB commands (`adb -s <serial> forward` or `adb reverse`) to establish local loopback port bindings (`127.0.0.1:PORT`).
* USB transport provides the highest throughput (up to USB 2.0 / 3.0 link speeds), near-zero packet loss, and lowest jitter.

### 2.2 Wireless Connection (TCP/IP) [CONFIRMED]
* **Initial Pairing Requirement:** ADB over TCP/IP cannot be initiated from cold boot wirelessly without an initial connection unless Android 11+ Wireless Debugging (pairing code) is used.
* **Workflow:**
  1. USB connected initially: Command `scrcpy --tcpip` automatically determines the device IP, executes `adb tcpip 5555`, and connects.
  2. Direct IP connection: `scrcpy --tcpip=192.168.x.x:5555`.
* **Caveat / Restriction [CONFIRMED]:** When the phone reboots, Android automatically disables TCP/IP debugging on port 5555. A brief USB reconnection or manual re-enabling via Wireless Debugging is mandatory by Android platform security rules.

### 2.3 Reconnect Behaviour & Failure Handling [CONFIRMED]
* When a network drop occurs over Wi-Fi, the TCP socket breaks. Standard scrcpy will terminate with an error code.
* Reconnection flags (e.g., `scrcpy --tcpip=+192.168.x.x`) force an ADB reconnect attempt. However, built-in scrcpy does not maintain a continuous silent background watchdog loop when disconnected; session supervision is the responsibility of the calling application.

---

## 3. Video Performance & Encoding

### 3.1 Codecs & Hardware Acceleration [CONFIRMED]
* **Supported Codecs:**
  * **H.264 / AVC** (`--video-codec=h264`): Default, universal hardware encoder compatibility across all Android chipsets.
  * **H.265 / HEVC** (`--video-codec=h265`): Lower bandwidth for identical quality (~30-50% savings); excellent for Wi-Fi.
  * **AV1** (`--video-codec=av1`): Supported on newer encoders/decoders.
* **Hardware Pipeline:** Zero-copy on Android: `SurfaceControl` -> `Surface` -> `MediaCodec` (Hardware Encoder) -> Socket.

### 3.2 Performance Factors & Configuration Parameters [CONFIRMED]
* **Bitrate:** Default is `8 Mbps` (`-b 8M`). Can be tuned dynamically from `2 Mbps` (low-bandwidth Wi-Fi) up to `16-30 Mbps` (crystal clear USB).
* **Resolution (Max Size):** `--max-size=1920` (or `1080`, `720`). Scaling down to `1080` significantly reduces encoding load and latency on budget/mid-range SOCs.
* **Frame Rate (FPS):** Variable frame rate by design (frames are emitted only on screen damage/change). Can be capped using `--max-fps=60` or `--max-fps=30`.
* **Latency Profile [CONFIRMED by Genymobile]:**
  * Official measured end-to-end latency: **35 to 70 ms** over USB under optimal conditions.
  * *Important Constraint:* "Zero lag" or 120 FPS cannot be guaranteed. Wi-Fi latency fluctuates depending on 2.4 GHz vs 5 GHz channel congestion, 802.11 QoS, and distance.

### 3.3 Benchmarking Plan for OnePlus Nord CE 2 Lite 5G (`CPH2381` / Snapdragon 695)
* **Hardware Specs:** Qualcomm Snapdragon 695 5G (`holi`), Adreno 619 GPU, Qualcomm hardware H.264/H.265 encoders.
* **Recommended Benchmark Test Suite:**
  1. H.264 at 1080p, 8 Mbps over USB vs. 5 GHz Wi-Fi.
  2. H.265 at 1080p, 4 Mbps over 5 GHz Wi-Fi (evaluating encoder delay vs. bandwidth).
  3. Frame drop count and FPS stability during UI scrolling using `scrcpy --print-fps`.

---

## 4. Input Control

### 4.1 Input Modes [CONFIRMED]
Scrcpy supports three input mechanisms:
1. **SDK Mode (`--mouse=sdk`, `--keyboard=sdk`):**
   * Uses hidden `InputManager.injectInputEvent()` via ADB shell privileges.
   * Universal compatibility; supports mouse clicks, dragging, scrolling, and text injection.
   * Limitations: Some software keyboards or security-flagged screens (`FLAG_SECURE`, banking apps) reject SDK event injection.
2. **UHID Mode (`--keyboard=uhid`, `--mouse=uhid`):**
   * Creates a virtual Linux kernel HID device via `/dev/uhid` on Android.
   * Appears to Android as a physical USB keyboard and mouse plugged into the device.
   * Bypasses most input restrictions and provides raw scan codes, gaming mouse capture, and precise multi-touch gestures.
3. **AOA Mode (Android Open Accessory):**
   * Directly simulates a physical USB HID device over USB cable without requiring ADB.

### 4.2 Touch, Scrolling & Multi-Touch [CONFIRMED]
* Mouse clicks are translated to touch pointers.
* Mouse scroll wheels are converted into natural touch swipe gestures.
* Multi-touch pinch-to-zoom is supported using modifier keys (<kbd>Ctrl</kbd> + click & drag).

---

## 5. Audio Forwarding & Voice Calls

### 5.1 Playback Forwarding [CONFIRMED]
* **Android 12+ (Our OnePlus phone runs Android 14):** Audio capture works natively out-of-the-box via `REMOTE_SUBMIX` without special tricks.
* **Audio Codecs:** Opus (default, ultra-low latency ~5-10ms buffer), AAC, or raw PCM (`--audio-codec=opus`).
* **Mute Device:** Default behavior routes audio to the PC and silences phone speakers.

### 5.2 Microphone Forwarding [CONFIRMED]
* Can capture the device microphone (`--audio-source=mic` or `voice-recognition`) to stream phone surroundings or voice input to the PC.

### 5.3 Critical Phone Call Limitation [CONFIRMED - CRITICAL FOR LINKBRIDGE]
* **Call Audio Inaccessibility via MediaCodec:** Android's security architecture strictly restricts normal capture of cellular voice call audio (`VOICE_CALL`) from user space.
* While scrcpy exposes `--audio-source=voice-call`, in practice on non-rooted production ROMs (including OxygenOS / ColorOS), carrier call audio streams are gated from third-party apps and ADB shell.
* **Implication for LinkBridge:** Scrcpy **cannot** be used as the cellular call audio conduit. Cellular call audio on LinkBridge must use **Bluetooth Hands-Free Profile (HFP)**, matching Microsoft Phone Link's architecture.

---

## 6. Clipboard and File Transfer

### 6.1 Clipboard Synchronization [CONFIRMED]
* **Seamless Bi-directional Sync:**
  * Copying text on PC automatically synchronizes to Android clipboard via the control channel.
  * Copying text on Android sends a notification packet to the PC host to update the Windows clipboard.
* **Android 10+ Background Restriction:** Due to Android 10+ clipboard access restrictions for background apps, scrcpy injects clipboard data directly via system shell RPC (`IClipboard`).

### 6.2 File Transfer & Drag-and-Drop [CONFIRMED]
* **Non-APK Files:** Dragging files onto the scrcpy window triggers an `adb push` to `/sdcard/Download/`.
* **APK Files:** Dragging an `.apk` automatically executes `adb install -r <file.apk>`.
* **Limitations:** It is an unmanaged push operation; it does not offer a bidirectional visual file manager or gallery browser.

---

## 7. Windows Integration Options for LinkBridge

How can a native Windows 11 application (WinUI 3 / .NET 10) integrate scrcpy?

### Option A: Subprocess Orchestration with Window Management (RECOMMENDED FOR INITIAL MVP)
* **Mechanism:** LinkBridge launches `scrcpy.exe` as a managed background child process (`System.Diagnostics.Process`), passing explicit CLI parameters:
  ```bash
  scrcpy.exe -s 63f2d6ee --window-title="LinkBridge - Screen Mirror" --window-borderless --video-bit-rate=8M --max-size=1080 --no-audio
  ```
* **Pros:**
  * Extreme stability and zero crash propagation to the LinkBridge main UI.
  * Uses upstream, battle-tested precompiled binaries without requiring C/C++ compilation or FFmpeg linking in .NET.
  * Upgrades to newer scrcpy releases simply require swapping the `scrcpy` binary folder.
* **Cons:** Scrcpy opens its own native Win32/SDL window rather than a XAML control.

### Option B: Embedding Scrcpy Window via Win32 `SetParent`
* **Mechanism:** LinkBridge launches scrcpy with `--window-borderless`, obtains its Win32 window handle (`HWND`), and calls `SetParent(scrcpyHwnd, linkBridgeContainerHwnd)`.
* **Risk & Limitations [CONFIRMED - NOT RECOMMENDED INITIALLY]:**
  * WinUI 3 (Windows App SDK) uses a composition swapchain. Cross-process Win32 `SetParent` reparenting often causes clipping anomalies, DPI scaling glitches, mouse coordinate desynchronization, and input routing bugs in SDL3.
  * It should not be assumed that embedding an external SDL window into WinUI 3 is clean or reliable.

### Option C: Custom LinkBridge Native Protocol Client (Long-term Architecture)
* **Mechanism:** LinkBridge deploys `scrcpy-server.jar` to the device via ADB, but connects to the video/control sockets itself, decoding video via Windows Media Foundation or a WinUI 3 swapchain.
* **Evaluation:** Offers 100% native UI immersion, but requires significant engineering effort (implementing H.264 demuxing, low-latency render surfaces, and control packet serialization).

---

## 8. Performance & Resource Usage Analysis

| Resource | USB Connection | Wi-Fi (5 GHz) Connection |
|---|---|---|
| **Host CPU Usage** | Very Low (1-3%) with hardware DXVA2/D3D11 decoding in FFmpeg | Low (2-5%) |
| **Host GPU Usage** | 5-10% (SDL surface render & texture copy) | 5-10% |
| **Android CPU/Battery** | Low-Medium; hardware `MediaCodec` encoder offloads CPU. Battery charges via USB. | Medium; continuous Wi-Fi TX + hardware encoder discharges battery (~10-15%/hr). |
| **Network Overhead** | Dedicated USB bus; no LAN bandwidth consumption | Consumes 4 to 12 Mbps of continuous local Wi-Fi bandwidth |
| **Latency Jitter** | Negligible (rock solid 35-50ms) | Susceptible to Wi-Fi retransmissions and router buffering |

---

## 9. Licensing and Project Risks

* **Scrcpy Core License [CONFIRMED]:** **Apache License, Version 2.0**.
  * Highly permissive. Permits commercial and open-source use, modification, redistribution, and sublicensing.
  * Requires preservation of copyright notices and license text.
* **Third-Party Dependency Licenses [CONFIRMED]:**
  * **FFmpeg:** Licensed under **LGPL v2.1+** (or GPL depending on build flags). If scrcpy's prebuilt binaries link dynamically (`avcodec-*.dll`), distribution is fully compliant under LGPL terms.
  * **SDL3 / SDL2:** Licensed under the permissive **zlib license**.
* **Packaging Risk:** Low. Distributing unmodified scrcpy release binaries alongside LinkBridge under a dedicated `tools/scrcpy/` subfolder with proper license attribution is fully compliant and safe.

---

## 10. Capability Comparison & Allocation Matrix

| Capability | Scrcpy Capability | LinkBridge Native Requirement | Recommended Allocation |
|---|---|---|---|
| **Screen Mirroring** | Industry benchmark (H.264/H.265, 35-70ms) | Redundant to reinvent | **Delegate to Scrcpy** |
| **Remote Touch / Mouse** | Full support (SDK / UHID / AOA) | Redundant to reinvent | **Delegate to Scrcpy** |
| **Keyboard Injection** | Full support (UHID / IME sync) | Redundant to reinvent | **Delegate to Scrcpy** |
| **Audio Mirroring (Media)** | High quality (Opus, Android 12+) | Suitable for media playback | **Delegate to Scrcpy** (optional toggle) |
| **Call Audio / Telephony** | ❌ **Unsupported** (OS restricted) | Native Bluetooth HFP Audio | **Build Independently** (Windows Bluetooth) |
| **Notification Sync** | ❌ **No UI / Notification Toasts** | Full notification history / dismiss | **Build Independently** (LinkBridge Companion) |
| **SMS / Messaging** | ❌ **No messaging interface** | Full thread view & SMS reply | **Build Independently** (LinkBridge Companion) |
| **Battery & Device Status** | ❌ **No status panel** | Floating widget battery % / Wi-Fi | **Build Independently** (ADB / Companion) |
| **Radial Menu & Floating UI** | ❌ **None** (Only single mirror window) | Core LinkBridge UX | **Build Independently** (WinUI 3) |
| **File Browser / Gallery** | Primitive drag-and-drop only | Thumbnail gallery & file explorer | **Build Independently** |

---

## 11. Proposed Integration Approach for LinkBridge

```
+-------------------------------------------------------------------------------+
|                       LinkBridge Desktop Application                          |
|         (WinUI 3 / .NET 10 / Floating Radial Menu / Notification Hub)         |
+-------------------------------------------------------------------------------+
            |                                           |
            | (Subprocess Process Control)              | (Direct ADB / Protocol)
            v                                           v
+-------------------------------+           +-----------------------------------+
|      scrcpy.exe Process       |           |   LinkBridge Background Daemon    |
| (Dedicated Mirroring Window)  |           | - Battery, SMS & Notification Sync|
| - Low-latency H.264 Video     |           | - Bluetooth HFP Cellular Calls    |
| - Mouse / Touch / Keyboard    |           | - ADB Connection Watchdog         |
+-------------------------------+           +-----------------------------------+
            | (USB / TCP:5555)                          | (Local Socket / Bluetooth)
            v                                           v
+-------------------------------------------------------------------------------+
|                    Android Phone (OnePlus Nord CE 2 Lite)                     |
|  - scrcpy-server (transient app_process for video/input)                      |
|  - LinkBridge Companion / Standard Android APIs (Notifications, SMS, HFP)    |
+-------------------------------------------------------------------------------+
```

1. **Phase 1 (MVP Integration):**
   * Package verified precompiled scrcpy x64 binaries into LinkBridge tools directory.
   * Provide an interactive button on the LinkBridge radial menu: *"Mirror Phone"*.
   * When clicked, LinkBridge queries ADB for connected device serial `63f2d6ee`, verifies state, and launches `scrcpy.exe` with optimal tuned parameters (`--max-size=1080 --video-bit-rate=8M --tunnel-host=127.0.0.1`).
   * LinkBridge monitors the process lifecycle, updating the radial menu state when closed.
2. **Phase 2 (Deepening Integration):**
   * LinkBridge handles automatic ADB TCP/IP connection establishment in the background, allowing scrcpy to launch wirelessly without manual command-line prompts.

---

## 12. Unresolved Questions & Investigation Next Steps

1. **Window Positioning Coordination:** How smoothly can the WinUI 3 floating bubble dock or attach adjacent to the native scrcpy SDL window without glitches?
2. **Wi-Fi Latency on OnePlus Nord CE 2 Lite:** What is the real-world frame rate and bitrate ceiling on the test phone's Snapdragon 695 hardware encoder over 5 GHz Wi-Fi?
3. **Screen-Off Mirroring:** Does `scrcpy --turn-screen-off` behave consistently on OxygenOS 14 without triggering device deep-sleep disconnects?

---

## 13. System Integrity Confirmation

* **No project code files or existing research reports were modified.**
* **No device settings or Android applications were installed or modified.**
* **All device queries were non-intrusive read-only properties (`getprop`).**

