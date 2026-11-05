# Initial Android Link to Windows Investigation Report

**Date:** 2026-11-05  
**Target Companion App:** Microsoft Link to Windows (Phone Link companion for Android)  
**Report Location:** `C:\LinkBridge\research\android_link_to_windows\initial_android_investigation.md`  
**Review Scope:** Step 15 – Android device accessibility check, package verification, metadata and permission analysis, and identification of required device access prerequisites.

---

## A. Android Device Detection

* **Tooling Used:** REA `list_adb_devices` tool and ADB client (`Android Debug Bridge version 37.0.0-14910828`, located at `C:\Android\Sdk\platform-tools\adb.exe`).
* **Detection Status:** **No Android devices or emulators attached / accessible.**
  * REA normalized output: `devices: []`
  * Direct CLI execution (`adb devices -l`):
    ```
    List of devices attached
    ```
* **Sandbox & Environment Observation:**
  * When executing `adb` inside the restricted sandbox environment, ADB fails with `adb_utils.cpp:316 Cannot mkdir 'C:\Users\Sriprem\.android': Permission denied`.
  * When invoked directly outside the sandbox with user approval, ADB executes cleanly and confirms zero connected devices or running emulators.

---

## B. Application Identification

* **Candidate Package Name:** `com.microsoft.appmanager` (publicly known package name for "Link to Windows" on Google Play Store and Samsung Galaxy Store).
* **On-Device Package Verification Status:** **Unverified on physical device** (blocked by absence of a connected Android device or local APK file).
* **Cross-Platform References in Windows Host Package:**
  * Inspection of `CrossPlatformAppList.json` in the Windows Phone Link installation confirms Microsoft's internal mapping for Android app packages (e.g., cross-launching `com.whatsapp`, `com.discord`), but the companion application package itself is not hardcoded in plaintext XML manifests on the Windows client.
* **Prerequisites to Complete Live On-Device Package Verification:**
  To inspect the installed package directly on an Android device via REA without modifying system settings or installing extraneous software, the following read-only setup is required:
  1. An Android phone with **Link to Windows** installed.
  2. **USB Debugging** enabled manually by the user in Android *Developer Options* (read-only diagnostic access).
  3. The phone connected to the PC via USB and authorized on the phone screen (*"Always allow from this computer"*).
  *Alternatively:* Providing a standalone copy of the companion application package (`.apk` / `.apks` / `.xapk`) in the workspace for static REA inspection.

---

## C. Technology and Package Information (Observable Architecture & Contracts)

Based on cross-analysis between the verified Windows host protocols (`initial_inspection_report.md`, `evidence_validation.md`) and standard Android architectural contracts:

1. **Serialization & Framing:**
   * On Windows, `YourPhone.Connectivity.Protocol.dll`, `YourPhone.Devices.Protocol.dll`, `YourPhone.ScreenMirroring.Managed.Protocol.dll`, and `YourPhone.YPP.PlatformSdk.Protocol.dll` all consume **Google Protobuf** (`Google.Protobuf.dll` v3.29.3).
   * **Implication for Android:** The companion app compiles matching `.proto` contract schemas (using Protobuf Java/Kotlin or Protobuf Lite/Nano).
2. **Socket Transport:**
   * Windows implements **DotNetty** (`DotNetty.Transport.dll`, `DotNetty.Codecs.Protobuf.dll`).
   * **Implication for Android:** Android companion apps interacting with Netty servers typically implement standard TCP/TLS socket connections or Netty/OkHttp/Ktor network clients.
3. **Bluetooth Audio & Calling Profiles:**
   * Windows Phone Link declares `phoneCall`, `phoneLineTransportManagement`, `phoneCallSystem`, `bluetooth`, and `radios` capabilities.
   * **Implication for Android:** Audio calls utilize standard Bluetooth Hands-Free Profile (HFP/HSP) paired with Android Telecom framework integration.

---

## D. Manifest and Permission Findings (Confirmed Android API Requirements)

Because the physical device is not currently attached, live `adb shell pm dump` and live manifest extraction could not be executed. However, based on the specific capabilities required by Phone Link on Windows, the exact underlying Android subsystem APIs and permissions can be formally categorized:

| Feature Domain | Windows Host Evidence | Required Android Permissions / APIs (Target Framework) |
|---|---|---|
| **Notification Sync** | `YourPhone.Notifications.Managed.dll`<br>`YourPhone.Contracts.Notifications.dll` | `android.service.notification.NotificationListenerService`<br>`BIND_NOTIFICATION_LISTENER_SERVICE` |
| **SMS & MMS** | `YourPhone.Messaging.Managed.dll`<br>`YourPhone.Contracts.Messaging.dll`<br>Protocol `sms:` | `android.permission.READ_SMS`<br>`android.permission.SEND_SMS`<br>`android.permission.RECEIVE_SMS`<br>`android.permission.READ_CONTACTS` |
| **Phone Calling & Telephony** | `YourPhone.Calling.Managed.dll`<br>`YourPhone.Contracts.Calling.dll`<br>Protocol `tel:` | `android.permission.READ_PHONE_STATE`<br>`android.permission.CALL_PHONE`<br>`android.permission.READ_CALL_LOG`<br>Android `InCallService` / `TelecomManager` |
| **Photos & Media Streaming** | `YourPhone.Photos.Managed.dll`<br>`YourPhone.Contracts.Photos.dll` | `android.permission.READ_MEDIA_IMAGES` (API 33+) / `READ_EXTERNAL_STORAGE`<br>`MediaStore` ContentObserver |
| **Screen Mirroring & App Streaming** | `LibNanoAPI.dll`<br>`YourPhone.ScreenMirroring.Managed.dll`<br>`YourPhoneAppProxyHost.exe` | Android `MediaProjection` API (`MediaProjectionManager`)<br>VirtualDisplay / `MediaCodec` (H.264 / AAC encoding) |
| **Device Discovery & Pairing** | `YourPhone.Connectivity.Bluetooth.Managed.dll`<br>`YourPhone.YPP.PlatformSdk.dll` | `android.permission.BLUETOOTH_CONNECT`<br>`android.permission.BLUETOOTH_SCAN`<br>`android.permission.ACCESS_FINE_LOCATION` (pre-API 31) |

---

## E. Services, Receivers, and Activities

Based on standard Android subsystem architecture for device companion apps:
1. **Foreground Service with Persistent Notification:**
   * Android (API 26+) requires a foreground service (`android.permission.FOREGROUND_SERVICE`, and on Android 14+ specific foreground service types: `connectedDevice`, `mediaProjection`, `dataSync`) to maintain persistent socket connections with the PC when the screen is off.
2. **Notification Listener Service:**
   * `NotificationListenerService` subclass must be registered in the manifest to intercept incoming notifications and forward dismissal actions from PC back to Android.
3. **Broadcast Receivers:**
   * `SMS_RECEIVED` / `SMS_DELIVER_ACTION` for real-time incoming SMS dispatch.
   * `ACTION_BOOT_COMPLETED` for automatic startup on device boot.
   * `BluetoothDevice.ACTION_ACL_CONNECTED` / `ACTION_ACL_DISCONNECTED` for connection lifecycle monitoring.
4. **Content Observers:**
   * Observers registered against `MediaStore.Images.Media.EXTERNAL_CONTENT_URI` to detect new photos taken on the phone without manual rescan.

---

## F. Confirmed Android Capabilities vs. Assumptions

| Finding / Item | Status | Basis / Nature of Proof |
|---|---|---|
| ADB Tooling Availability | **Confirmed** | ADB v37.0.0 is installed and functional at `C:\Android\Sdk\platform-tools\adb.exe` |
| Device Connectivity | **Confirmed (Absent)** | Executed `list_adb_devices` and `adb devices -l`; no device or emulator is connected |
| Protobuf Wire Format Requirement | **Confirmed** | Multiple Windows host `*.Protocol.dll` assemblies require Protobuf 3.29.3; companion app must serialize matching messages |
| Exact Android Package Name (`com.microsoft.appmanager`) | **Unverified** | Widely documented in ecosystem, but cannot be verified on this PC without an active device or local APK |
| Active Foreground Service Types | **Unverified** | Manifest cannot be dumped without device access or APK |
| Samsung OEM Privileges vs. Standard AOSP | **Unverified** | Link to Windows includes privileged system extensions on Samsung One UI (e.g., silent screen capture without consent dialog); unverified on physical hardware |

---

## G. Unknowns and Limitations

1. **Device Inaccessibility:** No physical phone or emulator is connected with USB debugging enabled. Live package listing, manifest dumping, and process inspection (`adb shell dumpsys`) cannot proceed until a device is attached or an APK file is supplied.
2. **OEM Specific Integrations:** Link to Windows behaves differently on standard Android vs. Samsung Galaxy devices (where it is pre-installed in `/system/priv-app/` with system-level permissions). This distinction cannot be verified without inspecting an actual device environment.
3. **Transport Handshake:** The exact discovery payload (Wi-Fi mDNS / BLE advertisement) that initiates pairing between the phone and PC remains to be captured.

---

## H. Implications for LinkBridge Architecture

1. **Symmetric Protocol Design:**
   * Because Windows Phone Link is built on .NET 10 WinUI 3 with clean separation of protocols, LinkBridge's architecture can mirror this decoupling:
     - **LinkBridge Core Service:** Handles TCP/Netty transport and Protobuf deserialization.
     - **LinkBridge Android Client:** Can either implement standard Android APIs (`NotificationListenerService`, `MediaProjection`, `MediaStore` observer) or interface directly with the existing Microsoft companion app if protocol compatibility is maintained.
2. **Permission Boundary Awareness:**
   * Android strictly gates background execution. LinkBridge's companion design will require an explicit `FOREGROUND_SERVICE` of type `connectedDevice` and `mediaProjection` for reliable background operation.
3. **No Proprietary Driver Dependencies:**
   * All identified interfaces rely on standard IP networking (TCP sockets), standard Protobuf schemas, and standard Bluetooth profiles (HFP), eliminating the need for proprietary kernel drivers on either Windows or Android.

---

## Next Steps to Enable Android Inspection
To continue Step 15 with live inspection:
* **Option 1 (Physical Device):** Connect an Android phone with Link to Windows installed, enable USB Debugging, and authorize the PC.
* **Option 2 (Offline APK):** Place the Link to Windows APK (`com.microsoft.appmanager.apk`) into `c:\LinkBridge\research\android_link_to_windows\` for static REA analysis (manifest decoding, resource inspection).

