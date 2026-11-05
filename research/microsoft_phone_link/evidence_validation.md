# Evidence-Validation Review: Microsoft Phone Link Inspection

**Target Application:** Microsoft Phone Link (`Microsoft.YourPhone`)  
**Package Version:** `1.26092.119.0` (x64)  
**Installation Path:** `C:\Program Files\WindowsApps\Microsoft.YourPhone_1.26092.119.0_x64__8wekyb3d8bbwe`  
**Review Date:** 2026-11-05  
**Review Scope:** Strict verification of claims in `initial_inspection_report.md` against observable filesystem, manifest, and runtime configuration evidence without runtime modification or decompilation.

---

## Technical Findings Validation Matrix

### 1. .NET 10 and CoreCLR 10.0.14
* **Original Claim:** The application runs on managed .NET 10 (`net10.0`) using CoreCLR (`coreclr.dll`, `clrjit.dll`, `clrgc.dll`, `hostfxr.dll`), framework version `10.0.14`.
* **Evidence File & Location:**
  * `PhoneExperienceHost.runtimeconfig.json`:
    ```json
    "runtimeOptions": {
      "tfm": "net10.0",
      "includedFrameworks": [
        {
          "name": "Microsoft.NETCore.App",
          "version": "10.0.14"
        }
      ]
    }
    ```
  * Filesystem binaries: `coreclr.dll` (4,613,456 bytes), `clrjit.dll` (2,095,912 bytes), `clrgc.dll` (674,088 bytes), `hostfxr.dll` (379,728 bytes).
* **Evidence Type:** Runtime configuration (`.runtimeconfig.json`) and physical CoreCLR host binaries.
* **Verification Status:** **Confirmed**
* **What the Evidence Proves:** The execution host is explicitly configured to initialize the .NET 10 CoreCLR runtime engine packaged directly within the application directory (self-contained/framework-dependent hybrid deployment).
* **Assumptions / Non-Facts:** None. The runtime version is unequivocally defined in the host config and on-disk binaries.

---

### 2. WinUI 3 and Windows App SDK 2.4
* **Original Claim:** The UI framework is WinUI 3 / Windows App SDK (Microsoft.WindowsAppRuntime.2, version 2.4).
* **Evidence File & Location:**
  * `AppxManifest.xml` (`<PackageDependency>`):
    ```xml
    <PackageDependency Name="Microsoft.WindowsAppRuntime.2" MinVersion="2.4.0.0" Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" />
    ```
  * `PhoneExperienceHost.deps.json`: Contains target entry `"lib/net6.0-windows10.0.17763.0/Microsoft.WinUI.dll"` with assembly version `3.0.0.0` and file version `3.0.0.2608`.
  * Filesystem binary: `Microsoft.WinUI.dll` (3,192,632 bytes).
* **Evidence Type:** Package manifest dependency definition and dependency graph (`.deps.json`).
* **Verification Status:** **Confirmed**
* **What the Evidence Proves:** The application declares a hard package dependency on Windows App SDK 2.4 runtime and references `Microsoft.WinUI.dll` v3.0 for its UI controls.
* **Assumptions / Non-Facts:** None.

---

### 3. Absence of WebView2
* **Original Claim:** There is no WebView2 loader or Edge Chromium runtime assemblies embedded or referenced; UI is rendered natively.
* **Evidence File & Location:**
  * Recursive filesystem search: Zero files matching `*WebView*` or `*WebView2*` found in the installation folder or subdirectories.
  * `PhoneExperienceHost.deps.json`: Zero matches for `WebView` or `Microsoft.Web.WebView2`.
* **Evidence Type:** Exhaustive negative check in dependency graph and filesystem index.
* **Verification Status:** **Confirmed** (within local package boundary)
* **What the Evidence Proves:** Phone Link does not package or statically link the standard `Microsoft.Web.WebView2.Core` or Edge WebView2 SDK.
* **Assumptions / Non-Facts:** While the primary UI is native WinUI 3 and not an embedded web app, we cannot rule out dynamic COM activation of system browser controls or external browser handoffs for authentication without runtime profiling.

---

### 4. PhoneExperienceHost.exe as Application Entry Point
* **Original Claim:** `PhoneExperienceHost.exe` is the primary executable and full-trust application entry point.
* **Evidence File & Location:**
  * `AppxManifest.xml`:
    ```xml
    <Application Id="App" Executable="PhoneExperienceHost.exe" EntryPoint="Windows.FullTrustApplication">
    ```
* **Evidence Type:** Appx / MSIX application manifest definition.
* **Verification Status:** **Confirmed**
* **What the Evidence Proves:** When the user launches the Phone Link application or it is activated via OS contracts (`ms-phone:`, `tel:`, `sms:`), the OS loader invokes `PhoneExperienceHost.exe` with mediumIL full-trust permissions.
* **Assumptions / Non-Facts:** None.

---

### 5. YourPhoneAppProxyHost.exe and Its Purpose
* **Original Claim:** `YourPhoneAppProxyHost.exe` is a remote app proxy / streaming host responsible for screen mirroring and Android app window projection.
* **Evidence File & Location:**
  * `AppxManifest.xml`: Declares two `windows.hostRuntime` extensions with `Executable="YourPhoneAppProxyHost.exe"` and IDs `YourPhoneAppProxy` and `YourPhoneAppProxyHost`.
  * Filesystem binaries: `YourPhoneAppProxyHost.exe` (355,640 bytes), `YourPhoneAppProxyHost.runtimeconfig.json`, `YourPhoneAppProxy.Core.dll`, `YourPhone.Contracts.AppProxyConnection.ProxyStub.dll`.
  * Folder `Assets\AppProxy`: Contains visual icon assets for projected app windows.
* **Evidence Type:** Appx manifest runtime declaration and related binary assembly naming.
* **Verification Status:** **Partially Confirmed**
* **What the Evidence Proves:** `YourPhoneAppProxyHost.exe` is an independent packaged classic runtime host executable registered to host proxy connections via COM/WinRT stubs.
* **Assumptions / Non-Facts:** Specifying that it handles *screen mirroring video decoding* is an unverified operational inference. While its name and stub associations strongly point to app projection and proxying, the exact distribution of work between this process and `PhoneExperienceHost.exe` requires dynamic process inspection.

---

### 6. SQLite Usage
* **Original Claim:** SQLite is used for local data persistence (`Microsoft.Data.Sqlite.dll`, `e_sqlite3.dll`).
* **Evidence File & Location:**
  * Filesystem binaries: `e_sqlite3.dll` (1,454,592 bytes) and `Microsoft.Data.Sqlite.dll` (176,128 bytes).
  * `PhoneExperienceHost.deps.json`: Declares package `"Microsoft.Data.Sqlite.Core/10.0.12"` depending on `"SQLitePCLRaw.core": "3.0.5"`, and references `lib/net8.0/Microsoft.Data.Sqlite.dll`.
* **Evidence Type:** Dependency graph and physical library existence.
* **Verification Status:** **Confirmed**
* **What the Evidence Proves:** The application includes the SQLite client library and native engine (`e_sqlite3.dll`) as a direct dependency.
* **Assumptions / Non-Facts:** The exact schema, database file path (e.g., in `%LocalAppData%\Packages\Microsoft.YourPhone_...`), and whether all data domains (SMS, contacts, photos) use this single database cannot be established from binary presence alone.

---

### 7. DotNetty and TCP Transport
* **Original Claim:** Asynchronous networking is handled via DotNetty over TCP sockets (`DotNetty.Buffers.dll`, `DotNetty.Codecs.dll`, `DotNetty.Codecs.Protobuf.dll`, `DotNetty.Common.dll`, `DotNetty.Transport.dll`).
* **Evidence File & Location:**
  * Filesystem: `DotNetty.Buffers.dll`, `DotNetty.Codecs.dll`, `DotNetty.Codecs.Protobuf.dll`, `DotNetty.Common.dll`, `DotNetty.Transport.dll`.
  * `PhoneExperienceHost.deps.json`: References `DotNetty.*` v0.7.0.
* **Evidence Type:** Dependency graph and assembly presence.
* **Verification Status:** **Partially Confirmed**
* **What the Evidence Proves:** DotNetty (a .NET port of Netty) and its Protobuf codec adapter are embedded as core transport libraries. Netty/DotNetty is primarily designed for high-performance network sockets (TCP/UDP).
* **Assumptions / Non-Facts:** Treating "DotNetty handles local Wi-Fi TCP sockets" as an established fact is premature. DotNetty can also be used over custom channels or named pipes; active TCP socket listening/connection must be verified via network/port inspection (`netstat` / packet capture).

---

### 8. SignalR Usage
* **Original Claim:** ASP.NET Core SignalR Client is used for real-time WebSockets / cloud push notifications (`Microsoft.AspNetCore.SignalR.Client.dll`).
* **Evidence File & Location:**
  * Filesystem: `Microsoft.AspNetCore.SignalR.Client.dll`, `Microsoft.AspNetCore.SignalR.Client.Core.dll`, `Microsoft.AspNetCore.SignalR.Common.dll`, `Microsoft.AspNetCore.SignalR.Protocols.Json.dll`.
  * `PhoneExperienceHost.deps.json`: Declares `"Microsoft.AspNetCore.SignalR.Client/10.0.12"`.
* **Evidence Type:** Dependency graph and assembly presence.
* **Verification Status:** **Partially Confirmed**
* **What the Evidence Proves:** The SignalR Client library and JSON hub protocol are compiled into the application's dependency tree.
* **Assumptions / Non-Facts:** Claiming it is actively used for "cloud push notifications" or "cloud relay between phone and PC" is an inference. SignalR could be used for cloud synchronization, Microsoft account signaling, or backend service notifications, but the exact remote endpoint cannot be confirmed without traffic inspection.

---

### 9. Protobuf and MessagePack Usage
* **Original Claim:** Protocol Buffers (`Google.Protobuf.dll`) and MessagePack (`MessagePack.dll`) are used for protocol serialization.
* **Evidence File & Location:**
  * Filesystem: `Google.Protobuf.dll` (v3.29.3, 892,928 bytes) and `MessagePack.dll` (v3.1.3, 217,088 bytes).
  * `PhoneExperienceHost.deps.json`: References `Google.Protobuf/3.29.3` and `MessagePack/3.1.3`.
  * Multiple internal assemblies declare `Google.Protobuf` as a direct dependency in `deps.json`:
    * `YourPhone.Connectivity.Protocol`
    * `YourPhone.Devices.Protocol`
    * `YourPhone.Fre.Protocol`
    * `YourPhone.ScreenMirroring.Managed.Protocol`
    * `YourPhone.YPP.PlatformSdk.Protocol`
    * `YourPhone.YPP.SideChannel.Protocol`
    * `DotNetty.Codecs.Protobuf`
* **Evidence Type:** Dependency manifest and multi-project dependency references.
* **Verification Status:** **Confirmed**
* **What the Evidence Proves:** Protobuf is heavily utilized as the serialization mechanism across multiple domain protocols. MessagePack is present as a secondary serialization format.
* **Assumptions / Non-Facts:** We cannot claim which specific feature uses MessagePack vs. Protobuf without examining method signatures or data models.

---

### 10. LibNanoAPI.dll and Its Native Implementation
* **Original Claim:** `LibNanoAPI.dll` (7.8 MB native C++ DLL) is a proprietary engine handling low-level media streaming and screen mirroring decoding.
* **Evidence File & Location:**
  * Filesystem: `LibNanoAPI.dll` (7,851,008 bytes, FileVersion `1.26091.6.0`, Company: `Microsoft Corporation`).
  * WinMD projection: `LibNanoAPI.winmd` (16,896 bytes) and `LibNanoAPI.Projection.dll` (200,704 bytes).
  * `PhoneExperienceHost.deps.json`: Maps package `"Microsoft.Internal.YourPhone.LibNanoApi.Managed/1.26091.6"` containing `"runtimes/win-x64/native/LibNanoAPI.dll"` and `"lib/net10.0-windows10.0.26100/LibNanoAPI.Projection.dll"`.
* **Evidence Type:** Binary metadata, WinMD type definitions, and NuGet package mapping.
* **Verification Status:** **Partially Confirmed**
* **What the Evidence Proves:** `LibNanoAPI.dll` is indeed a large native (unmanaged) 64-bit binary authored by Microsoft with WinRT metadata projection (`.winmd`), consumed by managed code via `LibNanoAPI.Projection.dll`.
* **Assumptions / Non-Facts:** Characterizing it specifically as the "media streaming and screen mirroring decoding pipeline" is plausible based on sizing and naming, but remains an unverified hypothesis. Its exported functions and WinMD types have not yet been enumerated.

---

### 11. Separation of Application Modules and Protocols
* **Original Claim:** The application follows a strict modular separation: Contracts (`YourPhone.Contracts.*`), Protocols (`*.Protocol.dll`), UI/Feature logic (`*.Managed.dll`), and Background tasks.
* **Evidence File & Location:**
  * Filesystem assembly listing and `PhoneExperienceHost.deps.json` structure:
    * **Contracts:** `YourPhone.Contracts.Connectivity.dll`, `YourPhone.Contracts.Messaging.dll`, `YourPhone.Contracts.Calling.dll`, `YourPhone.Contracts.ScreenMirroring.dll`, etc.
    * **Protocols:** `YourPhone.Connectivity.Protocol.dll`, `YourPhone.Devices.Protocol.dll`, `YourPhone.Fre.Protocol.dll`, `YourPhone.ScreenMirroring.Managed.Protocol.dll`, `YourPhone.YPP.PlatformSdk.Protocol.dll`, `YourPhone.YPP.SideChannel.Protocol.dll`.
    * **Implementations:** `YourPhone.Connectivity.Managed.dll`, `YourPhone.Messaging.Managed.dll`, `YourPhone.Notifications.Managed.dll`, etc.
    * **Background Tasks:** `YourPhone.Background.Tasks.dll`, `YourPhone.Background.Tasks.winmd`.
* **Evidence Type:** File inventory, namespace naming scheme, and assembly dependency hierarchy.
* **Verification Status:** **Confirmed**
* **What the Evidence Proves:** Microsoft engineered Phone Link using a formal Domain-Driven / Interface-Contract architecture. Data contracts and wire protocol models are decoupled into dedicated standalone assemblies separate from presentation and business logic.
* **Assumptions / Non-Facts:** None.

---

## Summary of Validation Findings

| # | Claim | Status | Evidence Quality |
|---|---|---|---|
| 1 | .NET 10 & CoreCLR 10.0.14 | **Confirmed** | Exact match in `.runtimeconfig.json` & binaries |
| 2 | WinUI 3 & Windows App SDK 2.4 | **Confirmed** | Exact match in `AppxManifest.xml` & `.deps.json` |
| 3 | Absence of WebView2 | **Confirmed** | Negative match across all files & `.deps.json` |
| 4 | PhoneExperienceHost.exe Entry Point | **Confirmed** | `AppxManifest.xml` `<Application>` entry |
| 5 | YourPhoneAppProxyHost.exe Purpose | **Partially Confirmed** | Confirmed as host runtime; screen decoding role is unverified |
| 6 | SQLite Persistence | **Confirmed** | `e_sqlite3.dll` & `Microsoft.Data.Sqlite.Core` present |
| 7 | DotNetty & TCP Transport | **Partially Confirmed** | DotNetty present; direct TCP runtime usage unverified |
| 8 | SignalR Usage | **Partially Confirmed** | SignalR client present; cloud relay usage unverified |
| 9 | Protobuf & MessagePack Usage | **Confirmed** | Assemblies present and referenced across 6 Protocol DLLs |
| 10 | LibNanoAPI.dll Native Engine | **Partially Confirmed** | Native PE + WinMD confirmed; exact streaming function unverified |
| 11 | Modular Module/Protocol Separation | **Confirmed** | Distinct assembly layers confirmed in `.deps.json` |

---

## Methodological Safeguards for Next Phase
1. **Differentiate Capability from Active Channel:** A dependency indicates the capability to use a technology; runtime validation (Wireshark, ETW, or socket inspection) is necessary before asserting which protocol channel is active during specific user actions.
2. **Type-Level Verification Before Semantic Conclusions:** Inspecting `.winmd` type projections or exported symbols (without modifying or extracting code) is needed before assigning definitive roles to native binaries like `LibNanoAPI.dll`.

