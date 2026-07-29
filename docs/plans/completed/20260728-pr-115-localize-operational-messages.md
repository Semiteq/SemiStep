# PR-115: Localize operational and error messages

## Overview

Issue #115 — the resx pipeline exists (`Localization/Resources.resx` + `Resources.ru.resx`, a hand-maintained
`Resources.Designer.cs` accessor per key) and menus/dialog AXAML use it, but a class of UI-composed
user-facing strings is hardcoded English. A Russian-locale operator gets a mixed-language UI exactly on the
error and status paths where clarity matters.

This PR moves the UI-composed strings into resx. **Out of scope:** Core error *content*. Since #151
(merged), typed Core errors already localize on type via `ReasonLocalizer` at the panel seams; the
free-text `Result.Fail("...")` sites and exception `.Message` detail that remain stay English until their
per-subsystem waves (see the roadmap). #115 localizes the UI-composed CONTEXT strings around them — the
report+log context, panel-only literals, file-picker titles — a disjoint seam from #151's error-content
localization (they meet only at `ReportFailure(result, context)`, where the context is a pre-localized
plain string and the errors localize separately; no overlap).

### Settled decision: logs stay English, panel is localized

F1/F3 restructured most command-error strings into the report+log path, where a single `context` argument is
BOTH logged AND shown in the panel (`ExceptionReporter.ReportAndLog`: `logger.LogError(ex, "{Context}", context)`
then `panel.ReportError($"{context}: {ex.Message}")`). Localizing that context naively would localize the log
too. Decision (confirmed): **the log keeps the invariant/English value; the panel shows the current-culture
value.** A resx key yields both — `ResourceManager.GetString(key, CultureInfo.InvariantCulture)` for the log,
`ResourceManager.GetString(key, Resources.Culture)` for the panel.

## Mechanism (primary proposal — Fable to refine ergonomics)

- Add a small `LocalizedText` type (in `SemiStep.UI.Localization`) that carries a resx key and exposes
  `Invariant` and `Localized`:
  ```csharp
  public readonly record struct LocalizedText(string ResourceKey)
  {
      public string Localized  => Resources.ResourceManager.GetString(ResourceKey, Resources.Culture)          ?? ResourceKey;
      public string Invariant  => Resources.ResourceManager.GetString(ResourceKey, CultureInfo.InvariantCulture) ?? ResourceKey;
  }
  ```
  Call sites build it compile-safely with `nameof` against the designer accessor, e.g.
  `new LocalizedText(nameof(Resources.CopyStepFailed))` — a typo fails the build because the accessor must exist.
  `nameof(Resources.X)` is the chosen form (static per-key fields duplicate the key list; a `Resources.Text(...)`
  factory is the same call renamed). Two constraints: **no implicit conversion from `string`** (a raw literal must
  never silently masquerade as a key), and `.Localized` must tolerate `Resources.Culture == null` (it then falls
  through to `CurrentUICulture`, matching the designer accessors' behavior).
- Change the report+log seams to take `LocalizedText` instead of a bare `string context`:
  - `ReportThrownExceptions(this ReactiveCommand<,>, MessagePanelViewModel, ILogger, LocalizedText)`
  - `ExceptionReporter.ReportAndLog(MessagePanelViewModel, ILogger, LocalizedText, Exception)` — logs `.Invariant`, reports `$"{.Localized}: {ex.Message}"`
  - `MainWindowViewModel.Guarded(LocalizedText, Action)` and `OnSubscriptionError(LocalizedText)`
- Panel-only sites (no log) that pass a plain string get the localized accessor directly:
  `ReportError(Resources.X)`, `ReportFailure(result, Resources.X)`, `ReportSuccess(string.Format(CultureInfo.CurrentCulture, Resources.XFormat, arg))`.

## Context (grounded on current master, post F1/F2/F3/#114/#151)

**Category A — report+log context strings (dual-use: logged + panel prefix → `LocalizedText`):**
- `Clipboard/ClipboardViewModel.cs:61,64,67` — "Copy failed", "Cut failed", "Paste failed".
- `RecipeFile/RecipeFileViewModel.cs:42,45,48` — "Save failed", "Save As failed", "Load failed".
- `MainWindow/MainWindowViewModel.cs:73,76,79,82` — "Sync toggle failed", "Style editor failed", "Exit failed", "Orientation toggle failed" (`ReportThrownExceptions`); `:90,91,96,97,103,104,243` — "PLC state update", "PLC conflict handling", "Sync time refresh", "PLC conflict resolution failed" (`Guarded`/`OnSubscriptionError`). **The three bare-topic contexts** ("PLC state update", "PLC conflict handling", "Sync time refresh") read as neutral status lines, not errors — reword them to failure phrasing ("PLC state update failed", etc.) as part of this task, so the panel entry reads as a fault in both English and Russian.
- `RecipeGrid/RecipeCommandsViewModel.cs:56,59,62,65` — "Add step failed", "Delete step failed", "Undo failed", "Redo failed".

**Category B — panel-only literals / formats:**
- `MainWindow/MainWindowViewModel.cs:231` — `ReportError("Failed to show PLC conflict dialog")`.
- `MainWindow/MainWindow.axaml.cs:160` — `ReportError($"Exit failed: {ex.Message}")` in `ShowExitChoiceAsync` (a SECOND "Exit failed", view-side — reuse the same key as the `ExitCommand` context).
- `Clipboard/ClipboardViewModel.cs:141` — currently `ReportError($"Paste failed: {errorMessages}")`, a raw sink with a manual `.Message` join. **Route it now** through the localized seam: `ReportFailure(recipeResult, Resources.PasteStepFailed)` (reuse the Category-A key), dropping the manual join. Satisfies #115's context localization AND removes a raw sink the clipboard/CSV wave would otherwise route later — mark that wave ROUTE item done-early in the roadmap. The paste-rejection errors are Core free-text today, so their *content* still renders English via the seam until their wave types them; the context prefix localizes now.
- `StyleEditor/GridStyleEditorViewModel.cs:308` — `ErrorMessage = $"Save failed: {ex.Message}"` on the editor's own error surface (the log at `:307` is already correctly separate English; `:290` already uses `Resources.EditorCannotSave`).
- `RecipeFile/RecipeFileViewModel.cs:103` — `ReportFailure(result, "Failed to save recipe")`; `:109,131` — `ReportSuccess($"Saved: {name}")` / `($"Loaded: {name}")` (→ resx format strings).
- `Coordinator/RecipeCoordinator.cs:420` — `ReportFailure(applyResult, "PLC reconnect")`.
- `RecipeGrid/RecipeGridSurfaceBase.cs:317,346` — `ReportFailure(result, $"Step {n}")` (→ resx format prefix). `:372` — currently `ReportError($"Step {n}: Failed to change action - {result.FormatErrors()}")`, a raw sink; **route it now** to `ReportFailure(result, string.Format(CultureInfo.CurrentCulture, Resources.StepActionChangeFailedFormat, n))` (a Step-{n} "change action failed" format key), matching its siblings at `:317/:346` and dropping the manual `FormatErrors()` join. Removes a raw sink the recipe wave would otherwise route later — mark that wave ROUTE item done-early. Error *content* renders English via the seam until the recipe wave types those errors.
- `Logging/GlobalExceptionBackstop.cs:21,107` — `RecoverableUserMessage = "An unexpected error occurred; see the log for details."` (panel; the log stays English separately).

**Category C — dialogs (file pickers only):**
- `MainWindow/MainWindow.axaml.cs:234,238-239,251,256-257` — file-picker `Title` "Open Recipe"/"Save Recipe" and the `FilePickerFileType` display names ("Recipe Files", "All Files", "CSV Files"). (Anchors match Task 3.)
- The window title (`MainWindowViewModel.BuildWindowTitle`, `$"SemiStep - {fileName}{dirtyIndicator}"`) is NOT in scope: "SemiStep" is the product name, `fileName` is user data, the dirty indicator is `" *"`, and the only natural-language part (the new-recipe placeholder) is already `Resources.WindowTitleNewRecipe`. No English remains to localize.

**Explicitly EXCLUDED (Core-owned / wave-owned — whitelist for the Task 4 straggler grep):** `RecipeGridSurfaceBase.cs:191` (`exception.Message`), `RecipeCoordinator.cs:522` (`error.Message`, the Core PLC-fault bridge — PLC wave routes it; see `error-reporting.md` "Localizing failures on type"), the pre-config `ErrorWindow` (`Program.cs`), and the Core `Result` texts joined at `GridStyleEditorViewModel.cs:193,297` (style-editor surface, wave-owned).

**Infra:** each new string = a `<data name="X">` in `Resources.resx` + the Russian in `Resources.ru.resx` + a `public static string X => ResourceManager.GetString("X", resourceCulture) ?? string.Empty;` accessor in `Resources.Designer.cs`. The two resx files currently hold 132 entries each (post-#151) and MUST stay matched. `MapSyncStatus`/`FormatLastSyncTime`/`PlcConflictDialogViewModel` show the established `Resources.X` / `string.Format(culture, Resources.XFormat, …)` pattern.

## Development Approach

- Regular (code, then tests). Warnings are errors; build stays clean after each task.
- Add English (`Resources.resx`) and Russian (`Resources.ru.resx`) for every key, plus the designer accessor. Keep the two resx entry counts equal (a test or grep asserts parity).
- Russian translations are added by this PR and flagged for a native-speaker pass in the PR body (not blocking).
- Tests set `Resources.Culture` to `ru` and assert representative panel messages are the Russian value, while the LOG uses the invariant/English value. Restore culture in a finally / use a serialized collection so parallel tests do not see a mutated static `Resources.Culture`.
- `dotnet build SemiStep.slnx` (0 warnings) + `dotnet test` after each task.

## Acceptance Evidence

**Automatable:**
1. Report+log localization: under `Resources.Culture = ru`, a command fault reports the Russian prefix to the panel AND logs the invariant English context (assert both via a real panel + `RecordingLogger`). `--filter "FullyQualifiedName~Localiz"` (or the relevant filter).
2. Panel-only sites: under ru, `ReportSuccess`/`ReportError`/`ReportFailure(context)` representative sites produce the Russian string.
3. resx parity: `Resources.resx` and `Resources.ru.resx` have the same set of `data name` keys (a test or a grep count check).
4. No stragglers: grep confirms no UI-composed user-facing literal remains at the enumerated sites (the report+log contexts, file-picker titles, window title, backstop message, ReportSuccess/ReportError/ReportFailure-context literals).

**Manual smoke:** run with `ui.locale=ru`; trigger a copy failure, a save success, a PLC conflict-dialog show failure, open the file pickers — the panel/titles are Russian; the log file shows English.

Full suite green + `dotnet build SemiStep.slnx` (0 warnings) is the gate.

## Progress Tracking

Mark `[x]` on completion; `➕` new tasks; `⚠️` blockers.

## Implementation Steps

### Task 1: Localization mechanism + migrate the report+log contexts (Category A)

**Files:**
- Create: `SemiStep/SemiStep.UI/Localization/LocalizedText.cs`
- Modify: `SemiStep/SemiStep.UI/MessageService/ReactiveCommandReportingExtensions.cs`, `SemiStep/SemiStep.UI/MessageService/ExceptionReporter.cs`
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs` (`Guarded`/`OnSubscriptionError` + its 4 ReportThrownExceptions + 4 Guarded/OnSubscriptionError contexts)
- Modify: `Clipboard/ClipboardViewModel.cs`, `RecipeFile/RecipeFileViewModel.cs`, `RecipeGrid/RecipeCommandsViewModel.cs`
- Modify: `Localization/Resources.resx`, `Localization/Resources.ru.resx`, `Localization/Resources.Designer.cs`
- Modify (existing tests that call the changed seams — WILL NOT COMPILE otherwise): `SemiStep.Tests/UI/MessageService/ReactiveCommandReportingExtensionsTests.cs`, `SemiStep.Tests/UI/MainWindow/MainWindowViewModelReportingTests.cs`; and `SemiStep.Tests/UI/Localization/ResourceSyncTests.cs` (parity leg)
- Create/Modify: new localization tests

- [x] add `LocalizedText` (Invariant/Localized from a resx key); add the resx keys + English + Russian + designer accessors for every Category-A context.
- [x] change `ReportThrownExceptions`, `ExceptionReporter.ReportAndLog`, `Guarded`, `OnSubscriptionError` to take `LocalizedText`; log `.Invariant`, report `$"{.Localized}: {ex.Message}"`. Remove the now-dead `ArgumentNullException.ThrowIfNull(context)` on the (struct) context arg in `ReactiveCommandReportingExtensions.cs:20`.
- [x] migrate all Category-A call sites to `new LocalizedText(nameof(Resources.X))`; reword the three bare-topic contexts to failure phrasing ("PLC state update failed", "PLC conflict handling failed", "Sync time refresh failed").
- [x] migrate the two existing seam-caller tests: `ReactiveCommandReportingExtensionsTests.cs:33,61` (string arg → `LocalizedText`, update the `"Copy failed: boom"` assertion to the resolved value); `MainWindowViewModelReportingTests.cs:53,68,116` — its `[AvaloniaTheory]`/`[InlineData("PLC state update")]` CANNOT hold a `LocalizedText` (not a compile-time constant), so restructure it (pass the key name as a string and build `LocalizedText` inside the test, or `[MemberData]`), and update every exact-message assertion for the reworded contexts + the default-culture resolved value.
- [x] parity: the three-way check is ALREADY mostly in `ResourceSyncTests.cs` (`RussianSatellite_ContainsEveryNeutralKey_AndNoOrphans` = en-keys == ru-keys; `Key_ResolvesToNonEmptyValue_UnderEnglishAndRussian` reflects the designer `string` props ⊆ resx). Add ONLY the missing leg there: every resx `data name` has a matching `public static string` accessor (resx ⊆ designer). Mirror its `PropertyType == typeof(string)` filter (excludes `Culture`/`ResourceManager`). Do not add a parallel parity test.
- [x] tests: under a `ru` culture set via the existing `ResourcesCultureScope` (`SemiStep.Tests/UI/Localization/ResourcesCultureScope.cs`, used via `WithCulture` in `ViewModelLocalizationTests.cs`), a throwing command reports the Russian prefix to the panel AND logs the invariant English context (RecordingLogger). Parallelization is already disabled assembly-wide, so no per-test serialization is needed beyond the scope's save/restore.
- [x] `dotnet build SemiStep.slnx` (0 warnings) + `--filter` green — before next task.

### Task 2: Localize the panel-only literals (Category B)

**Files:**
- Modify: `MainWindow/MainWindowViewModel.cs` (`:231`), `MainWindow/MainWindow.axaml.cs` (`:160`), `Clipboard/ClipboardViewModel.cs` (`:141`), `StyleEditor/GridStyleEditorViewModel.cs` (`:308`), `RecipeFile/RecipeFileViewModel.cs` (`:103,109,131`), `Coordinator/RecipeCoordinator.cs` (`:420`), `RecipeGrid/RecipeGridSurfaceBase.cs` (`:317,346,372`), `Logging/GlobalExceptionBackstop.cs` (`:21`)
- Modify: `Localization/Resources.resx`, `Resources.ru.resx`, `Resources.Designer.cs`
- Modify/Create: tests

- [x] add resx keys + EN + RU + accessors for: PLC-conflict-dialog-show-failed, save-recipe-failed context, Saved/Loaded formats, PLC-reconnect context, Step-{n} format prefix, the RecipeGridSurfaceBase `:372` message, and the backstop generic message. **Reuse existing keys** where the string repeats: the `MainWindow.axaml.cs:160` "Exit failed" reuses the Category-A Exit key; `ClipboardViewModel.cs:141` reuses `PasteStepFailed`; `GridStyleEditorViewModel.cs:308` reuses (or shares) the save-failed key.
- [x] replace the literals: `ReportError(Resources.X)`, `ReportFailure(result, Resources.X)`, `ReportSuccess(string.Format(CultureInfo.CurrentCulture, Resources.SavedFormat, name))`, the two `$"...: {ex.Message}"` view/editor sites → `$"{Resources.X}: {ex.Message}"`, `RecoverableUserMessage` → a resx accessor at the report site (keep each already-separate English log line English).
- [x] **route-now (two sites the waves would otherwise own):** `ClipboardViewModel.cs:141` `ReportError($"Paste failed: {join}")` → `ReportFailure(recipeResult, Resources.PasteStepFailed)` (drop the manual `.Message` join); `RecipeGridSurfaceBase.cs:372` `ReportError($"...{FormatErrors()}")` → `ReportFailure(result, string.Format(CultureInfo.CurrentCulture, Resources.StepActionChangeFailedFormat, n))` (matching siblings `:317/:346`). After this, update the roadmap: mark the clipboard/CSV and recipe wave ROUTE items for these two sinks done-early. (Roadmap already reflects both sinks as done-early.)
- [x] tests: under ru (`ResourcesCultureScope`), representative Category-B sites produce the Russian string.
- [x] build + `--filter` green.

### Task 3: Localize the file-picker titles and filters (Category C)

**Files:**
- Modify: `MainWindow/MainWindow.axaml.cs` — "Open Recipe" title `:234`, filters `:238-239`; "Save Recipe" title `:251`, filters `:256-257`
- Modify: `Localization/Resources.resx`, `Resources.ru.resx`, `Resources.Designer.cs`
- Modify/Create: tests

- [x] add resx keys + EN + RU + accessors for the file-picker titles ("Open Recipe"/"Save Recipe") and the filter display names ("Recipe Files", "All Files", "CSV Files").
- [x] replace the literals with the accessors. (The window title is NOT touched — see Context; no English remains in it.)
- [x] tests: the file-picker `Title`/filter strings resolve to the resx accessors; these are view glue, so assert the accessor values under ru (or a smoke note if the picker itself is not headless-drivable).
- [x] build + `--filter` green.

### Task 4: Verify + document

**Files:**
- Modify: `Docs/architecture/ui-localization.md` (the existing localization doc)

- [x] full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 1557 passed, 0 failed.
- [x] `dotnet build SemiStep.slnx` — 0 warnings, 0 errors.
- [x] `dotnet format SemiStep.slnx` — no files re-formatted; build stays clean.
- [x] confirm the three-way parity test from Task 1 is green (resx == ru.resx == designer accessors). `--filter "FullyQualifiedName~ResourceSync"` = 167 passed, 0 failed.
- [x] grep for stragglers: no UI-composed user-facing literal remains at a report/title site. The whitelist of by-design English (do NOT flag): `RecipeGridSurfaceBase.cs:191`, `RecipeCoordinator.cs:522`, `ErrorWindow`, and the Core `Result` joins at `GridStyleEditorViewModel.cs:193,297`. List what was checked. — Checked every `ReportError`/`ReportFailure`/`ReportSuccess`/`Title =`/`FilePickerFileType`/`RecoverableUserMessage` site plus a broad "English literal at a report/title site" grep across `SemiStep.UI`. All migrated to `Resources.X`/`LocalizedText`/`string.Format(Resources.XFormat…)`. Remaining English literals are the whitelist (now at `RecipeGridSurfaceBase.cs:192` `exception.Message`, `RecipeCoordinator.cs:523` `error.Message`, `GridStyleEditorViewModel.cs:193,297` Core `Result` joins, `ErrorWindow`), the window title (product name + user data, per Context), exception `.Message` suffixes, and `logger.Log*`/`Log.*` English templates. No straggler found.
- [x] update `ui-localization.md`: operational/error/status strings go through resx; the report+log path logs the invariant English and reports the current-culture value via `LocalizedText`; Core `Result`/exception text stays English by design.
- [x] mark this plan for archival at delivery (do NOT move it mid-run).

## Post-Completion

**Manual verification:** run with `ui.locale=ru` and walk the smoke scenarios; confirm the panel/titles are Russian and the log file is English.

**Translations:** the Russian strings this PR adds are a first pass — flag in the PR body for a native-speaker review; they are not build-blocking.

**Executed by exec:**
- branch: pr-115-localize-operational-messages

## Verify it yourself

Note: this machine's ambient UI culture is Russian, so the panel already renders Russian by default with
`Resources.Culture` unset — the meaningful proof is the log-stays-English split, plus the migration completeness.

1. **Automated, the load-bearing proof** — `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~Localiz|FullyQualifiedName~ResourceSync|FullyQualifiedName~Reporting"`.
   - `CommandErrorLocalizationTests` is the split proof: under `ru`, the panel entry is the Russian sentence (`"Не удалось скопировать: boom"`) while the RecordingLogger record is the English invariant (`"Copy failed"`). On `master` (before this branch) `LocalizedText` does not exist and the context was a plain string logged and shown identically.
   - `ResourceSyncTests` proves resx en == ru == designer accessors (163 each), including the new resx⊆designer leg.
   - The two `[AvaloniaFact]` route-now sink tests in `CategoryBLocalizationTests` prove `ClipboardViewModel` paste and `RecipeGridSurfaceBase` change-action now route through `ReportFailure` with a localized prefix.
2. **Log-English can't regress silently** — in `ExceptionReporter.ReportAndLog` swap `context.Invariant` for `context.Localized` on the log line and re-run: `CommandErrorLocalizationTests` goes red (log would become Russian). Restore it.
3. **Manual smoke** — run with `ui.locale=ru`; trigger a copy/save/paste failure, a save success, a PLC conflict-dialog show failure, open the file pickers; the panel + picker titles are Russian and the log file lines are English.

Full gate: `dotnet build SemiStep.slnx` (0 warnings) + `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` (1547 passed).
