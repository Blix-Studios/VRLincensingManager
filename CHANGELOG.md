# Changelog

All notable changes to `com.blixstudios.vr-licensing`. Pre-release tags (`vX.Y.Z-pre.N`) are
test builds for VirtualChainsaw; the final tag is cut only after QA on the headset.

## Unreleased — 1.12.0-pre.16 (not tagged: waiting for the on-device confirmation)

Device telemetry and demo counting (spec `11-DEVICE-TELEMETRY.md` §6; contract §2 item 3 gains
optional parameters). Built on pre.15, whose tag (2eea18d, layout fix only) is not moved.

- **Demo clock rewritten** (`DemoStopwatch`, used by `DemoModeManager`). Until pre.15 the demo
  time was `Time.unscaledTime - sessionStart`: wall-clock time that only stopped on
  `OnApplicationPause(true)`. A headset taken off mid-demo kept counting until Android paused
  the app, the expiry was only detected on the next frame, and nothing capped the value written
  or reported (rows of 17-19, 118 and 1141 minutes against a 15-minute limit). Now the clock is
  an accumulator fed one frame at a time:
  - a frame counts only while the app is not paused, has focus (ignored in the Editor) and the
    user is present (`CommonUsages.userPresence` of the head device; "present" when the runtime
    does not report it; hosts can set `DemoModeManager.UserPresenceProvider`);
  - a single frame longer than 3 s (`DemoStopwatch.MaxFrameGapSeconds`) is dropped whole, and so
    is the first frame after any pause;
  - the total stops at the effective limit exactly (a stored total already above it is left
    alone) and the time counted so far is persisted on focus loss as well as on pause;
  - `LicenseManager` stops a clock found running outside `Demo` state (a net under the existing
    paths: key redeemed, subscribed, plans/feedback opened, expiry).
  Players no longer lose demo time by taking the headset off.
- `[VR Licensing] demo:` lines for `StartDemo`, "clock held (reason)", "clock running again" and
  "frame gap of N s ignored" are now logged in **release builds** too, with both clocks
  (`unscaledTime`, `realtime`); the 30-second tick stays development-only.
- **Report id and no double counting** (`DemoReportLedger`): every `report_device_session`
  carries `p_report_id` (a new uuid per report). A report whose outcome is unknown (timeout, no
  connection, 5xx, unreadable answer) stays pending: the next report first re-reads the server
  total with `get_device_status` and sends a new delta from it; only when that refresh fails too
  is the same report sent again with the same id and delta. This closes the suspected double
  count where the expiry report timed out after being applied and the expired panel's
  `plans_opened` flush sent the same delta again.
- **Delta clamp**: `delta = clamp(local - server, 0, limit - server)`, so a headset holding an
  over-counted total from an older build reports at most what is left up to the limit.
- **New report fields** (`DeviceSessionReport`): `p_app_version` (`LicenseManager.AppVersion`,
  default `Application.version`; hosts set "1.0.0 (31)"), `p_upm_version` (`PackageVersion.Value`),
  `p_license_state` (`demo` | `demo_expired` | `licensed` | `license_expired` |
  `store_subscribed` | `blocked`) and `p_locale` (the language the license UI shows). Empty
  values are omitted.
  - `license_expired` (sixth value, decided 2026-11-06): a key or a store subscription ran out or
    was revoked. `demo_expired` is for the demo only. `Unlicensed` (welcome panel) reports
    `demo`; an admin block of the demo reports `blocked` unless the headset is unlocked or its
    licence expired (the more specific state wins); `ClockTampered` reports nothing.
  - **Old servers keep working**: a server that does not know the parameters answers PostgREST
    function-not-found (404 / PGRST202); the same report is sent once more without them and the
    rest of the run skips them (`SupabaseClient.ExtendedReportSupported`).
  - Not sent on `get_device_status`: no server accepts them there yet.
- `PackageVersion.Value` is generated from `package.json` by `Editor/PackageVersionGenerator.cs`
  (on editor load, only where the package is editable) and checked by a test, so the constant
  is never typed by hand.
- A `report_device_session` answer with `ok:false` is now treated as a refusal instead of being
  read as "server total 0".
- **Device code in the feedback panel**: already there since pre.11 (footer of every panel,
  including plans and feedback). The pause menu belongs to each app.
- README: the gate is modal — panels must be dismissed before the app's own menus can be used.
- Tests: `DemoStopwatchTests` (12: frame gap of 20 min ignored, nothing counted while paused /
  unfocused / not worn, stops at the limit, stored total above the limit, raise after expiry,
  nothing outside the demo), `DeviceSessionReportTests` (17: fields sent, old contract body,
  one retry on function-not-found, timeout = unknown outcome, ledger ids / clamp / unconfirmed
  reports, state mapping, generated version).

## 1.12.0-pre.15 — 2026-11-04 (tag `v1.12.0-pre.15`)

Layout fix only: no strings, flows or server contract change.

- **Welcome panel, store builds**: the four-button column (demo / subscribe / key / QR) sat on top
  of the "Select an option to continue" subtitle since pre.8, covering the lower ~40% of the text
  in every language (subtitle y 85..115, first button y 53..98). The text fitted its own rect, so
  overflow checks passed. The column is now anchored between the subtitle and the info line
  (10 px gap to each) and stacks from the top, so it cannot reach them whatever the number of
  buttons. Spacing 10 with four buttons, 12 with three. Direct APKs move about 5 px.
- **License-expired panel, store builds**: same pattern with a text. The message box (y 98..143)
  reached 7 px into the column titles (y 79..105). It was tight rather than cut with two lines, and
  would have collided on a third. The message moved 10 px up, clear of both.
- **Key-entry and demo-expired panels**: checked, no overlap.
- **Backdrop documented, not changed**: the overlay's raycast target is deliberate and is not what
  makes the gate modal. While a panel is open `SyncModality` disables every raycaster outside the
  license UI and the keyboard in the whole scene (43e42bf: a tester started the app through its
  own menu without touching demo or license). So a host's language selector cannot be pressed
  while a panel is open, wherever it stands; see the README.
- **Tests**: `PanelLayoutTests` (26 cases) builds the real UI and compares rects: each panel's
  subtitle against its controls and other texts, controls against each other, in the store and
  direct-APK variants. Checked against the pre.14 geometry: it fails with the numbers above.

## 1.12.0-pre.14 — 2026-11-03 (tag `v1.12.0-pre.14`)

The whole license UI follows the user's language (12 languages: en, es, fr, de, it, pt, ru, zh,
hi, fi, sv, nl). Until pre.13 only the device code, the subscription column, feedback and the
exit survey were localized; the buttons, titles, status lines and error messages were English.

- **78 strings moved from code into `LicenseStrings`** (`LicenseStrings.Key.*`, `Get`, `Format`,
  `ShortDuration`): welcome panel ("Start Free Demo", "Enter License Key", "Scan QR",
  "Select an option to continue", "Free demo: {0} remaining of {1}" with localized min/h units),
  key panel (title, hint, "Activate License", "Validating...", "Back"), demo-expired and
  license-expired panels (titles, messages with the app name and the server reason, "Already
  have a license key?", "Enter a license key"), the store column ("Unlock the full course",
  "Loading plans…", "Billed through your Meta account…", checkout / verification status
  lines, "Plans are not available right now…"), the success panel (subscription and key
  variants), the QR scan HUD and every scanner status line, the direct-APK organisation hint and
  purchase CTA (promo code, QR hints), the demo HUD ("Demo  ·  14:59 left"), and every message
  `LicenseManager` / `SupabaseClient` hand to the UI (store unavailable, checkout cancelled /
  failed, account / subscription verification failures, no subscription, entitlement blocked,
  internet needed, license not active, clock tampered, connection / response errors, invalid
  key). The English "¡Key redeemed successfully!" lost its stray "¡".
- Dutch uses the informal "je" throughout (upm_nl_strings.md); German/French/Russian/Chinese/
  Hindi keep the formal register of their existing rows. The welcome buttons reuse the app's
  Dutch labels ("Probeer de demo", "Licentiecode invoeren").
- **Layout kept**: every button label auto-sizes down (14→9 / 15→10) only if a language needs
  it; the longest labels (Russian "Начать бесплатную демоверсию", German "Lizenzschlüssel
  eingeben") fit at full size on the 400/260 px buttons. Multi-line messages (expired panels,
  organisation hint, success subtitle, store status, scan subtitle) auto-size instead of
  being cut with an ellipsis. The welcome info line is now two lines (520×36) so the offline
  notice fits in German; when the entitlement check blocks the app the message takes the
  middle of the panel. Plan buttons and the rating stars keep their fixed sizes.
- **Language changes while a panel is open re-render it**: `LicenseStrings.LanguageOverride` is
  now a property raising `LicenseStrings.LanguageChanged`; `LicenseUIBuilder` re-renders every
  bound label, the device codes, the store texts and the HUD. Host code that assigns the field
  compiles unchanged.
- `LicenseManager.LastCheckoutCancelled` replaces the UI's comparison against the English
  "Checkout cancelled." text.
- **"Demo limit from server" is logged on the first adoption of every run** (`… 900s
  (unchanged).`), not only when the value changes — logcat always shows the limit in force.
- Still English by design: text the server sends (`validate_and_bind_license` error messages,
  admin notices), status codes in parentheses (`revoked`), the key placeholder, prices, the
  purchase URL, app and term names from the store.
- Tests (`LicenseStringsCoverageTests`, 8): every key in every language, constants ⇄ rows,
  placeholders match English, every row formats, Dutch informal, English/key fallback,
  ShortDuration / welcome / HUD lines, LanguageChanged. 44 edit-mode tests pass.

## 1.12.0-pre.13 — 2026-11-03 (tag `v1.12.0-pre.13`)

Demo time decided by the server, per headset (spec `10-DEMO-TIME-CONTROL.md` §6; contract §1
RPCs and the "Demo mode" bullet change — see the spec's contract note).

- **Effective demo limit** = `demo_limit_seconds` from the last `get_device_status` /
  `report_device_session` answer (> 0) → last cached server value → `LicenseConfig.demoDurationSeconds`
  (offline first launch only). `LicenseManager.EffectiveDemoLimitSeconds` exposes it; the rules
  live in the new pure `DemoLimit` class.
- The limit is **cached sealed** in `SecureLicenseStorage` (`vrl_demo_limit`, next to the demo
  seconds used; part of the integrity seal only when present, like the store entitlement and the
  reset marker, so headsets updated from older builds are not flagged as tampered).
- Used everywhere the config value was used: `StartDemoMode`, the startup and start expiry
  checks, `DemoModeManager.StartDemo`, the welcome sign ("remaining of …") and, through the
  manager's limit, the HUD countdown and its watchdog.
- **Raised limit re-opens an expired demo**: an expired demo (not a license/store expiry, not an
  admin block) whose used time is under the new limit returns to the welcome panel. A lowered
  limit is pushed into the running demo (`DemoModeManager.SetLimit`) and ends it on the next tick
  when already exceeded; at startup it is simply the new threshold.
- The same re-open path now also applies to an **admin demo reset received mid-run** (before,
  the stored total was lowered but the expired panel stayed until the next launch).
- `get_device_status` with `found=false` is honoured too (the server returns the resolved default
  for unregistered headsets).
- `LicenseConfig.demoDurationSeconds` stays as the offline fallback; its tooltip and the setup
  window label say so.
- Tests (`DemoLimitTests`, 7): server limit wins, cache used offline, config used with no cache,
  raise un-expires, lower expires, `DemoModeManager.SetLimit` end to end, sealed cache round trip.
  36 edit-mode tests pass.
- Old builds (pre.12 and earlier, including the published Chainsaw build) ignore the server value
  and keep their config time. Chainsaw build 29 and Forklift build 5 must pin this tag.

## 1.12.0-pre.12 — 2026-10-30 (tag `v1.12.0-pre.12`)

- **Dutch (`nl`)** added to every license-UI string table in `LicenseStrings` (device code, best
  value, benefits, term labels, Subscribe/Back, intro-offer durations and note, feedback panel,
  exit survey, company line) — informal "je" form, "jaar" invariable after a number, exit reason
  `for_my_company` reads "Het is voor mijn bedrijf of school" (first Dutch customer is a school;
  the reason code is unchanged). `SupportedLanguages` has 12 entries and
  `SystemLanguage.Dutch` maps to `nl`; hosts with their own selector pass `"nl"` through
  `LicenseStrings.LanguageOverride`. No contract change.

## 1.12.0-pre.11 — 2026-10-25 (tag `v1.12.0-pre.11`) — use this one

Same code as `v1.12.0-pre.10` (798fb4e) with `package.json` at the right version. **pre.10 was
published with a stale `package.json` (`1.12.0-pre.9`)**: do not pin it. Rule from now on: the
`package.json` bump goes in the same commit that gets the tag, never in a later one.

## 1.12.0-pre.10 — 2026-10-25 (tag `v1.12.0-pre.10` = 798fb4e, superseded by pre.11)

Engagement layer, part 1 (spec `08-ENGAGEMENT-CAMPAIGNS.md` §4–§5, contract §9), on top of
the pre.9 telemetry: `plans_opened` on every panel with plans, intro offers, feedback panel,
demo exit survey and the host-side language sync. First consumer: VirtualChainsaw build 28
(not uploaded), pinned to pre.11. `package.json` at this tag still reads `1.12.0-pre.9` — see pre.11.

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
