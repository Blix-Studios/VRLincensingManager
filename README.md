# VR Licensing System — Unity UPM Package

[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black?logo=unity)](https://unity.com/)
[![Platform](https://img.shields.io/badge/Platform-Meta%20Quest-blue)](https://www.meta.com/quest/)

Generic licensing and key registration module for VR simulators on Meta Quest. Installed as a UPM package via Git and connects to **Supabase** for license key validation.

## Features

- 🔑 **License key validation** against Supabase REST API
- ⏱️ **Demo Mode** with a server-decided time per headset (`demo_limit_seconds`, set from `/admin`, default 15 min); the config value is only the offline fallback
- 🔒 **Encrypted storage** (AES-256 with device-derived key + HMAC-SHA256 integrity seal)
- 🕐 **Anti-clock tampering** (NTP-based clock rollback detection)
- 📊 **Session time tracking** using `Time.unscaledTime`
- ⏲️ **Demo clock that only counts play time** — it holds while the app is paused or unfocused, the headset is off or frames stop, and it stops at the limit (v1.12.0-pre.16)
- 📡 **Server-side device registry** — survives factory resets; prevents demo abuse
- 🔌 **Auto-initialization** (`RuntimeInitializeOnLoadMethod`) — Plug & Play
- ⚙️ **ScriptableObject configuration** — each simulator has its own config
- 🌐 **Localized license UI** — every panel, button and message in 16 languages (en, es, fr, de, it, pt, ru, zh, hi, fi, sv, nl, ar, ja, ko, ur; Arabic and Urdu are shaped and laid out right to left by the package), following the host app's selector or the headset language

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
| **Demo Duration Seconds** | Offline fallback only: demo seconds used until the server reports the headset's `demo_limit_seconds` (cached afterwards) |
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
| `LicenseConfig.storeSubscriptionSku` / `storeTerms` | Meta models a subscription as **one SKU per tier** (`sub_access`) with **terms** as billing plans. Terms use Meta's `OfferTerm` tokens: `MONTHLY`, `SEMIANNUAL`, `ANNUAL` (also WEEKLY, BIWEEKLY, QUARTERLY, BIANNUAL). Checkout string: `{sku}:SUBSCRIPTION__{TERM}`. Prices come from `IAP.GetProductsBySKU` → `BillingPlan.PaidOffer.Price`; none are in code. The panel shows ANNUAL first and a localized benefits line; the localized "Best value" badge appears on ANNUAL only when its price per month is strictly below every other configured term, computed from the store's prices (hidden if any price is unreadable). |
| Free apps (no SKU) | Leave `storeSubscriptionSku` **empty** in a store build of a free app. The build then has nothing to sell: no "Subscribe" button on the welcome panel, no plans panel or plans column on the expired panels, `ShowPlans()` and `SubscribeViaStore()` do nothing, the boot subscription lookup is skipped and `LicenseManager.StoreOffersEnabled` is false (gate your own pause-menu button on it). The entitlement check still runs. A store build never shows the web purchase CTA, whatever `showPurchaseCta` says (v1.12.0-pre.17). |
| Boot subscription check | With `MetaHorizon` the signed-in account's subscription is checked on **every** launch before the demo can start (a user may subscribe from the store page); a subscriber never sees the countdown or the paywall. Unlock still requires the server's verdict. |
| Editor store stub | *Tools > Blix Studios > Licensing > Editor store stub (toggle)*: fake store + fake server verification to exercise the whole flow in Play mode (plans → checkout → verify → `SubscribedViaStore` → cache). Never compiled into players. |
| Release check | A non-development build with `storePlatform = MetaHorizon` and `enforceEntitlement` OFF **fails** with a clear message. |
| `LicenseConfig.enforceEntitlement` | OFF in development (sideloaded APKs fail Meta's app-ownership check); ON only in published store builds. |
| Meta XR Platform SDK | **Optional.** Installing `com.meta.xr.sdk.platform` compiles the `VRLicensing.MetaStore` assembly (gated by the package-presence symbol `VRL_META_PLATFORM_PKG` from its asmdef `versionDefines`) and an editor sync adds `VRL_META_PLATFORM` to Player Settings for project code. Without it, `MetaHorizon` logs a warning and behaves as `None`. Set the App ID in *Meta > Platform > Edit Settings*. |
| Boot (MetaHorizon) | Platform init + entitlement check within 10 s → cached key → cached store subscription → (phase B) server verification. Failures are logged; the app continues in demo/key mode unless `enforceEntitlement` is ON. |
| Expired panels | Left column: Monthly / 6 months / Annual buttons with the store's localized prices (`IAP.GetProductsBySKU`); right column: license key. No URL, QR, external price or promo. If the store reports no products, the key column takes the centre and a console line says why. |
| `LicenseState.SubscribedViaStore` | Equivalent to `Licensed` for gating; no client branding. Cached encrypted until `min(expiresAt + 3 days grace, verifiedAt + maxOfflineHours)`. |
| Device code | `Device code: XXXXX-XXXXX` at the foot of every license panel, in 16 languages: first 10 hex chars (uppercase) of SHA-256 over the UTF-8 bytes of `SystemInfo.deviceUniqueIdentifier` — the exact string sent as `p_device_id`. |
| Branding / telemetry | Only through the `get_license_branding` / `report_telemetry` RPCs. Since v1.12.0-pre.17 there is no fallback to the tables: the app never reads or writes a table (contract §2). When the branding call fails, the branding cached on the device from the last good answer is used (name and logo file); with no cache, no branding. |
| Intro offers | If a billing plan carries an `INTRO_OFFER`, its button shows the store's localized intro price and, smaller, the duration and the regular price ("for the first 3 months, then US$39.99", 11 languages). Only the duration words are ours; no offer, no change. |
| Store events (`p_events`) | `plans_opened` (once per opening of a panel with plans: demo-expired, license-expired, plans panel), `checkout_opened`, `checkout_completed`, `checkout_cancelled`, `subscription_verified` — name, SKU, term and UTC time only. Persisted in PlayerPrefs until reported; `plans_opened` and `checkout_*` are sent at once, the rest with the next session report. |
| Server verification | Always on: `verify-store-entitlement` Edge Function (Meta user id + `Users.GetUserProof` nonce), `p_store_user_id` on `get_device_status`, `p_events` on `report_device_session` (events carry name, SKU, term and time — never the Meta user id). While the function is not deployed the call fails and the app stays locked with the "internet needed" notice; use the editor stub to test. |

### Plans before / during the demo (v1.12.0-pre.8)

Store builds show a localized **Subscribe** button on the welcome panel. To offer the plans
while the demo is running, call `LicenseManager.ShowPlans()` from your own UI — the recommended
place is the game's pause menu:

```csharp
var manager = FindFirstObjectByType<VRLicensing.LicenseManager>();
// show the button only while it makes sense:
bool show = manager != null && manager.StoreOffersEnabled && manager.CurrentState == VRLicensing.LicenseState.Demo;
// on click:
manager.ShowPlans();   // pauses the demo stopwatch; "Back" resumes it, a purchase unlocks
```

Label it with `VRLicensing.LicenseStrings.Subscribe()` (16 languages) and show it only while
`manager.StoreOffersEnabled` is true. `ShowPlans()` is a no-op in any state other than
`Unlicensed` / `Demo`, without a store platform and without a subscription SKU (free apps), so
the wiring is safe in company APKs and free store apps too. VirtualChainsaw does this with `PauseMenuSubscribeButton`, a small
component on the pause-menu panel that clones one of the existing buttons at runtime.

### Feedback and demo exit survey (v1.12.0-pre.11)

Every simulator (store build or not) can let the user send feedback from its own UI:

```csharp
var manager = FindFirstObjectByType<VRLicensing.LicenseManager>();
// label: VRLicensing.LicenseStrings.Feedback()  → "Send feedback" in the UI language
manager.ShowFeedback();   // rating 1-5 (optional), category, optional text, thank-you; "Back" closes
```

The panel pauses the demo stopwatch while open and sends `submit_app_feedback` (contract §9):
device id, product, rating, category (`bug` / `performance` / `content` / `suggestion`), text
(max 500), `p_app_version` (`LicenseManager.AppVersion` if the host sets it, otherwise
`"<Application.version> (<versionCode>)"` built by the package) and `p_locale` (the language the license UI shows). Never a user id or e-mail.
VirtualChainsaw wires it with `PauseMenuFeedbackButton` next to `PauseMenuSubscribeButton`.

The **demo-expired panel** first asks "What stopped you?" (`price`, `for_my_company`,
`not_what_i_expected`, `other`, optional text, skippable) once per headset and product; the
answer time is stored and the question only returns after an admin demo reset
(`demo_reset_at` newer than the answer). "It's for my company" leads to the license-key column
with a line about company quotes — no URL, QR or price in store builds.

To keep the license UI in the language your app shows, set `LicenseStrings.LanguageOverride`
to the selected locale code whenever it changes (VirtualChainsaw: `LicenseLanguageSync`). Since
v1.12.0-pre.14 the whole UI is localized and an open panel re-renders on the change
(`LicenseStrings.LanguageChanged`); `LicenseStrings.Get(LicenseStrings.Key.X)` gives any UI string.

**The gate is modal, so a host's language menu cannot be pressed while a license panel is open.**
While any panel is up the UI builder disables every `BaseRaycaster` outside the license UI and the
keyboard, in the whole scene, and rescans about once a second. That is deliberate: a tester once
started an app through its own menu without touching demo or license. The panels therefore show the
headset's language (or a language the host set beforehand through `LanguageOverride`) until the
student closes them, and the host's menu works again afterwards. **Panels must be dismissed before
the app's own menus can be used**: start the demo, redeem a key, or close the plans / feedback
panel. Do not put a control the user needs at that moment (a language selector, a quit button)
behind the gate; set the language through `LanguageOverride` instead. A locale the package has no strings
for (see `SupportedLanguages`) falls back to the headset's language in the same way.

Unknown license statuses (e.g. `revoked`) deserialize without error and are treated as "not valid" with the server's message; an online re-validation that returns any verdict other than a connection error drops the cached license immediately.

### Client branding on decals, banners and UI (v1.12.0-pre.17)

`BrandingDecalApplier`, `BrandingMeshApplier` and `BrandingImageApplier` put the client's logo on a URP
Decal Projector, a mesh and a UI Image.

- **Texture property**: each applier looks on the material for the property it really has, starting with the
  one configured and then `_Base_Map`, `Base_Map` (URP 17's decal Shader Graph), `_BaseMap`, `_MainTex`.
  A decal applier left at `_Base_Map` on a URP 17 material used to apply nothing.
- **Fit, no crop**: the logo keeps its aspect ratio and is shown whole. Decals use the projector's
  width / height, meshes the shape of the texture the material had (or `aspectOverride`), UI Images
  `preserveAspect`. `fitLogo` / `preserveAspect` OFF restores the old stretch.
- **Transparency**: on decals and alpha materials the rest of the surface stays transparent; on an opaque
  material (a banner) the logo is composited over `backgroundColor` (white by default), so a transparent PNG
  no longer shows black.
- Appliers of the same shape share one fitted texture; a new logo releases the old ones.
- **The material assets are never modified** (v1.12.0-pre.19). The decal applier puts a runtime copy of
  the material on its projector and the mesh applier uses a MaterialPropertyBlock (or a copy when
  `useMaterialPropertyBlock` is off); both give the original back when they are destroyed. Playing in
  the Editor with a client logo therefore leaves the `.mat` files alone. Only projectors that carry a
  `BrandingDecalApplier` show the logo: a decal that merely shares the material no longer does.
  `ApplyLogo(Texture2D)` / `RestoreOriginal()` are public for hosts and tests.

### Languages and right-to-left text (v1.12.0-pre.17)

The license UI has every string in 16 languages. Arabic and Urdu are shaped by the package itself
(`RtlText`: contextual letter forms, lam-alef, Latin and digits kept readable, brackets mirrored) and their
labels switch to TextMeshPro's right-to-left mode, so no shaping plugin is needed. What the package cannot
bring is the **font**: the project's TMP default font (or its fallback list) must contain the script,
including the Arabic Presentation Forms blocks (U+FB50-FDFF, U+FE70-FEFF) for Arabic and Urdu.
When no font can draw a text the label shows English instead, and a label is moved to a font that can draw
it (its own, TMP's current default, or the font the UI was built with) rather than left empty - a host that
swaps the default font per language no longer blanks the license panels. A host that runs its own Arabic
fixer over every label in the scene does not break them either: text it has already shaped is recognised and
drawn as it is. Urdu has no `SystemLanguage` value, so it is only selected through `LanguageOverride = "ur"`.

Every label may shrink to fit its rect (v1.12.0-pre.19: auto-size from the design size down to 55 %, never
under 7 pt). The rects are sized for Latin type; a face with a taller line — Noto Sans Arabic needs 2.1 x the
font size per line — did not fit one line in a title rect and TextMeshPro then drew nothing. With such a face
the titles come out smaller (16 pt → about 12 pt) instead of disappearing, and a label that still cannot show
one line is drawn across its rect rather than not at all. Labels laid out right to left have pair kerning off:
TextMeshPro applies it the wrong way round there.

### Device telemetry and demo counting (v1.12.0-pre.16)

Every `report_device_session` carries, besides the demo delta:

| Parameter | Value |
|---|---|
| `p_report_id` | A new uuid per report. The server adds nothing for an id it has already seen. |
| `p_app_version` | `LicenseManager.AppVersion` if the host set it. Otherwise, since v1.12.0-pre.17, the package sends `"<Application.version> (<versionCode>)"` itself, e.g. `"1.0.0 (31)"`, reading the versionCode of the installed APK (`LicenseBuildInfo.DefaultAppVersion`; in the Editor there is no code and it is just the version). |
| `p_upm_version` | `PackageVersion.Value`, generated from `package.json`. |
| `p_license_state` | `demo`, `demo_expired`, `licensed`, `license_expired` (a key or subscription ran out), `store_subscribed` or `blocked`. |
| `p_locale` | The language the license UI is showing (`LicenseStrings.CurrentLanguage`). |

```csharp
// Optional since pre.17 - only when the host wants a different text than the default:
VRLicensing.LicenseManager.AppVersion = "1.0.0 (31)";
```

A server that has not been migrated answers function-not-found to the new parameters; the report
is sent once more without them and the rest of the run skips them. No user id, e-mail or Meta
account is ever sent.

The demo clock counts frames, not wall time: nothing is added while the app is paused or has no
focus, while the headset is not worn, or for a single frame longer than 3 seconds, and the total
stops at the limit. After a report that timed out the client re-reads the server total before
reporting again, so the same time is never sent twice.

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
