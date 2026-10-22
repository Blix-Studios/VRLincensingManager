# Changelog

All notable changes to `com.blixstudios.vr-licensing`. Pre-release tags (`vX.Y.Z-pre.N`) are
test builds for VirtualChainsaw; the final tag is cut only after QA on the headset.

## 1.12.0-pre.7 — 2026-10-22

- **Meta store provider survives IL2CPP stripping.** `VRLicensing.MetaStore` is referenced by
  nothing (the runtime reaches it only through `StorePlatformRegistry`), so UnityLinker dropped
  the assembly from player builds and `MetaStorePlatform.Register()` never ran: the player
  logged "Meta XR Platform SDK is not installed", skipped the entitlement check and showed the
  organisation-key column instead of the subscription plans (VirtualChainsaw build 23). The
  assembly now carries `[assembly: AlwaysLinkAssembly]`. Check on device: logcat shows
  `Store provider registered: MetaHorizon (Meta XR Platform SDK)` followed by the platform
  init / entitlement lines.
- **Expired panel no longer forces passthrough.** Panels with a key field counted as "typing
  mode" and enabled passthrough even with `usePassthroughBackground` off. `Enable()` binds to
  the camera of that instant, culls it to a private layer and moves the canvas there; when the
  camera changed afterwards (Boot → Log In race at startup, in-session expiry in the tutorial)
  the panel became invisible or the scene vanished. Only "Scan QR" (or the opt-in background)
  enables passthrough now.
- Passthrough re-binds to the new main camera when its camera dies or `Camera.main` changes
  (clean `Disable()` restores layers, culling and gravity first), and `passthroughActive`
  mirrors what `Enable()` actually did, so "Scan QR" pressed in Boot still works in Log In.
- The 10 s platform-init / entitlement deadline no longer counts time the app spends paused
  behind a system dialog (permissions, "Restore app", Guardian): Platform SDK callbacks cannot
  arrive while paused, and a dialog longer than 10 s at boot ended in "entitlement check failed →
  quit" with `enforceEntitlement` on.
- No contract change (RPCs, Edge Function, LicenseConfig fields unchanged).

## 1.12.0-pre.6 — 2026-10-21

- Demo expiry inside a session is now guaranteed: a watchdog in the license UI (its own
  GameObject) ends the demo through the normal `OnDemoExpired` path when the countdown has sat
  at zero for 2 s while the manager is still in `Demo` (`LicenseManager.ForceDemoExpiry` →
  `DemoModeManager.ForceExpire`). Field report: a Quest 3 counted 16 min of a 15-min demo
  without the expired panel; the in-session path works in the editor, so the device-side
  cause is still being traced.
- Development builds log demo diagnostics (`[VR Licensing] demo: …`): start, every 30 s,
  pause/resume/focus, disable, forced expiry.
- No contract change (RPCs, Edge Function, LicenseConfig fields unchanged).

## 1.12.0-pre.5 — 2026-10-19

- "Best value" badge on the ANNUAL plan is now **data-driven**: shown only when the annual price
  per month is strictly lower than the per-month price of every other configured term, using the
  prices Meta reports (`Price.AmountInHundredths`, or a locale-agnostic parse of the formatted
  price). Any unreadable or missing price hides the badge. ANNUAL still appears first.
  Catalogue prices decided 2026-10-19 (MONTHLY 39.99 / SEMIANNUAL 199.99 / ANNUAL 399.99) do
  not earn the badge (annual = two semesters).
- `StoreTermPrice.amountInHundredths` + `TryGetAmount`, `StorePriceParser`,
  `StoreSubscription.TermMonths` / `IsAnnualBestValue`.
- Editor store stub uses the catalogue prices; menu toggle to test an annual at 349.99.
- No contract change (RPCs, Edge Function, LicenseConfig fields unchanged).

## 1.12.0-pre.4 — 2026-10-13

- `VRLicensing.MetaStore` is gated on `VRL_META_PLATFORM_PKG` (asmdef `versionDefines` only), so
  removing the Meta XR Platform SDK no longer breaks the first recompile.

## 1.12.0-pre.3 — 2026-10-13

- Spec §1.0 SKU model: one subscription SKU per tier (`storeSubscriptionSku`, default
  `sub_access`) with terms as billing plans (`storeTerms`, default MONTHLY / SEMIANNUAL /
  ANNUAL). Checkout string `{sku}:SUBSCRIPTION__{TERM}`; prices per `BillingPlan.PaidOffer`.
- Subscription checked on every launch before the demo (store-page subscribers never see the
  countdown or the paywall); server verification always on.
- Plans column: ANNUAL first, localized "Best value" badge and benefits line (11 languages).
- Checkout analytics events carry name, SKU, term and time only (no Meta user id).
- Editor store stub (fake store + fake `verify-store-entitlement`) for Play-mode testing.
- Release build with `storePlatform = MetaHorizon` and `enforceEntitlement` OFF fails.

## 1.12.0-pre.2 — 2026-10-05

- Fix: TMP `HasCharacters` signature in Unity 6 (`out uint[]`).

## 1.12.0-pre.1 — 2026-10-05

- Meta Horizon Store support: `storePlatform`, optional Meta XR Platform SDK provider,
  entitlement check within 10 s, `LicenseState.SubscribedViaStore` with encrypted cache.
- Privacy device code (`XXXXX-XXXXX`, SHA-256 of the device id) at the foot of every panel.
- Branding and telemetry through `get_license_branding` / `report_telemetry` with fallback.
- Unknown license statuses (e.g. `revoked`) are invalid without deserialization errors.
