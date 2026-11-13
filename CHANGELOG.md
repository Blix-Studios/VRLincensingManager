# Changelog

All notable changes to `com.blixstudios.vr-licensing`. Pre-release tags (`vX.Y.Z-pre.N`) are
test builds for VirtualChainsaw; the final tag is cut only after QA on the headset.

## 1.12.0-pre.19 — 2026-11-13

> **Do not pin `v1.12.0-pre.18`.** That tag exists on origin but points at commit 97fbd7c, whose
> `package.json` and `PackageVersion` still say 1.12.0-pre.17: the release script failed before the
> version commit and the tag command ran anyway. Tags are never moved, so the same code is released
> here as pre.19 with the version bumped in the tagged commit. Comments in the sources that say
> "pre.18" refer to this work.

- **Branding appliers never modify a shared material asset.** pre.17 made the decal applier
  find the right property (`Base_Map`), and with that it started writing the client's logo
  into the projector's material — the project's asset. In the Editor that outlives Play mode:
  VirtualChainsaw's `LogoDecal_MAT.mat` was saved with `Base_Map` empty after one Play session
  with a logo (found while verifying build 32; no build was affected, a player never saves
  assets, but a commit of that file would have removed the default logo).
  - `BrandingDecalApplier` now puts a runtime copy of the material on its Decal Projector
    (`BrandingMaterials`: one copy per original material + property + texture, shared by the
    appliers that need the same, flagged `DontSave`) and never calls `SetTexture` on the asset.
  - `BrandingMeshApplier` keeps the MaterialPropertyBlock path (nothing but the renderer
    changes); with `useMaterialPropertyBlock` off it uses the same kind of copy through
    `sharedMaterials` instead of `renderer.materials`, whose instances were never destroyed.
  - On `OnDestroy` (and `RestoreOriginal()`) the projector / renderer gets its original
    material back and the copy is released; it is destroyed with its last user. Giving the
    original back is only the safety net: the asset was never written in the first place.
  - `BrandingImageApplier` only ever changed its own `Image`; unchanged.
  - **Behaviour change**: only projectors that carry a `BrandingDecalApplier` show the logo.
    Before, every decal sharing the material showed it, because the material itself changed.
  - `ApplyLogo(Texture2D)` is public on the decal and mesh appliers.
  - Test `BrandingMaterialSafetyTests.PlayMode_WithAClientLogo_LeavesTheMaterialAssetsOnDiskUnchanged`:
    creates a decal material and two banner materials on disk, enters Play mode, applies a
    logo through a real Decal Projector and both mesh paths, leaves Play mode, forces a save
    and compares the `.mat` files byte for byte. The decal part needs the Universal RP
    package (inconclusive without it; the banner part always runs).
- **Labels that were not drawn in Arabic and Urdu** (VR-Forklift QA on pre.17,
  `DEFECT-pre17-ar-ur-expired-titles.md`). The column titles of the demo-expired panel
  (`ExpOrgTitle`, `ExpRedeemTitle`) showed nothing: a 16 pt line of Noto Sans Arabic needs
  33.8 units (2.11 x the font size), the rects are 26 high, and TextMeshPro's Ellipsis mode
  draws nothing when not even one line fits. Fixed as a pattern, not per label:
  - every label created by the license UI, input placeholders included, may now shrink to
    fit (auto-size from its design size down to 55 %, never under 7 pt); a label that fits
    keeps its size. With that face the titles are 12.3 pt instead of 16, `KeySubtitle` 11.8
    instead of 13;
  - last resort: a label that still draws none of its text is switched from Ellipsis to
    Overflow, so the text crosses the rect's edge instead of not being drawn.
  - `LabelFitTests`: every label of every panel, in the 16 languages and the three build
    variants (store with SKU, free store app, direct APK), must draw at least one character
    and not overflow — once with the fonts' own metrics and once with the line as tall as the
    face that showed the bug (there: nothing invisible, and the reported titles fit). With the
    fix switched off the tall-line cases fail on exactly the reported labels (`ExpOrgTitle`,
    `ExpRedeemTitle`, `LicExpRedeemTitle`, the store column titles, `ExpBuyTitle`) plus the
    input placeholders. The tests use system fonts and are skipped where those are missing.
- **Right-to-left labels have pair kerning off.** Found while rendering with VR-Forklift's
  own faces: TextMeshPro applies a kerning pair as if the text ran left to right, so with a
  face that carries kerning (Noto Sans Arabic loaded as a dynamic font: reh + kaf, reh +
  meem…) the reh was drawn on top of the next letter ("للشركات"). Kerning is switched off
  while a label is in right-to-left mode and comes back with left-to-right text.
- **Urdu**: a license key is now «کلید» in all 19 strings that mention one. The loanword
  «کی» reads as the genitive particle ("کیا آپ کے پاس لائسنس کلید ہے؟").

Tests: 284 EditMode tests (180 in pre.17).

## 1.12.0-pre.17 — 2026-11-13

Blockers from VR-Forklift batch 1 QA. Contract `03-LICENSING-CONTRACT.md` §2 is unchanged: this
release removes the last places where the client did not follow it.

- **Free store apps (no subscription SKU).** A store build whose `storeSubscriptionSku` is empty
  showed "Subscribe" on the welcome panel and opened an empty plans panel. Now such a build has
  no subscribe or plans entry point at all (`StoreOffers`): no button on the welcome panel, no
  plans panel, no plans column on the demo-expired and license-expired panels (they show the
  neutral "ask your organisation for a key" hint), `ShowPlans()` and `SubscribeViaStore()` do
  nothing, the boot subscription lookup is skipped and no `plans_opened` / checkout event is
  queued. `LicenseManager.StoreOffersEnabled` is false, which is what the pause-menu helpers of
  VirtualChainsaw and VR-Forklift already gate on. A store build never shows the web purchase
  CTA (URL, QR, price), with or without a SKU, whatever `showPurchaseCta` says.
- **Security: no direct table access left.** When `get_license_branding` failed or was missing,
  the package read the `license_branding` table directly with the anon key; `report_telemetry`
  had the same kind of fallback inserting into `device_telemetry`. Both fallbacks are removed
  (`SupabaseClient.FetchBranding` and `SendTelemetry` no longer exist): the client only calls
  SECURITY DEFINER RPCs. When the branding call fails for any reason, the branding kept on the
  device from the last good answer is used (name and logo file, `BrandingCache`); with no cache
  there is no branding. A rate-limited or "license not valid" answer counts as a failure, so it
  cannot wipe a client's branding; "none configured" from the server clears the cache.
  Both functions are in migration `20261013100000_licensing_v1_12_store_entitlements.sql`,
  confirmed applied on the production project (`supabase migration list`, 2026-11-13).
  `BrandingTests.RuntimeNeverTouchesATable` fails the suite if a `/rest/v1/<table>` URL or a
  table name ever comes back into the runtime sources.
- **Branding decals on URP 17.** `BrandingDecalApplier` only tried the configured property
  (`_Base_Map`); URP 17's decal Shader Graph calls it `Base_Map`, so nothing was applied
  (VirtualChainsaw's decals). Appliers now look on the material for `_Base_Map`, `Base_Map`,
  `_BaseMap`, `_MainTex` after the configured name.
- **Branding images keep their shape and their transparency.** The logo is fitted to the
  surface, whole and with its own aspect ratio (`BrandingTexture.Fit`): decals use the
  projector's width / height and stay transparent around the logo; meshes use the shape of the
  texture the material had (or `aspectOverride`) and, on an opaque material, composite the logo
  over `backgroundColor` (white by default) — a transparent PNG no longer shows black; UI
  Images use `preserveAspect`. Options `fitLogo` / `preserveAspect` (default ON) give the old
  stretch back. Appliers also listen for later branding answers instead of only the first one.
- **Default app version.** `p_app_version` is "<Application.version> (<versionCode>)", the
  code read from the installed APK, when the host does not set `LicenseManager.AppVersion`
  (`LicenseBuildInfo`). The per-project helpers (VR-Forklift `LicenseAppVersion`, Chainsaw
  `AppVersionInfo`) keep working — a host value wins — and can be deleted.
- **Arabic, Japanese, Korean and Urdu** in every license-UI string (16 languages;
  `LicenseStringsExtraLanguages.cs`). Arabic and Urdu are shaped by the package (`RtlText`:
  contextual forms, lam-alef, Latin and digits kept readable, brackets mirrored, rich-text tags
  untouched) and their labels switch to TextMeshPro's right-to-left mode, so they wrap
  correctly; no shaping plugin is needed, only a font with the Arabic presentation forms.
  Text already shaped by a host's own fixer is recognised and drawn as it is.
- **Labels are never left empty.** In VR-Forklift the Arabic locale swapped TMP's default font
  for one without Latin glyphs while the package had no Arabic strings: the labels held English
  text in a font that could not draw it. A label now takes the first font that can draw its
  text — its own, TMP's current default, the font the UI was built with — and English when no
  font has the language (`LabelFallback`).

Tests: 180 EditMode tests (103 before). New: `StoreOffersTests` (both configs on the real UI),
`BrandingTests`, `AppVersionTests`, `ExtraLanguagesTests`. Renders of the welcome panel with
and without SKU and of the Arabic / Urdu / Japanese / Korean panels were checked by eye.
Not verified on a headset: the versionCode read (Android only) and the fonts of each project.

## 1.12.0-pre.16 — 2026-11-06

Device telemetry and demo counting (spec `11-DEVICE-TELEMETRY.md` §6; contract §2 item 3 gains
optional parameters). Built on pre.15, whose tag (2eea18d, layout fix only) is not moved.

**Device run that confirmed the cause** (2026-11-06, Quest 3, Chainsaw store build 30 with
pre.14, USB plugged in): the demo was started, the headset taken off after about 35 s of play
and left asleep for 39 minutes. 163 ms after the app resumed it logged `Demo time expired.` and
`Reporting device session (delta 2375s)`: the whole wall-clock time since the demo started,
for about 35 s really played (local total 2499 s against a 900 s limit). The headset slept at
once and Android did pause the app, so ordinary sleep is enough: on resume the old handler
re-based the session start on a `Time.unscaledTime` that had not advanced yet, and the next
frame jumped the whole gap. The fix below is covered by EditMode tests; its own headset run
comes with the first build that pins this tag.

- **Demo clock rewritten** (`DemoStopwatch`, used by `DemoModeManager`). Until pre.15 the demo
  time was `Time.unscaledTime - sessionStart`: wall-clock time that was only
  persisted on `OnApplicationPause(true)`. A headset taken off mid-demo counted the whole time
  it was off, asleep or not (see the device run above), the expiry was only detected on the
  next frame, and nothing capped the value written
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
- **Server clamp adopted locally** (web side, 2026-11-06): when the server holds a LOWER demo
  total than the headset, the sealed local total and `serverReportedDemo` follow it, so a limit
  raised afterwards is not eaten by the next report (local 6000 s, server 900 s, limit raised to
  1800 s: 900 s remain). After a `report_device_session` answer the server total always wins
  (plus what was persisted while the request was in flight; a re-sent report, or a first report
  sent before any server answer of the run, only ever raises).
  After `get_device_status` it wins only when the local total is above the limit it was counted
  under — an over-count from an older build. A local total at or under that limit is demo time
  played and not reported yet (a session that ended without expiring is reported at the next
  launch) and is kept: adopting the server total there would hand the time back on every launch.
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
- Tests: `DemoStopwatchTests` (13: frame gap of 20 min ignored, nothing counted while paused /
  unfocused / not worn, stops at the limit, stored total above the limit, raise after expiry,
  nothing outside the demo, server clamp + raised limit), `DeviceSessionReportTests` (20: fields sent, old contract body,
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
