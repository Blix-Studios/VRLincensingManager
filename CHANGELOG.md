# Changelog

All notable changes to `com.blixstudios.vr-licensing`. Pre-release tags (`vX.Y.Z-pre.N`) are
test builds for VirtualChainsaw; the final tag is cut only after QA on the headset.

## 1.12.0-pre.10 — 2026-10-25 (tag `v1.12.0-pre.10` = 798fb4e)

Engagement layer, part 1 (spec `08-ENGAGEMENT-CAMPAIGNS.md` §4–§5, contract §9), on top of
the pre.9 telemetry: `plans_opened` on every panel with plans, intro offers, feedback panel,
demo exit survey and the host-side language sync. First consumer: VirtualChainsaw build 27
(not uploaded). Note: `package.json` at the tagged commit still reads `1.12.0-pre.9`; the
version string was bumped in the following commit.

### Purchase telemetry (completes pre.9)
- `plans_opened` is now queued **once per opening of any panel with plans**: the demo-expired
  panel, the license-expired panel (store builds) and the plans panel reached from the welcome
  "Subscribe" button or the game's pause menu. The UI reports through
  `LicenseManager.NotifyPlansShown()`; the once-per-opening flag resets when the modal moves to
  another state. On the demo-expired panel it counts when the columns appear, not while the
  exit survey is up.

### Intro offers
- When a billing plan of the subscription SKU carries an `INTRO_OFFER` (`TrialOffer.TrialType`),
  `StoreTermPrice` exposes it (`introFormattedPrice`, `introTerm`, `introTermCount`,
  `HasIntroOffer`) and the plan button shows **the store's localized intro price** on the first
  line and, smaller, how long it lasts and the regular price afterwards: "Annual · US$299.99" /
  "for the first 1 year, then US$399.99". Prices are the store's strings verbatim; only the
  duration words (week/month/year with plurals, Russian 1/2–4/5+ forms, Chinese counters) and
  the connective are localized by the package (11 languages). No offer → nothing changes.
  `FREE_TRIAL` offers only set `hasTrial`. The "Best value" rule still uses regular prices.
- Editor store stub: *Store stub: intro offer on ANNUAL (toggle)*.

### Feedback and demo exit survey (`submit_app_feedback`, contract §9)
- **Feedback panel**: `LicenseManager.ShowFeedback()` / `CloseFeedback()` open a panel with an
  optional 1–5 rating, a required category (`bug`, `performance`, `content`, `suggestion`),
  optional text (system keyboard, 500 characters) and a thank-you line; during the demo the
  stopwatch pauses, "Back" resumes it. Works in every simulator, store build or not. Label a
  pause-menu button with `LicenseStrings.Feedback()` (VirtualChainsaw: `PauseMenuFeedbackButton`).
- **Demo exit survey** on the demo-expired panel, before the plans/key columns: "What stopped
  you?" with `price`, `for_my_company`, `not_what_i_expected`, `other`, optional text, Send or
  Skip. Asked **once per headset and product**; answered or skipped, the time is stored
  (`vrl_exit_survey_<productId>`) and the question returns only when `get_device_status` /
  `report_device_session` carry a `demo_reset_at` later than that time (admin demo reset).
  `for_my_company` moves on to the key column with a localized line saying companies request a
  quote on the website and receive license keys — **no URL, QR or price** (store rule).
  `plans_opened` is counted when the columns appear, not while the question is up.
- RPC: `submit_app_feedback(p_device_id, p_product_id, p_kind, p_rating, p_category, p_reason,
  p_text, p_app_version, p_locale)`; optional members are omitted; `p_locale` is the language the
  license UI is showing (`LicenseStrings.CurrentLanguage`, short BCP-47); `p_app_version` is
  `Application.version` unless the host sets `LicenseManager.AppVersion` (e.g. "1.0.0 (27)").
  Errors handled: `rate_limited` (feedback: friendly "enough for today" line and close; survey:
  treated as answered), transport failure (feedback: "try again later", inputs re-enabled;
  survey: told briefly, asked only once). `AppFeedback` (plain C#) holds codes, limits, body
  builder, result parser and the re-ask rule, all unit-tested.
- Editor store stub answers feedback locally (`AppFeedback.StubSubmitter`), with toggles
  *feedback answers rate_limited* and *Clear exit-survey answer (ask again)*.

### Localization
- Host-side language sync: set `LicenseStrings.LanguageOverride` from the app's selected locale
  so the license UI follows the simulator's language menu, not the headset language
  (VirtualChainsaw: `LicenseLanguageSync`, Unity Localization `SelectedLocaleChanged`). The same
  code is sent as `p_locale`.
- All new texts (feedback title, rating hint, categories, placeholder, Send/Skip, thank-you,
  errors, survey question and answers, company line, intro-offer note and durations) live in
  `LicenseStrings` in the same 11 languages as the license panels, follow the language the host
  shows (`LicenseStrings.LanguageOverride`) and fall back to English per label when the TMP font
  and its fallbacks (CJK) cannot draw the text, exactly like the existing panels.

## 1.12.0-pre.9 — 2026-10-23 (tag `v1.12.0-pre.9` = 40c093d, telemetry only)

- **Purchase telemetry.** New store event `plans_opened`, queued once per opening of the plans
  panel (`LicenseManager.ShowPlans`: welcome "Subscribe" button or the game's pause menu).
  Server side: `report_device_session` must accept the name in its `p_events` allow-list
  (migration `20261101000000`) and the contract (`03-LICENSING-CONTRACT.md` §8) lists it with
  the others: `plans_opened`, `checkout_opened`, `checkout_completed`, `checkout_cancelled`,
  `subscription_verified`. Fields are unchanged: name, sku, term (empty for plans_opened), at.
- `plans_opened` and every `checkout_*` event are reported **immediately** through
  `report_device_session` instead of waiting for the next session report. Reports are serialised
  (one in flight at a time) so the demo delta is never sent twice.
- The event queue is **persisted** in PlayerPrefs (`vrl_store_events`, no personal data) and
  flushed with the first report after start; a failed report puts its events back in the queue.
- `StoreEventQueue` (new, plain C#) with edit-mode tests under `Tests/Editor`
  (`VRLicensing.Tests`): JSON shape without user/device ids, persistence round trip, drain,
  restore order, 50-event cap, escaping.
- No contract change beyond the new event name (RPC signature and Edge Function unchanged).

## 1.12.0-pre.8 — 2026-10-22

- **Plans before and during the demo** (store builds only). The welcome panel gains a localized
  "Subscribe" button (11 languages) next to "Start Free Demo", and `LicenseManager.ShowPlans()`
  is public so a project can open the same plans panel from its own UI (e.g. a pause menu).
  The panel is the existing plans column on its own — Meta prices, data-driven "Best value"
  badge, benefits line, active-subscription detection — plus a localized "Back" button. No
  URL, QR or price outside Meta (App Policies 1.1).
- Opened during the demo, the demo stopwatch is **paused** (time used is persisted, HUD hidden)
  and resumes on "Back" with exactly the remaining time; a purchase ends the demo through the
  normal `SubscribedViaStore` path. `ShowPlans()` is a no-op outside `Unlicensed` / `Demo` or
  without a store platform, so buttons can be wired unconditionally.
- Per-project integration (see README): add a "Subscribe" entry to the game's pause menu that
  calls `ShowPlans()` and shows itself only while `CurrentState == Demo && StoreOffersEnabled`
  (VirtualChainsaw: `PauseMenuSubscribeButton` on the rig prefab's pause menu).
- No contract change (RPCs, Edge Function, LicenseConfig fields unchanged).

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
- **Admin demo reset reaches the headset.** `get_device_status` / `report_device_session` now
  return `demo_reset_at` (web migration `20261022100000_device_rpcs_return_demo_reset_at`).
  The first time the client sees a new date it adopts the server's (lower) total, restarts the
  running demo stopwatch and remembers the date (encrypted, sealed), instead of re-pushing its
  old local total as a delta — which is what undid every reset until now. Servers without the
  field behave exactly as before; the additive-delta contract is unchanged.
- No other contract change (Edge Function, LicenseConfig fields unchanged).

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
