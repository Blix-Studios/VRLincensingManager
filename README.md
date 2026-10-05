# VR Licensing System — Unity UPM Package

[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black?logo=unity)](https://unity.com/)
[![Platform](https://img.shields.io/badge/Platform-Meta%20Quest-blue)](https://www.meta.com/quest/)

Generic licensing and key registration module for VR simulators on Meta Quest. Installed as a UPM package via Git and connects to **Supabase** for license key validation.

## Features

- 🔑 **License key validation** against Supabase REST API
- ⏱️ **Demo Mode** with configurable timer (default: 1 hour)
- 🔒 **Encrypted storage** (AES-256 with device-derived key + HMAC-SHA256 integrity seal)
- 🕐 **Anti-clock tampering** (NTP-based clock rollback detection)
- 📊 **Session time tracking** using `Time.unscaledTime`
- 📡 **Server-side device registry** — survives factory resets; prevents demo abuse
- 🔌 **Auto-initialization** (`RuntimeInitializeOnLoadMethod`) — Plug & Play
- ⚙️ **ScriptableObject configuration** — each simulator has its own config

## Installation

### Option A — Package Manager (recommended)

In Unity, go to:

```
Window > Package Manager > + > Add package from git URL...
```

Paste:
```
https://github.com/Blix-Studios/VRLincensingManager.git#v1.1.0
```

### Option B — Edit manifest.json

Add to your project's `Packages/manifest.json`:

```json
{
    "dependencies": {
        "com.blixstudios.vr-licensing": "https://github.com/Blix-Studios/VRLincensingManager.git#v1.1.0"
    }
}
```

## Quick Setup

### 1. Create the Configuration Asset

In Unity:
```
Assets > Create > VR Licensing > New Configuration
```

This creates a `LicenseConfig` ScriptableObject. Fill in the following fields:

| Field | Description |
|-------|-------------|
| **Supabase Url** | Your Supabase project URL (e.g., `https://xxx.supabase.co`) |
| **Anon Key** | Supabase public anon key |
| **Product Id** | Product/simulator ID from the `products` table |
| **Demo Duration Seconds** | Demo time in seconds (3600 = 1 hour) |
| **Max Offline Hours** | Maximum offline hours before requiring reconnection (72 = 3 days) |
| **App Display Name** | Public-facing simulator name |

### 2. Place the Config in Resources

**Move** the `LicenseConfig` asset to the `Assets/Resources/` folder (create it if it doesn't exist). The file **must** be named `LicenseConfig`.

### 3. (Optional) Create a UI Prefab

Create a prefab named `LicenseGateUI` with the `LicenseManager` component and place it in `Assets/Resources/`. If it doesn't exist, the system will generate all UI panels at runtime via code automatically.

### 4. Done!

Press **Play** and the system will auto-initialize before any scene loads. The Welcome Panel will appear with options to start a free demo, enter a license key, or scan a QR code.

---

## Store builds — Meta Horizon Store subscriptions (v1.12)

Spec: `06-LICENSING-V1.12-SPEC.md` (VRInstructorRef). Everything below is additive; company APKs (`storePlatform = None`) behave exactly as before.

| Piece | What it does |
|---|---|
| `LicenseConfig.storePlatform` | `None` (default) · `MetaHorizon` · `Steam` (phase 2, behaves as None). |
| `LicenseConfig.storeSkus` | `sub_monthly` / `sub_6m` / `sub_annual` — the subscription SKUs of **this** app in the Meta dashboard. |
| `LicenseConfig.enforceEntitlement` | OFF in development (sideloaded APKs fail Meta's app-ownership check); ON only in published store builds. |
| Meta XR Platform SDK | **Optional.** Installing `com.meta.xr.sdk.platform` sets the `VRL_META_PLATFORM` define (asmdef `versionDefines` + an editor sync into Player Settings) and compiles the `VRLicensing.MetaStore` assembly. Without it, `MetaHorizon` logs a warning and behaves as `None`. Set the App ID in *Meta > Platform > Edit Settings*. |
| Boot (MetaHorizon) | Platform init + entitlement check within 10 s → cached key → cached store subscription → (phase B) server verification. Failures are logged; the app continues in demo/key mode unless `enforceEntitlement` is ON. |
| Expired panels | Left column: Monthly / 6 months / Annual buttons with the store's localized prices (`IAP.GetProductsBySKU`); right column: license key. No URL, QR, external price or promo. If the store reports no products, the key column takes the centre and a console line says why. |
| `LicenseState.SubscribedViaStore` | Equivalent to `Licensed` for gating; no client branding. Cached encrypted until `min(expiresAt + 3 days grace, verifiedAt + maxOfflineHours)`. |
| Device code | `Device code: XXXXX-XXXXX` at the foot of every license panel, in 11 languages: first 10 hex chars (uppercase) of SHA-256 over the UTF-8 bytes of `SystemInfo.deviceUniqueIdentifier` — the exact string sent as `p_device_id`. |
| Branding / telemetry | Now through `get_license_branding` / `report_telemetry` RPCs; falls back to the pre-1.12 direct table access while they return 404 (not deployed). |
| Phase B (`StoreVerification.ServerEnabled`) | Wired but OFF until the server is deployed: `verify-store-entitlement` Edge Function (Meta user id + `Users.GetUserProof` nonce), `p_store_user_id` on `get_device_status`, `p_events` on `report_device_session`. Until then a completed checkout **cannot** unlock the app (server-authoritative by design). |

Unknown license statuses (e.g. `revoked`) deserialize without error and are treated as "not valid" with the server's message; an online re-validation that returns any verdict other than a connection error drops the cached license immediately.

## Testing with Sandbox Licenses

Pre-provisioned sandbox license keys are available for external teams to test the SDK without needing access to the Supabase web portal. These are **real license entries** in the production database.

### Available Sandbox Keys

| License Key | Product ID | Product Name |
|---|---|---|
| `SBOX-TEST-CHN1-0001` | 1 | VR Chainsaw Training |
| `SBOX-TEST-TRK2-0002` | 2 | VR Truck Platform Training |
| `SBOX-TEST-FRK3-0003` | 3 | VR Forklift Training |
| `SBOX-TEST-SWP4-0004` | 4 | VR Road Sweeper Training |

All sandbox keys are `annual` licenses valid until **2030-12-31**.

### How to Test

1. Install the UPM package (see [Installation](#installation) above).
2. Create a `LicenseConfig` asset via `Assets > Create > VR Licensing > New Configuration`.
3. Set the **Product Id** field to the number matching the simulator you are building (1–4).
4. Place the config in `Assets/Resources/` named `LicenseConfig`.
5. Press **Play** in Unity — the licensing UI will appear.
6. Click **Enter License Key** and enter the matching sandbox key (e.g., `SBOX-TEST-CHN1-0001` for Product 1).
7. The license will validate against Supabase and activate successfully.

> **Note:** These sandbox keys work identically to real customer keys. When building the final production version, simply replace the sandbox key with the customer's actual purchased key.

---

## Architecture

```
LicenseBootstrapper (auto-init)
    └── LicenseManager (orchestrator)
        ├── SupabaseClient (HTTP REST API)
        ├── SecureLicenseStorage (AES-256 + PlayerPrefs)
        ├── ClockGuard (anti clock-rollback)
        ├── SessionTimeTracker (Time.unscaledTime)
        └── DemoModeManager (demo timer)
```

## LicenseManager Events

Subscribe to these events from your code to react to state changes:

```csharp
var manager = FindFirstObjectByType<VRLicensing.LicenseManager>();

manager.OnStateChanged += (state) => {
    // LicenseState: Unlicensed, Demo, Licensed, Expired, ClockTampered
    Debug.Log($"State: {state}");
};

manager.OnLicenseValidated += (license) => {
    Debug.Log($"Active license: {license.license_type}, expires: {license.expires_at}");
};

manager.OnDemoExpired += () => {
    // Show lockout screen
};

manager.OnLicenseExpired += () => {
    // Return to demo mode or lock
};
```

## Submitting a License Key from Code

```csharp
manager.SubmitLicenseKey("ABCD-1234-EFGH-5678", (success, error) => {
    if (success)
        Debug.Log("License activated!");
    else
        Debug.LogError($"Error: {error}");
});
```

## File Structure

```
Runtime/
├── Scripts/
│   ├── LicenseManager.cs          # Main orchestrator
│   ├── LicenseBootstrapper.cs     # Auto-init BeforeSceneLoad
│   ├── LicenseConfig.cs           # ScriptableObject configuration
│   ├── LicenseData.cs             # Data model (maps to user_licenses)
│   ├── SupabaseClient.cs          # HTTP client for Supabase REST API
│   ├── SecureLicenseStorage.cs    # AES-256 encrypted storage
│   ├── ClockGuard.cs              # Clock rollback detection
│   ├── SessionTimeTracker.cs      # Real-time counter
│   └── DemoModeManager.cs         # Demo timer
├── Prefabs/                       # UI Prefabs (optional)
└── VRLicensing.Runtime.asmdef
Editor/
└── VRLicensing.Editor.asmdef
```

## Requirements

- **Unity** 6000.0+ (Unity 6)
- **Supabase** project with `user_licenses` table
- **IL2CPP** scripting backend (standard for Quest builds)

### Automatically Installed Dependencies

The following packages are listed as UPM dependencies and will be installed automatically when you add this package:

| Package | Min Version | Purpose |
|---------|------------|--------|
| `com.unity.xr.interaction.toolkit` | 3.0.0 | VR interaction, tracked device raycasting, LazyFollow |
| `com.unity.xr.arfoundation` | 6.0.0 | Passthrough (via ARCameraManager + OpenXR Meta) |

### Required Sample (Manual Import)

After installing, you must import the **Spatial Keyboard** sample from XRI:

1. Open **Window > Package Manager**
2. Select **XR Interaction Toolkit**
3. Go to the **Samples** tab
4. Click **Import** next to **Spatial Keyboard**

> **Note:** UPM packages cannot auto-import samples. If the sample is not imported, the license UI will still function but the VR keyboard will not appear when selecting input fields.

### Scene Setup

For the VR keyboard to work, your scene must include the **XRI Global Keyboard Manager** prefab:

1. Navigate to `Assets/Samples/XR Interaction Toolkit/[version]/Spatial Keyboard/Prefabs/`
2. Drag the **XRI Global Keyboard Manager** prefab into your scene
3. This is a singleton that persists across scenes

### Passthrough Setup (Meta Quest)

For the "Scan QR" passthrough feature:

1. Install `com.unity.xr.meta-openxr` (Unity OpenXR: Meta)
2. In **Project Settings > XR Plug-in Management > OpenXR**, enable the **Meta Quest** feature group
3. Enable **Meta Quest: Camera (Passthrough)** feature
4. Ensure your Graphics API is set to **Vulkan**

## Required Supabase Schema

Your `user_licenses` table must include at least:

```sql
CREATE TABLE user_licenses (
    id uuid DEFAULT gen_random_uuid() PRIMARY KEY,
    license_key text NOT NULL,
    license_type text NOT NULL,     -- 'weekly', 'monthly', 'annual'
    status text DEFAULT 'active',    -- 'active', 'expired', 'cancelled', 'suspended'
    product_id integer NOT NULL,
    user_id uuid NOT NULL,
    order_id uuid,
    starts_at timestamptz DEFAULT now(),
    expires_at timestamptz NOT NULL,
    created_at timestamptz DEFAULT now(),
    updated_at timestamptz DEFAULT now()
);
```

## License

Property of Blix Studios. For authorized projects only.
