# Slice 2 — Grid-style editor: async Save off the UI thread

## Overview

Slice 2 of the #118 debloat. `GridStyleEditorFacade.Load` is `async Task<Result<…>>`, but `Save` is **synchronous** and
does file I/O (`File.WriteAllText` + `File.Move`) on the UI thread — the editor's `SaveCommand` is
`ReactiveCommand.Create(Save, …)`, so a save blocks the dispatcher. Make the write path async end to end, matching
`Load`. Small, contained, independent of the other #118 slices (nesting, `StyleColor`, the VM split).

**Behavior-preserving.** Nothing about *what* is saved changes — `BuildRecord`, `Seed`, and the mappers are untouched, so
slice 1's round-trip guards keep holding. Only the *threading* of the write changes: the sync file calls become their
async equivalents and the command awaits them.

**Scope decisions (verified against the code):**
- **`Validate` stays synchronous.** `RecomputeCanSave` calls `_gridStyleEditorFacade.Validate(BuildRecord())` on **every
  property set** (per keystroke); it is an in-memory `GridStyleValidator` pass with no I/O. Making it async would push
  per-keystroke work onto the task pool for nothing. Only `Save` (the file write) goes async.
- **The window is unchanged.** `SaveCommand` stays `ReactiveCommand<Unit, bool>` — `CreateFromTask(Func<Task<bool>>)`
  produces exactly that type. `GridStyleEditorWindow.OnSaveCompleted(bool saved)` and its restart-prompt/close flow, and
  the `SaveCommand.ThrownExceptions.Subscribe(ReportSaveException)` routing, all keep working (`CreateFromTask` routes a
  faulted task to `ThrownExceptions`).
- **`File.Move` stays synchronous.** There is no async `File.Move`; it is a fast metadata rename. The *write* of the temp
  file becomes `File.WriteAllTextAsync` and the header read becomes `File.ReadAllLinesAsync`; the atomic move stays sync
  inside the same try/catch. The slice-7 Rule-B envelope (`GridStyleSaveFailedError.CausedBy(ex)`) in the catch is kept.
- **No `CancellationToken`.** Save is a user button-click with no cancellation source (same rationale as the user-load
  path). Do not thread a token.

## Acceptance Evidence

- `IGridStyleEditorFacade.Save` returns `Task<Result>`; `GridStyleEditorFacade.Save` and `GridStyleWriter` are async
  (`WriteAllTextAsync`/`ReadAllLinesAsync`, `File.Move` still sync); `GridStyleEditorViewModel` has `SaveAsync` wired via
  `ReactiveCommand.CreateFromTask`, `SaveCommand` still `ReactiveCommand<Unit, bool>`.
- `Validate` is still synchronous and `RecomputeCanSave` is unchanged.
- The window (`GridStyleEditorWindow.axaml.cs`) is untouched; the save→restart-prompt→close and the
  `ReportSaveException` fault path still work.
- Slice 1's guards (`Seed_PopulatesEverySurfacedProperty…`, the perturbation test, `SaveThenLoad_DistinctFixture…`) stay
  green (the last now awaits the async `Save`).
- `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format` clean.

## Task 1: Make the Save write-path async

**Files:**
- Modify: `SemiStep/SemiStep.Core/Configuration/IGridStyleEditorFacade.cs` (`Save` → `Task<Result>`).
- Modify: `SemiStep/SemiStep.Core/Configuration/GridStyleEditorFacade.cs` (`Save` → `async Task<Result>`; `await _gridStyleWriter.SaveAsync(...)`; `Validate` unchanged).
- Modify: `SemiStep/SemiStep.Core/Configuration/Loaders/GridStyleWriter.cs` (`Save` → `async Task<Result> SaveAsync`; `WriteAtomic` → async write + sync `File.Move`; `ReadLeadingCommentBlock` → async; keep the `GridStyleSaveFailedError.CausedBy(ex)` catch).
- Modify: `SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorViewModel.cs` (`Save()` → `private async Task<bool> SaveAsync()`; `SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, this.WhenAnyValue(vm => vm.CanSave))`; keep the `ThrownExceptions.Subscribe(ReportSaveException)`, the `!CanSave` early-return, and the slice-7 `ErrorMessage`-localize + `LogCausedByExceptions` failure handling).
- Modify: the affected tests so the suite compiles and passes (see blast radius).

- [x] `IGridStyleEditorFacade.Save(string, GridStyleOptions)` → `Task<Result>`. `Validate`/`Load` unchanged.
- [x] `GridStyleEditorFacade.Save` → `async Task<Result>`: `var validation = Validate(options); if (validation.IsFailed) return validation; return await _gridStyleWriter.SaveAsync(configDir, options);`.
- [x] `GridStyleWriter`: rename `Save` → `SaveAsync` returning `Task<Result>`; `await File.WriteAllTextAsync(tempPath, content, _utf8NoBom)` in `WriteAtomic` (make it `async Task`), `File.Move(..., overwrite: true)` stays sync in the try/catch; `ReadLeadingCommentBlock` → `async Task<string>` via `File.ReadAllLinesAsync`. The `catch (Exception ex)` still returns `Result.Fail(new GridStyleSaveFailedError(Path.GetFileName(filePath)).CausedBy(ex))`.
- [x] `GridStyleEditorViewModel`: `Save()` → `async Task<bool> SaveAsync()` (`await _gridStyleEditorFacade.Save(...)`), `SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, …)`. `SaveCommand` type stays `ReactiveCommand<Unit, bool>`. Leave `RecomputeCanSave`/`Validate` sync.
- [x] **Test blast radius** (grep for `.Save(` / `SaveAsync` / `SaveCommand` AND for `IGridStyleEditorFacade` implementers — grepping callers alone misses the fake-facade *declarations*):
  - **Fake-facade implementations** `ThrowingFacade` and `CausedByFailingFacade` (`GridStyleEditorViewModelTests.cs` ~:500/:518) — their `Save` declaration must become `Task<Result>`. Bodies: `CausedByFailingFacade.Save` wraps its return in `Task.FromResult(...)`; `ThrowingFacade.Save` keeps its synchronous `throw failure;` under the non-async `Task<Result>` signature — that synchronous throw is still captured into `SaveAsync`'s task and routed to `ThrownExceptions` (the whole fault-path test depends on it, so keep it a plain throw, not a rejected task).
  - **Direct writer callers** — `GridStyleWriterTests.cs` has **7** `new GridStyleWriter().Save(...)` sites (~:33/:53/:70/:84/:112/:130/:148) and `GridStyleOrientationTests.cs` **2** (~:85/:98) → `await ...SaveAsync(...)`. `GridStyleEditorFacadeTests.cs:137` likewise — and its method `Save_WriteFailure_CarriesOriginalExceptionOnCausedBy` (~:131) becomes `async Task` (was `void`), staying under a plain `[Fact]`. (`Save_UnderCommaLocale…` at `GridStyleWriterTests.cs:138` sets `CurrentCulture` around the call — culture flows across awaits via `ExecutionContext`, and `Serialize` runs before the first await anyway, so it stays green; `Save_WhenMoveFails…` awaits inside a `using` file-lock — fine.)
  - **Facade callers** — `GridStyleEditorFacadeTests` `facade.Save(...)` at ~:51/:64/:104 → `(await facade.Save(...))`; the `SaveThenLoad_DistinctFixture…` guard awaits the async facade `Save` (behavior-preserving — identical bytes, only threading changes).
  - **VM/window SaveCommand tests (verify-only, no edit expected)** — the slice-7 log-sink + `ReportSaveException` tests already `await ExecuteSwallowing(SaveCommand)` / `await SaveCommand.Execute()` under `[AvaloniaFact]`; `GridStyleEditorWindowTests.cs:~43` and `GridStyleEditorWindowOwnerRoutingTests.cs:~135` already `await viewModel.SaveCommand.Execute()`. With `CreateFromTask` these awaits work identically (headless dispatcher pumps the output-scheduler post), so no edit is expected — but **run `GridStyleEditorWindowOwnerRoutingTests` explicitly**: it is the highest async-timing risk in the slice (async `CreateFromTask` execution → the `async void OnSaveCompleted` → `ShowDialog<bool>` modal → manual `Dispatcher.UIThread.RunJobs()`/owned-window timing); confirm the extra async hop before the `SaveCommand` subscription fires doesn't disturb the `RunJobs`/`OwnedWindows` sequence.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## Task 2: Verify + doc

**Files:** `Docs/architecture/grid-style-configuration.md`.

- [x] Confirm `GridStyleEditorWindow.axaml.cs` is untouched and its `OnSaveCompleted(bool)` + `ReportSaveException` fault path behave as before (the async command still yields `bool` and routes exceptions to `ThrownExceptions`). **Doc update is mandatory:** `Docs/architecture/grid-style-configuration.md:~233` states the facade seam as `Save(configDir, GridStyleOptions) → Result` — change it to `Task<Result>` and note `Save` is now async off the UI thread (mirroring `Load`), while `Validate` stays sync (per-keystroke). Note the `File.Move`-stays-sync atomic-write detail where the doc covers the writer.
- [x] full `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format`.

## Post-Completion

**Next slices of #118:** (3) nest `GridStyleOptions` to mirror the DTO groups (+ shared `DepthPalette`), rewiring the two
mappers and the ~10 runtime consumers under slice 1's guards — the big line-count win, and the one slice that ripples
the consumers; (4) type colors as a `StyleColor` value type, fold color-validation into the load mapper, delete
`HexColor`; (5) split the VM into per-group drafts (property-initializer = seed, positional `Build()` = compile guard) +
grouped compiled-binding AXAML. #118 closes after slice 5.

**Executed by exec:**
- branch: grid-style-async-save

## Verify it yourself

The change is behavior-preserving threading — the file written is byte-identical; only *when/where* the write runs moves
off the UI thread. No manual repro (the freeze it removes is imperceptible for a small YAML). Verify by the diff + tests:

1. **The write path is async end to end:** `git show master..HEAD -- SemiStep/SemiStep.Core/Configuration/IGridStyleEditorFacade.cs .../GridStyleEditorFacade.cs .../Loaders/GridStyleWriter.cs` — `Save` returns `Task<Result>`, the facade `await`s `_gridStyleWriter.SaveAsync`, and `WriteAtomicAsync` `await`s `File.WriteAllTextAsync` (the disk write leaves the dispatcher there); `File.Move` stays sync. `Validate` is untouched (still sync, per-keystroke).
2. **The command is async, window unchanged:** `git show master..HEAD -- SemiStep/SemiStep.UI/StyleEditor/GridStyleEditorViewModel.cs` shows `SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, …)`, still `ReactiveCommand<Unit,bool>`; `GridStyleEditorWindow.axaml.cs` is absent from `git diff master...HEAD --name-only`.
3. **Behavior + fault path preserved:** `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~GridStyleEditor|FullyQualifiedName~GridStyleWriter|FullyQualifiedName~GridStyleOrientation"` — the round-trip guard (`SaveThenLoad_DistinctFixture…`, now awaiting the async Save) is green, the atomic-write/`File.Move`-failure test passes, and the save-fault tests (`ThrowingFacade` throw → `ReportSaveException`; `CausedByFailingFacade` → `LogCausedByExceptions`) still fire.
4. **Whole suite:** `dotnet build SemiStep.slnx` (0 warnings) and `dotnet test` (1687 passed, 0 failed).
