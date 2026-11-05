# Initial Inspection Report: Microsoft Phone Link (Microsoft.YourPhone)

**Date:** 2026-11-05  
**Target Package:** `Microsoft.YourPhone`  
**Target Version:** `1.26092.119.0` (x64)  
**Package Path:** `C:\Program Files\WindowsApps\Microsoft.YourPhone_1.26092.119.0_x64__8wekyb3d8bbwe`  
**Tooling Environment:** REA (Reverse Engineer Anything), Windows PowerShell, .NET Host Profiling, WinRT Inspection  

---

## A. Findings

### 1. Application Type and Technology Stack
* **Application Framework:** Packaged Windows Desktop App (UWP/WinAppSDK packaged container, targeting Windows Desktop `10.0.22631.0` - `10.0.26100.0`).
* **Execution Environment:** **.NET 10 (net10.0 / Microsoft.NETCore.App 10.0.14)** managed runtime using CoreCLR (`coreclr.dll`, `clrjit.dll`, `clrgc.dll`, `hostfxr.dll`).
* **UI Framework:** **WinUI 3 / Windows App SDK (Microsoft.WindowsAppRuntime.2)** with CsWinRT projections (`Microsoft.Windows.SDK.NET.dll`, `WinRT.Runtime.dll`).
* **No WebView2 Dependency Detected:** There are no WebView2 loader or Edge Chromium runtime assemblies embedded or referenced in `PhoneExperienceHost.deps.json`. The user interface is rendered primarily using native XAML / WinUI 3 controls.
* **Storage Stack:** SQLite (`Microsoft.Data.Sqlite.dll`, `e_sqlite3.dll`).
* **Networking & Transport:**
  * **Protobuf & Serialization:** `Google.Protobuf.dll`, `MessagePack.dll`.
  * **Asynchronous Networking:** DotNetty (`DotNetty.Buffers.dll`, `DotNetty.Codecs.dll`, `DotNetty.Codecs.Protobuf.dll`, `DotNetty.Common.dll`, `DotNetty.Transport.dll`).
  * **Real-time WebSockets / Push:** ASP.NET Core SignalR Client (`Microsoft.AspNetCore.SignalR.Client.dll`, `Microsoft.AspNetCore.SignalR.Client.Core.dll`).
  * **HTTP Resilience:** `Microsoft.Extensions.Http.Polly.dll`, `Microsoft.Extensions.Http.Resilience.dll`.
  * **API Client Layer:** Microsoft Kiota client generator stack (`YourPhone.YPP.Kiota.dll`, `Microsoft.Kiota.Abstractions.dll`).

---

### 2. Executables & Entry Points
The package contains three main executables:

1. **`PhoneExperienceHost.exe`** (355 KB)
   * **Role:** Primary UI Application and Main Process.
   * **Manifest Definition:** Declared as `<Application Id="App" Executable="PhoneExperienceHost.exe" EntryPoint="Windows.FullTrustApplication">`.
   * **Launch Profiles:** Full trust desktop application hosting the .NET 10 runtime (`PhoneExperienceHost.runtimeconfig.json`).
   * **Activation Handles:**
     * Protocol URIs: `ms-phone:`, `tel:`, `sms:`
     * COM Server classes: Hosts out-of-process COM activation endpoints.
     * Startup Task & Share Target.
2. **`YourPhoneAppProxyHost.exe`** (355 KB)
   * **Role:** Remote app proxy / streaming host.
   * **Manifest / Background Support:** Accompanied by `YourPhoneAppProxyHost.runtimeconfig.json`, `YourPhoneAppProxy.Core.dll`, and `YourPhone.Contracts.AppProxyConnection.ProxyStub.dll`.
   * **Function:** Responsible for handling phone screen mirroring, remote Android application windowing, and app projection.
3. **`createdump.exe`** (71 KB)
   * **Role:** Standard .NET diagnostic / crash dump utility.

---

### 3. Native vs. Managed Architecture Split
The application architecture is a **hybrid managed-core architecture with native WinRT shims**:
* **Managed Components (.NET 10 Assemblies):**
  * Core business logic is partitioned into modular assemblies:
    * `YourPhone.AppCore.Managed.dll`
    * `YourPhone.Connectivity.Managed.dll`
    * `YourPhone.Connectivity.Bluetooth.Managed.dll`
    * `YourPhone.Connectivity.YPP.Managed.dll`
    * `YourPhone.ScreenMirroring.Managed.dll`
    * `YourPhone.ScreenMirroring.Managed.Protocol.dll`
    * `YourPhone.Messaging.Managed.dll`
    * `YourPhone.Calling.Managed.dll`
    * `YourPhone.Notifications.Managed.dll`
    * `YourPhone.Photos.Managed.dll`
    * `YourPhone.Contacts.Managed.dll`
    * `YourPhone.Hotspot.Managed.dll`
    * `YourPhone.SharedContent.Managed.dll`
* **Native / WinRT Projections:**
  * `LibNanoAPI.dll` (7.8 MB native C++ DLL) & `LibNanoAPI.winmd` / `LibNanoAPI.Projection.dll`: Proprietary engine likely handling low-level media streaming, screen mirroring decoding, or hardware accelerated pipelines.
  * `ConnectedDevicesDevKit.Projection.dll` & `ConnectedDevicesDevKit.PhoneLink.Projection.dll`: Project Rome / Microsoft Connected Devices Platform integration.
  * `YourPhone.AppCore.WinRT.dll`, `YourPhone.Exp.WinRT.dll`: Native WinRT boundary components.
  * COM Proxy/Stubs: `YourPhone.Contracts.AppProxyConnection.ProxyStub.dll`, `YourPhone.Contracts.Background.ProxyStub.dll`.

---

### 4. Architectural Model
Based on component layout and dependency declarations:
```
+-----------------------------------------------------------------------------------+
|                            PhoneExperienceHost.exe                                |
|  (WinUI 3 / Windows App SDK 2.4 / .NET 10 CoreCLR Runtime / FullTrustApplication) |
+-----------------------------------------------------------------------------------+
       |                                       |                               |
       v                                       v                               v
[ UI & Feature Modules ]            [ Communication Subsystem ]     [ Media & Screen Stream ]
- YourPhone.Messaging.Managed       - YourPhone.Connectivity.*      - YourPhone.ScreenMirroring
- YourPhone.Calling.Managed         - DotNetty (Protobuf transport) - YourPhoneAppProxyHost.exe
- YourPhone.Notifications.Managed   - SignalR Client (Cloud/Relay)  - LibNanoAPI.dll (Native)
- YourPhone.Photos.Managed          - Windows Bluetooth WinRT APIs  - Win2D / Direct3D Canvas
- YourPhone.Contacts.Managed        - ConnectedDevicesPlatform (CDP)
       |                                       |
       +-------------------+-------------------+
                           |
                           v
            [ Local SQLite & App State ]
            - e_sqlite3.dll / Microsoft.Data.Sqlite
```

---

## B. Evidence and File Locations

| Category | File / Path | Evidence Details |
|---|---|---|
| **Package Manifest** | `AppxManifest.xml` | Package ID: `Microsoft.YourPhone`, Entry Point: `PhoneExperienceHost.exe` (`Windows.FullTrustApplication`), Protocols: `ms-phone`, `tel`, `sms` |
| **Runtime Configuration** | `PhoneExperienceHost.runtimeconfig.json` | TFM: `net10.0`, Framework: `Microsoft.NETCore.App` (10.0.14), CsWinRT configured |
| **Host Configuration** | `YourPhoneAppProxyHost.runtimeconfig.json` | TFM: `net10.0`, dedicated host for projection/streaming |
| **Protocols & Networking** | `Google.Protobuf.dll`<br>`DotNetty.Transport.dll`<br>`DotNetty.Codecs.Protobuf.dll` | Protobuf serialization over custom DotNetty TCP/transport pipelines |
| **Relay / Cloud** | `Microsoft.AspNetCore.SignalR.Client.dll`<br>`YourPhone.YPP.Kiota.dll` | SignalR protocol client, generated Kiota HTTP API clients |
| **Screen Streaming** | `LibNanoAPI.dll`<br>`LibNanoAPI.winmd`<br>`YourPhone.ScreenMirroring.Managed.Protocol.dll` | Native frame decode / screen projection engine |
| **Local Persistence** | `Microsoft.Data.Sqlite.dll`<br>`e_sqlite3.dll` | Embedded relational database engine |

---

## C. REA Tools Used

* **REA Target Discovery & File Structure:** Inspected directory layout, file manifests, and binaries under `C:\Program Files\WindowsApps\Microsoft.YourPhone_1.26092.119.0_x64__8wekyb3d8bbwe`.
* **Manifest & Metadata Inspection:** Evaluated `AppxManifest.xml` (Capabilities, Extension Points, COM servers, Protocols).
* **Managed Runtime Inspection:** Evaluated `.deps.json` and `.runtimeconfig.json` configurations for CoreCLR targeting.
* **Component Classification:** Categorized components into Native PE32+, Managed CIL (.NET 10), and WinMD projection layers.

---

## D. Unknowns and Limitations

1. **Protocol Serialization Schemas:** While Protobuf and MessagePack libraries are present, the exact `.proto` schemas and message structures used between Windows and the "Link to Windows" Android companion application are embedded inside the managed DLLs (`YourPhone.Connectivity.Protocol.dll`, `YourPhone.ScreenMirroring.Managed.Protocol.dll`, etc.) and have not yet been extracted.
2. **Transport Channel Multiplexing:** It remains to be determined under which conditions Phone Link communicates via direct local Wi-Fi / TCP sockets (DotNetty), Bluetooth RFCOMM / GATT (`YourPhone.Connectivity.Bluetooth.*`), or Microsoft cloud relay services (SignalR / YPP endpoints).
3. **Role of LibNanoAPI.dll:** The exact internal interface and function of `LibNanoAPI.dll` (7.8 MB native) needs clarification (e.g., whether it implements proprietary WebRTC/RTP streaming or custom low-latency video streaming).
4. **Android Side Contract:** The corresponding Android APK (`com.microsoft.appmanager`) behavior and endpoints are not yet analyzed in this step.

---

## E. Implications for LinkBridge Architecture

1. **Pure Open Protocol Replacement Feasibility:**
   * Because Phone Link uses **Protobuf schemas** and standard networking primitives (DotNetty TCP sockets, SignalR, standard Bluetooth profiles), LinkBridge can reverse-engineer the `.proto` contracts and implement a clean, lightweight alternative daemon without depending on proprietary Microsoft libraries.
2. **Screen Mirroring & Media Streaming:**
   * The presence of `LibNanoAPI.dll` and `YourPhone.ScreenMirroring.Managed.Protocol.dll` indicates screen mirroring uses a specialized video streaming protocol over local transport. LinkBridge will need to support H.264/H.265 video decoding or adopt standard scrcpy / WebRTC conventions if building a standalone Android bridge.
3. **Bluetooth Integration:**
   * Phone Link leverages Windows Bluetooth capabilities (`bluetooth`, `radios`, `phoneLineTransportManagement`) for cellular calls (HFP/A2DP audio and Handsfree Profile). LinkBridge can implement standard Bluetooth call handling using Windows native Bluetooth APIs.
4. **Modularity Advantage:**
   * Phone Link cleanly separates Contracts (`YourPhone.Contracts.*`), Protocols (`*.Protocol.dll`), and UI (`*.Managed.dll`). LinkBridge should adopt a similar clean architecture separating protocol engines from UI presentation.

---

## F. Investigation Opportunities for Communication with Android

1. **Protobuf Contract Extraction:**
   * Analyze `YourPhone.Connectivity.Protocol.dll`, `YourPhone.Devices.Protocol.dll`, and `YourPhone.ScreenMirroring.Managed.Protocol.dll` to reconstruct the exact Protobuf message definitions.
2. **Channel Establishment & Discovery Workflow:**
   * Inspect `YourPhone.Connectivity.Managed.dll` and `YourPhone.YPP.*` to map the pairing, handshake, and authentication lifecycle (Bluetooth discovery vs. QR code / Wi-Fi LAN / Microsoft account pairing).
3. **Data Sync Protocols (SMS, Notifications, Contacts):**
   * Inspect the message handler implementations in `YourPhone.Messaging.Managed.dll` and `YourPhone.Notifications.Managed.dll` to identify packet formats and sync semantics.
4. **Companion App Correlation:**
   * Obtain and inspect the Android companion app APK (`com.microsoft.appmanager` / "Link to Windows") to cross-reference packet handlers and RPC methods against the Windows host findings.

