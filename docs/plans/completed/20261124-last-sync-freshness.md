# Last-sync freshness in coarse buckets

## Behavior
- Today the status bar shows the age of the last PLC sync as a fractional second count, for
  example `Синхр.: 164.5 с назад`. The fraction is noise, and a large second count is hard to read.
- After the change the age shows in four buckets. Only the first bucket carries a number, and that
  number is a whole second count. The other three buckets are fixed phrases that say how stale the
  sync is. The exact strings are in `## UI design`.
- The label still refreshes every second. Once the age leaves the first bucket, the text stops
  changing until the bucket changes or a new sync resets it.
- Scope guard: the `Last sync:` / `Синхр.:` prefix, the `Never` / `Никогда` text, the label
  position and style, the sync status text and the step and recipe countdowns stay as they are.

## Flow branches
| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| No sync since start | `Синхр.: Никогда` (en `Last sync: Never`), as today | issue: `LastSyncNever` is unaffected | Acceptance 2 |
| Age below 30 s | whole seconds, rounded down: 0 to 29, `12 с назад` | issue table; agent: rounding down keeps the number below 30, so the first bucket never shows `30` | Acceptance 2, smoke 3 |
| Age from 30 s up to 1 min | `> 30 с назад` | issue table | Acceptance 2, smoke 4 |
| Age from 1 min up to 1 h | `> 1 мин назад` | issue table | Acceptance 2, smoke 5 |
| Age 1 h or more | `> 1 ч назад` | issue table | Acceptance 2 |
| Age exactly 30 s, 60 s or 3600 s | the older bucket: `> 30 с`, `> 1 мин`, `> 1 ч` | agent: the issue writes `< 30 s` and `>= 1 h`, so each boundary belongs to the bucket above it | Acceptance 2 |
| Last-sync time is ahead of the clock (the system clock moved back after a sync) | `0 с назад` | agent: a negative age has no meaning to the operator; showing 0 keeps the label in the first bucket instead of a negative number | Acceptance 2 |
| A new sync completes while a coarse bucket shows | the label returns to `0 с назад` on the next refresh | existing behavior: `PlcSyncExecutor` stamps `LastSyncTime`, the 1 s tick re-reads it | smoke 6 |
| UI language is English | the same buckets with the English strings from `## UI design` | issue table | Acceptance 2, 3 |

## Decisions
- The buckets and boundaries are built exactly as the issue table gives them. The issue calls the
  boundaries a starting proposal, not a fixed requirement. A later change of a boundary touches
  one rule and its tests, so it is cheap to revisit after field use.
- Only the first bucket carries a number. A number next to a Russian noun needs plural forms
  (`1 минута` / `3 минуты` / `5 минут`), which resx cannot select for one status label. rejected:
  `hh:mm:ss` like the countdowns; a minutes-old age carries no operational information beyond
  "stale", and an exact value that keeps changing draws the eye.
- Whole seconds are rounded down. rejected: rounding to nearest; 29.6 s would show `30 с назад`
  inside the `< 30 s` bucket, one second before `> 30 с назад` appears.
- The view model reads the current time from the injected `IScheduler` (`IScheduler.Now`) instead
  of `DateTimeOffset.UtcNow`. This is the existing clock seam of the UI layer: `UiDi.AddUi`
  registers `RxSchedulers.MainThreadScheduler`, `PlcMonitorViewModel` reads `IScheduler.Now`, and
  `UIFixture` passes a `HistoricalScheduler` that starts at a fixed
  clock (2030-01-01). Tests set `LastSyncTime = Scheduler.Now - age` and reach every boundary
  through the public `LastSyncTimeText`. The 1 s refresh
  interval stays as it is. rejected: an injected `TimeProvider`, a second clock seam in the same
  layer plus a new DI registration; rejected: a test stub timestamp backdated against the real
  clock, which cannot hit an exact boundary; rejected: making the private formatter visible to
  tests, which `CLAUDE.md` and the project rules forbid.
- One resource per bucket. The seconds bucket keeps the key `LastSyncAgoFormat`: its current values
  (`{0} s ago`, `{0} с назад`) already match the table. The three coarse buckets get new keys with
  no placeholder (names in `## Boundaries`). The number is formatted with the invariant culture,
  as today.
- Tests assert relations, not wording (`docs/architecture/error-reporting.md`, "What tests assert
  on errors"): the label equals the prefix resource wrapped around the expected bucket resource,
  under both cultures. Acceptance 3 checks the literal strings of the table key by key, and the
  smoke test checks them on screen.

## Caveats
- `>` is legal XML text inside a resx `<value>`; only `<` and `&` need escaping.
  ASSUMPTION: the resx source generator and the resource reader return `>` for a value written
  as plain `>`; fallback: write the character as `&gt;`; checked by: the Task 1 build and
  Acceptance 3. The visual check is smoke steps 4 and 5.
- `ResourceTranslationTests` compares the key sets and the `{n}` placeholder sets of the two resx
  files. A bucket key missing in one file, or a `{0}` in only one language, fails that test.
  probed: read `SemiStep.Tests/UI/Localization/ResourceTranslationTests.cs`, 2026-11-24.
- The label lives in `AppStatusBar.axaml` as a plain `TextBlock` bound to `LastSyncTimeText`, with
  no visibility condition. probed: read the binding, 2026-11-24.
- `RxSchedulers.MainThreadScheduler.Now` tracks the wall clock, the same clock that
  `PlcSyncExecutor` stamps `LastSyncTime` with (`DateTimeOffset.UtcNow`).
  ASSUMPTION: the Avalonia main-thread scheduler takes `Now` from the system clock, as every
  `LocalScheduler` does; `PlcMonitorViewModel` already relies on it for its countdowns; fallback:
  none needed in the code, a wrong clock would show in smoke step 3 as a non-zero start;
  checked by: smoke step 3.

## Constraints and non-goals
- The 1 s `Observable.Interval` refresh and its `Guarded` error path stay unchanged.
- Numbers stay invariant-culture; `CurrentCulture` is never assigned (`ui-localization.md`).
- No plural-form machinery, no relative-time library.
- No change to how or when `LastSyncTime` is stamped in Core.
- Tests stay as simple as possible; no speculative abstractions.

## UI design
The approved design is the issue's table, built as written:

| age | en | ru |
| --- | --- | --- |
| < 30 s | `12 s ago` | `12 с назад` |
| 30 s .. 1 min | `> 30 s ago` | `> 30 с назад` |
| 1 min .. 1 h | `> 1 min ago` | `> 1 мин назад` |
| >= 1 h | `> 1 h ago` | `> 1 ч назад` |

Each value sits inside the existing prefix: `Last sync: > 1 min ago`, `Синхр.: > 1 мин назад`.

## Gates
- none

## Acceptance
1. `dotnet build SemiStep.slnx` → `0 Error(s)`, `0 Warning(s)` (warnings and style rules are errors).
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=LastSyncFreshness"` →
   `22` passed, `0` failed (on master this filter matches no test). The 22 cases are ten ages
   (0 s, 12.7 s, 29.9 s, 30 s, 59.9 s, 60 s, 3599.9 s, 3600 s, one day, 5 s ahead of the clock)
   and `Never`, each under `en` and `ru`.
3. One check per key and value, in both files; a swapped value fails it:
   ```bash
   python -X utf8 -c "
   import xml.etree.ElementTree as ET
   d = 'SemiStep/SemiStep.UI/Localization/'
   expected = {
       'Resources.resx': {'LastSyncAgoFormat': '{0} s ago', 'LastSyncOver30SecondsAgo': '> 30 s ago',
                          'LastSyncOverMinuteAgo': '> 1 min ago', 'LastSyncOverHourAgo': '> 1 h ago'},
       'Resources.ru.resx': {'LastSyncAgoFormat': '{0} с назад', 'LastSyncOver30SecondsAgo': '> 30 с назад',
                             'LastSyncOverMinuteAgo': '> 1 мин назад', 'LastSyncOverHourAgo': '> 1 ч назад'}}
   for f, pairs in expected.items():
       root = ET.parse(d + f).getroot()
       for k, v in pairs.items():
           node = root.find(f\"data[@name='{k}']/value\")
           print(f, k, 'OK' if node is not None and node.text == v else 'FAIL')
   "
   ```
   → eight lines, each ending in `OK`.
4. `grep -n '"0.0"' SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs` → no output.
5. `dotnet format SemiStep.slnx --verify-no-changes` → exit code 0.
6. manual smoke (Russian preset, PLC emulator):
   1. Start the emulator:
      `dotnet run --project SemiStep/SemiStep.Tools.PlcEmulator/SemiStep.Tools.PlcEmulator.csproj -- --config-dir ConfigFiles/MBE --port 10102`
      → the emulator window opens.
   2. Start the app:
      `dotnet run --project SemiStep/SemiStep.UI/SemiStep.UI.csproj -- --config-dir ConfigFiles/MBE --plc-endpoint 127.0.0.1:10102`
      → the status bar shows `Синхр.: Никогда`.
   3. Click the sync button in the status bar (the button bound to `ToggleSyncCommand`, beside
      the sync status text). If the PLC conflict dialog opens, keep the local recipe. Add a step
      or edit a cell → the label shows `Синхр.: 0 с назад` and counts up in whole seconds, with
      no decimal part.
   4. Wait without edits → at 30 s the label changes to `Синхр.: > 30 с назад` and stays.
   5. Keep waiting → at 60 s the label changes to `Синхр.: > 1 мин назад` and stays.
   6. Edit a cell again → the label returns to `Синхр.: 0 с назад` within about a second.
- not visible to headless tests: real wall-clock transitions (smoke 3 to 6). Covered by tests,
  not by the smoke: the 1 h bucket, the clock-moved-back case and the English strings.

---

## Context
- `SemiStep.UI/MainWindow/MainWindowViewModel.cs`: `FormatLastSyncTime` builds the text,
  `LastSyncTimeText` exposes it, the constructor holds the 1 s refresh.
- `SemiStep.UI/UiDi.cs`: `AddUi` registers `RxSchedulers.MainThreadScheduler` as the `IScheduler`
  singleton and `MainWindowViewModel` as a singleton.
- `SemiStep.UI/Plc/PlcMonitorViewModel.cs`: the existing reader of `IScheduler.Now`.
- `SemiStep.UI/Localization/Resources.resx` and `Resources.ru.resx`: `LastSyncNever`,
  `LastSyncAgoFormat`, `LastSyncPrefix`. The resx source generator writes the `Resources`
  accessor at build; edit both files, never a generated file.
- No test covers `LastSyncTimeText` today. The issue names two tests in
  `ViewModelLocalizationTests`; commit 86ca3a8 (#192) removed that file, so the tests are new.
- Tests: `SemiStep.Tests/UI/Helpers/UIFixture.cs` (`CreateMainWindowViewModel` builds a
  `HistoricalScheduler` for `PlcMonitorViewModel`), `SemiStep.Tests/Helpers/StubPlcSyncService.cs`
  (`LastSyncTime` always returns null today), `SemiStep.Tests/UI/Localization/ResourcesCultureScope.cs`
  (switches `Resources.Culture`). New UI tests use `[AvaloniaFact]` / `[AvaloniaTheory]` and the
  traits `Component=UI`, `Area=LastSyncFreshness`, `Category=Unit`.
- Rules: `docs/architecture/ui-localization.md`, `docs/architecture/error-reporting.md`
  ("What tests assert on errors"), `docs/architecture/headless-ui-testing.md`.

## Boundaries
- Resource keys Task 1 creates and Task 2 reads: `LastSyncAgoFormat` (kept, `{0}`),
  `LastSyncOver30SecondsAgo`, `LastSyncOverMinuteAgo`, `LastSyncOverHourAgo`.

## Tasks

### Task 1: Bucket resources in both languages

**Goal:** the four bucket strings of `## UI design` exist in English and Russian
**Entry:** `SemiStep.UI/Localization/Resources.resx`, key `LastSyncAgoFormat`
**Likely touches:** `Resources.resx`, `Resources.ru.resx`

- [x] keep `LastSyncAgoFormat` unchanged as the seconds bucket
- [x] add the three coarse-bucket keys to `Resources.resx`, English values from the table, no
      placeholder
- [x] add the same keys to `Resources.ru.resx`, Russian values from the table
- [x] tests: the existing `ResourceTranslationTests` prove key parity, non-blank values and
      matching placeholders
- [x] run tests - must pass before the next task

### Task 2: Age-to-bucket rule on the scheduler clock

**Goal:** `LastSyncTimeText` shows the bucket for the current age, per `## Flow branches`
**Entry:** `MainWindowViewModel.FormatLastSyncTime`
**Likely touches:** `MainWindowViewModel.cs`, `UIFixture.cs`, `StubPlcSyncService.cs`

- [x] the view model takes the `IScheduler` and reads the current time from `IScheduler.Now`
- [x] the age maps to a bucket: whole seconds rounded down below 30 s, then the three coarse
      buckets; boundaries go to the older bucket; a negative age shows as 0 s
- [x] tests: a test sets the current time and the last-sync time it observes; the fixture exposes
      the `HistoricalScheduler` it passes to the view models, and the stub sync service accepts a
      last-sync time
- [x] tests: under `en` and `ru`, the label equals the prefix resource around the expected bucket
      resource for the ten ages of Acceptance 2, and `Never` when no sync happened; the tests
      carry `Area=LastSyncFreshness`
- [x] run tests - must pass before the next task

### Task 3: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] Acceptance 1 to 5 pass with their expected results
- [x] Acceptance 6 manual smoke (skipped - operator smoke)
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`

### Task 4: [Final] Update documentation

**Goal:** docs describe the bucketed last-sync label
**Entry:** `docs/architecture/ui-localization.md`

- [x] `ui-localization.md`, section "Culture wiring": replace the `{elapsed:0.0}` example with a
      number the docs still format (the step counts already named there, or the whole-second
      last-sync count)
- [x] `ui-localization.md`, section "The resx model": the last-sync text is a set of bucket
      strings, only the first carries a number, and the reason (no Russian plural forms)

## Post-Completion
- the operator runs Acceptance 6 (manual smoke)
- closes #168
