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
https://github.com/Blix-Studios/VRLincensingManager.git#v1.0.3
```

### Option B — Edit manifest.json

Add to your project's `Packages/manifest.json`:

```json
{
    "dependencies": {
        "com.blixstudios.vr-licensing": "https://github.com/Blix-Studios/VRLincensingManager.git#v1.0.3"
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

- **Unity** 2021.3+ (tested on 2022.3.48f1 and 6000.3.10f1)
- **Meta XR SDK** (optional, for Oculus User ID)
- **Supabase** project with `user_licenses` table
- **IL2CPP** scripting backend (standard for Quest builds)

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
