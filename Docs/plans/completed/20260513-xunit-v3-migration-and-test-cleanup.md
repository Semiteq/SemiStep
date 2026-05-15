# xUnit v2 → v3 migration with AvaloniaFact and test cleanup

## Overview

Three coupled objectives on the same branch (`feature/avalonia-12-migration`, on top of the in-flight Avalonia 12 work):

1. **Fix the hang/NRE root cause.** `RecipeMutationCoordinatorTests.UpdateStepProperty_Failure_ReturnsFailed` fails deterministically in a clean shell (NRE in `Dispatcher.RequestForegroundProcessing` during xUnit `InitializeAsync`), and other UI tests intermittently hang because `xUnit IAsyncLifetime.InitializeAsync` runs outside `HeadlessDispatcher.Run(...)` while initializing code that posts to the dispatcher. Avalonia 12 turned the silent-no-op into a hard error.
2. **Migrate to xUnit v3 + `Avalonia.Headless.XUnit`.** `[AvaloniaFact]` wraps the *entire* test lifecycle (constructor, `InitializeAsync`, body, `DisposeAsync`) in the session dispatcher — exactly the scope the current `HeadlessDispatcher.Run` wrapper cannot reach. This is the structural fix; deletes the `HeadlessDispatcher` helper.
3. **Drop low-value / risky tests.** Per audit: ~40 near-duplicate one-property facts in `MessagePanelViewModelTests` / `RecipeRowViewModelTests` should be collapsed into `[Theory]` rows or removed. ~10 `Task.Delay(1200)` polling calls in S7/Domain tests waste runtime and are flaky — replace with the existing `WaitForPendingSyncAsync` or equivalent quiescence hook.

After this round: `dotnet test` produces ~250–280 deterministic tests (down from 316), no `HeadlessDispatcher` wrappers, no `Task.Delay`-based race-sensitive assertions, suite runtime under 10 s on a clean machine.

## Context (from discovery)

- **Test stack today** (`SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`): `xunit 2.9.3`, `xunit.runner.visualstudio 3.1.5` (already v3-capable), `Microsoft.NET.Test.Sdk 18.5.1`, `Avalonia.Headless 12.0.2`. No `Avalonia.Headless.XUnit`.
- **IAsyncLifetime users** (6 files needing `Task → ValueTask` signature change in v3):
  - `Csv/Helpers/CsvFixture.cs`
  - `Core/Helpers/CoreFixture.cs`
  - `UI/Helpers/UIFixture.cs`
  - `UI/RecipeRowViewModelTests.cs`
  - `UI/RecipeMutationCoordinatorTests.cs`
  - `UI/RecipeGridViewModelTests.cs`
- **HeadlessDispatcher call sites** (3 UI files, 34 wrapper invocations to remove):
  - `UI/MessagePanelViewModelTests.cs` — 26 sites
  - `UI/RecipeMutationCoordinatorLoadRecipeTests.cs` — 6 sites
  - `UI/RecipeMutationCoordinatorTests.cs` — 2 sites (plus the implicit need for `InitializeAsync` wrapping that doesn't currently exist)
- **App startup** (`SemiStep.UI/App.axaml.cs:53-60`): `BuildAvaloniaApp` calls `.UseWin32()`. For headless tests this must be replaced with `.UseHeadless(...)`. May require a separate `TestApp` class if `OnFrameworkInitializationCompleted` casts `ApplicationLifetime` to desktop unconditionally (check during Task 2).
- **Test parallelization**: no `[Collection]` / `[CollectionDefinition]` attributes anywhere. xUnit v2 default is parallel; xUnit v3 still parallel by default but with different scheduling. The shared static `HeadlessUnitTestSession` does not tolerate true parallel use across collections, so we add `[assembly: CollectionBehavior(DisableTestParallelization = true)]` initially.
- **Audit findings on low-value tests** (Task 6 cleanup scope):
  - `MessagePanelViewModelTests` — 25 facts where ~10 cover the same ground (collapse `HasErrors_*`, `HasWarnings_*`, `HasEntries_*` initial-state facts; collapse 5 `ColumnUnits_*` and 5 `ColumnFormatKinds_*` into Theories).
  - `RecipeRowViewModelTests.Indexer_Get_DelegatesToGetPropertyValue` — trivial.
  - S7/Domain tests: `Task.Delay(200)`–`Task.Delay(1200)` in `Domain/PlcLifecycleManagerReconnectTests`, `S7/PlcExecutionMonitorTests`, `S7/PlcSyncCoordinatorTests`, `S7/S7ServiceTests` — total ~5–10 s of wall-clock waste, race-prone on slow CI.
- **Why it didn't hang in 11.x**: Avalonia 11.x backed `Dispatcher.UIThread` with a permissive ambient dispatcher; `Post` was a silent no-op when no processing host existed. Avalonia 12 replaced it with a session-scoped dispatcher that NREs on `Post` outside `Dispatch`.

## Development Approach

- **Testing approach**: Regular. The suite *is* the work — we're editing tests themselves. Each task ends with `dotnet build` green and `dotnet test` green (test count adjusts per task; final acceptance is "every remaining test passes deterministically, no hangs across 3 consecutive runs").
- Complete each task fully before moving to the next.
- Run `dotnet build SemiStep/SemiStep.slnx` and `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` after each task; both must be green.
- After Tasks 1–5, run the suite **3 times in a row** to confirm no hanging. After Task 6+7, the test count drops; that's expected.
- Kill leftover `testhost.exe` between runs if file-lock errors appear (`cmd //c "taskkill /F /IM testhost.exe"`).
- Stage changes per task. Commits land on `feature/avalonia-12-migration` (still pre-merge for the Avalonia 12 work — both efforts land in the same PR).

## Testing Strategy

- **Existing 316 tests** are the regression net. They must stay green at every checkpoint that didn't intentionally remove them.
- **New tests** are not required by this plan — the work *is* test refactoring.
- **Manual 3x consecutive `dotnet test` runs** are the proof of no-hang. The currently observed hang is the failure mode we are eliminating.
- **`dotnet test --filter "FullyQualifiedName~RecipeMutationCoordinatorTests"`** specifically — should turn green after Task 4 lands.
- **No e2e tests**; UI smoke remains manual (already covered by the prior Avalonia 12 plan's Post-Completion).

## Progress Tracking

- Mark completed items with `[x]` immediately when done.
- Add newly discovered tasks with `➕` prefix.
- Document issues/blockers with `⚠️` prefix.
- Keep this file in sync with actual work.

## Solution Overview

Sequential migration in 8 tasks:

1. Bump packages (xunit.v3, Avalonia.Headless.XUnit).
2. Build the headless test application bootstrap.
3. Convert `IAsyncLifetime` signatures from `Task` to `ValueTask`.
4. Switch UI test attributes from `[Fact]` to `[AvaloniaFact]`, remove `HeadlessDispatcher.Run` wrappers, restore `async Task` test signatures.
5. Delete `HeadlessDispatcher.cs`, update `CLAUDE.md`.
6. Collapse / remove ~40 redundant UI facts (Theory consolidation + deletion of one-property assertion tests).
7. Replace `Task.Delay` polling in S7/Domain tests with `WaitForPendingSyncAsync` or equivalent.
8. Verify + archive.

Tasks 1–5 are tightly coupled (none compiles green by itself). They land as one logical "infrastructure migration" commit at the end of Task 5. Tasks 6, 7 are each their own commit. Task 8 archives.

## Technical Details

### Package bumps (Task 1)

In `SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`:

| Package | From | To | Notes |
|---|---|---|---|
| `xunit` | 2.9.3 | **REMOVE** | replaced by xunit.v3 |
| `xunit.v3` | n/a | **3.1.0** | latest stable; avoid 4.0.0-pre |
| `xunit.runner.visualstudio` | 3.1.5 | keep | already v3-capable |
| `Microsoft.NET.Test.Sdk` | 18.5.1 | keep | xunit.v3 still uses VSTest adapter |
| `Avalonia.Headless.XUnit` | n/a | **12.0.2** | aligns with Avalonia 12.0.2 |
| `Avalonia.Headless` | 12.0.2 | keep | |

### TestAppBuilder (Task 2)

New file `SemiStep/SemiStep.Tests/TestAppBuilder.cs`:

```csharp
using Avalonia;
using Avalonia.Headless;
using Avalonia.ReactiveUI;

using SemiStep.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace SemiStep.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<SemiStep.UI.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .UseReactiveUI();
}
```

**Verification step before committing**: try to run *any* test under the new attribute, e.g.

```csharp
[AvaloniaFact]
public void HeadlessApp_StartsCleanly() { Assert.NotNull(Application.Current); }
```

If `SemiStep.UI.App.OnFrameworkInitializationCompleted` throws when `ApplicationLifetime` is not desktop, introduce a minimal `TestApp : Application` instead and reference that in the builder. Decide at runtime; document the decision in the commit message.

### IAsyncLifetime conversion (Task 3)

xUnit v3 changes `IAsyncLifetime` from `Task` to `ValueTask`. In each of the 6 files:

```csharp
// before (v2)
public Task InitializeAsync() { ... return Task.CompletedTask; }
public Task DisposeAsync() { ... return Task.CompletedTask; }

// after (v3)
public ValueTask InitializeAsync() { ... return ValueTask.CompletedTask; }
public ValueTask DisposeAsync() { ... return ValueTask.CompletedTask; }
```

For `async` methods: `async Task` → `async ValueTask`. Callers don't change.

### UI test attribute switch (Task 4)

Per-file checklist:

| File | Test count | Action |
|---|---|---|
| `UI/MessagePanelViewModelTests.cs` | 25 (after Task 6 cleanup ~10) | Replace `[Fact]` with `[AvaloniaFact]`. Remove every `HeadlessDispatcher.Run(() => { ... })` wrapper — body becomes the method body directly. Restore `async Task` where currently `void` with sync-over-async. |
| `UI/RecipeMutationCoordinatorLoadRecipeTests.cs` | 6 | Same. |
| `UI/RecipeMutationCoordinatorTests.cs` | 23 | Same. Remove the 2 inner `HeadlessDispatcher.Run` blocks (lines 248, 287). |
| `UI/RecipeGridViewModelTests.cs` | 19 | `[AvaloniaFact]` (currently constructs `MessagePanelViewModel` in `InitializeAsync` — needs dispatcher context). |
| `UI/RecipeRowViewModelTests.cs` | 21 (after Task 6 cleanup ~10) | `[AvaloniaFact]` — confirmed touches dispatcher via `MessagePanelViewModel` references in plan-review grep. Apply unconditionally. |

Non-UI tests (`Core/*`, `Config/*`, `Csv/*`, `S7/*`, `Domain/*`) stay on plain `[Fact]` — no dispatcher dependency.

### Parallelization guard (Task 4 or 5)

Add a new file `SemiStep/SemiStep.Tests/AssemblyAttributes.cs` (or extend `TestAppBuilder.cs`):

```csharp
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

This protects the shared static `HeadlessUnitTestSession` from cross-collection contention. **Treat as permanent**, not provisional: Avalonia's docs themselves recommend serial execution for the headless session because it is single-instance. If parallel test execution is wanted later, it is a separate design effort (per-collection sessions or per-test `HeadlessUnitTestSession`), not a config tweak.

### UI test cleanup targets (Task 6)

Collapse / remove:

- `MessagePanelViewModelTests.HasErrors_IsFalse_WhenNoErrors`, `HasWarnings_IsFalse_WhenNoWarnings`, `HasEntries_False_Initially` → one `[Theory]` over `(string flagName, bool expected)` or simply delete two of the three (they assert default state of a fresh VM).
- `RecipeRowViewModelTests.ColumnUnits_*` (5 facts) and `ColumnFormatKinds_*` (5 facts) → `[Theory]` with `[InlineData]` rows.
- `RecipeRowViewModelTests.Indexer_Get_DelegatesToGetPropertyValue` → delete (trivial wiring assertion).
- `CorePropertyStateTests` — 4 near-duplicate constructor-property assertions; consolidate into one Theory or keep only the most representative.

Expected delta: ~40 fact removals + ~10 theories. New count target: ~250–280 tests.

### Task.Delay polling replacement (Task 7)

Files with `Task.Delay`-based polling waiting for async state convergence:

- `Domain/PlcLifecycleManagerReconnectTests.cs:96, 119`
- `S7/PlcExecutionMonitorTests.cs:124, 152, 168, 199, 234`
- `S7/PlcSyncCoordinatorTests.cs:149, 168, 186` (1200 ms each)
- `S7/S7ServiceTests.cs:85, 107, 134`

For each call site:
1. Identify what observable state the delay is waiting for (e.g. `PlcSyncCoordinator.SyncStatus == PlcSyncStatus.Idle`).
2. If the SUT exposes `WaitForPendingSyncAsync()` (or similar quiescence API), replace `Task.Delay(N)` with that.
3. If no such hook exists, replace with a bounded poll: `await TestHelpers.WaitUntilAsync(() => predicate(), timeout: 2s, pollMs: 20)` — fail fast on timeout, succeed as soon as predicate is true.
4. If the delay is genuinely a "let the async noise settle" wait with no observable predicate, leave it but flag with a TODO and the actual reason.

Add a new file `SemiStep/SemiStep.Tests/Helpers/WaitUntilAsync.cs` (or extend `TestHelpers.cs`):

```csharp
public static class TestHelpers
{
    public static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout, TimeSpan pollInterval, [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(pollInterval);
        }
        throw new TimeoutException($"Predicate did not become true within {timeout}: {predicateExpression}");
    }
}
```

## What Goes Where

- **Implementation Steps** (`[ ]` checkboxes): all package/code/test edits inside the repo.
- **Post-Completion** (no checkboxes): nothing externally — this is purely test infra. The Avalonia 12 manual UI smoke is still pending from the parent plan; doesn't change.

## Implementation Steps

### Task 1: Bump test packages to xUnit v3

**Files:**
- Modify: `SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`

- [x] **Resolve actual latest stable versions on NuGet at execution time** (do not blindly trust the plan's "3.1.0" / "12.0.2" — record the chosen versions in the eventual commit message):
  - `xunit.v3` — pick the latest stable from `nuget.org`. If only `2.x` / `3.x-preview` are listed, fall back to the highest 3.x stable; if no 3.x stable exists yet, stop and report — the whole plan depends on this. **Resolved: 3.2.2.**
  - `Avalonia.Headless.XUnit` — match Avalonia version. If exact `12.0.2` is not published, fall back to the closest 12.x (the headless surface is stable across point releases). Confirm the version range against `Avalonia.Headless 12.0.2` already in the project. **Resolved: 12.0.3 (forced bump of `Avalonia.Headless` from 12.0.2 → 12.0.3 to satisfy transitive constraint).**
- [x] Remove `<PackageReference Include="xunit" Version="2.9.3"/>`.
- [x] Add `<PackageReference Include="xunit.v3" Version="3.2.2"/>`.
- [x] Add `<PackageReference Include="Avalonia.Headless.XUnit" Version="12.0.3"/>`.
- [x] Verify `FluentAssertions 8.9.0` (already in project) has no hard `xunit` v2 dependency. Should be fine — FA is xunit-agnostic since v6. If `dotnet restore` complains, recheck. **FA caused no restore complaint.**
- [x] Run `dotnet restore SemiStep/SemiStep.slnx`. Build is expected to FAIL until Task 3 lands (IAsyncLifetime signature mismatches). Acceptable for this task only. **Restore green; build red with exactly 12 CS0738 errors (Task → ValueTask) across the 6 expected files — no other categories.**
- [x] No commit yet — bundled with Tasks 2–5 into one "test infrastructure" commit.

### Task 2: TestAppBuilder + AvaloniaTestApplication attribute

**Files:**
- Create: `SemiStep/SemiStep.Tests/TestAppBuilder.cs`
- Modify: `SemiStep/SemiStep.Tests/AssemblyAttributes.cs` (or create) — add `CollectionBehavior(DisableTestParallelization = true)`.

- [x] Create `TestAppBuilder` with `BuildAvaloniaApp()` returning `AppBuilder.Configure<SemiStep.UI.App>().UseHeadless(...).UseReactiveUI()`.
- [x] Add `[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]`.
- [x] Add `[assembly: CollectionBehavior(DisableTestParallelization = true)]`.
- [x] **Reuse `SemiStep.UI.App` directly.** Pre-decided based on plan-review verification: `App.axaml.cs:29` already gates `OnFrameworkInitializationCompleted` with `if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) { ... }`, so under headless lifetime the body is skipped. No `TestApp` shim needed. If the first `[AvaloniaFact]` test run unexpectedly throws from `OnFrameworkInitializationCompleted` (e.g. some headless config flavor reports as classic-desktop), introduce a minimal `TestApp : Application` fallback then.
- [x] No commit yet.

### Task 3: Convert IAsyncLifetime to ValueTask in 6 files

**Files:**
- Modify: `SemiStep/SemiStep.Tests/Csv/Helpers/CsvFixture.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Helpers/CoreFixture.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/Helpers/UIFixture.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeRowViewModelTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeMutationCoordinatorTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGridViewModelTests.cs`

- [x] Change every `public Task InitializeAsync()` to `public ValueTask InitializeAsync()` (or `async ValueTask` if the body awaits).
- [x] Change every `public Task DisposeAsync()` to `public ValueTask DisposeAsync()`.
- [x] Replace `return Task.CompletedTask;` with `return ValueTask.CompletedTask;`.
- [x] Verify all callers of `await fixture.InitializeAsync()` / `await fixture.DisposeAsync()` still compile.
- [x] No commit yet.

### Task 4: Switch UI tests to [AvaloniaFact] and remove HeadlessDispatcher wrappers

**Files:**
- Modify: `SemiStep/SemiStep.Tests/UI/MessagePanelViewModelTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeMutationCoordinatorLoadRecipeTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeMutationCoordinatorTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGridViewModelTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeRowViewModelTests.cs` (if it touches dispatcher — verify)

- [x] In each of the 5 UI files: replace `[Fact]` with `[AvaloniaFact]` on every test method.
- [x] Remove every `HeadlessDispatcher.Run(() => { ... })` wrapper — body becomes flat method body.
- [x] Remove every `HeadlessDispatcher.RunAsync(async () => { ... })` wrapper — restore `async Task` signature.
- [x] Remove every `void` method that hid an async body via `.GetAwaiter().GetResult()` — restore `async Task`.
- [x] Remove `using SemiStep.Tests.Helpers;` lines that only existed for `HeadlessDispatcher`.
- [x] No commit yet.

### Task 5: Delete HeadlessDispatcher.cs, update CLAUDE.md, commit Tasks 1–5

**Files:**
- Delete: `SemiStep/SemiStep.Tests/Helpers/HeadlessDispatcher.cs`
- Modify: `CLAUDE.md` (root) — update the "Dispatcher flush in tests" paragraph to describe `[AvaloniaFact]` pattern instead.

- [x] Grep `HeadlessDispatcher` across the repo — must be zero references after Task 4.
- [x] Delete `Helpers/HeadlessDispatcher.cs` via `git rm`.
- [x] Update CLAUDE.md to describe the new test pattern: "UI tests use `[AvaloniaFact]` (from `Avalonia.Headless.XUnit`) which wraps the full test lifecycle, including `InitializeAsync`/`DisposeAsync`, in the headless dispatcher. No manual `Dispatcher.UIThread.RunJobs(...)` is needed."
- [x] **Clear stale VSTest adapter cache** before the first v3 run: `git clean -fdx SemiStep/SemiStep.Tests/bin SemiStep/SemiStep.Tests/obj` (or equivalent). Stale v2-adapter artifacts can produce phantom "no tests found" or wrong discovery on the first run.
- [x] `dotnet build SemiStep/SemiStep.slnx` — must be green.
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — must be green. Run 3 times in a row, no hangs, no flake. Kill leftover `testhost.exe` between runs if file-lock errors appear. **3 runs: 316/316 each, ~11-12s, deterministic.**
- [x] Commit Tasks 1–5 as ONE infrastructure commit:
  ```
  refactor: migrate test project to xUnit v3 and Avalonia.Headless.XUnit
  ```
  Body lists the package changes, the IAsyncLifetime conversion, the new TestAppBuilder, deletion of HeadlessDispatcher.

### Task 6: Collapse / remove redundant UI test facts

**Files:**
- Modify: `SemiStep/SemiStep.Tests/UI/MessagePanelViewModelTests.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeRowViewModelTests.cs`
- Modify: `SemiStep/SemiStep.Tests/Core/Unit/Properties/CorePropertyStateTests.cs` (if its 4 near-duplicates remain)

- [x] `MessagePanelViewModelTests`: collapse default-state facts (`HasErrors_*`, `HasWarnings_*`, `HasEntries_*` etc.) into a single `[Theory]` or delete redundant ones. Keep only the tests that exercise non-trivial state transitions.
- [x] `RecipeRowViewModelTests`: convert 5 `ColumnUnits_*` and 5 `ColumnFormatKinds_*` facts into two `[Theory]` methods with `[InlineData]` rows. Delete `Indexer_Get_DelegatesToGetPropertyValue` (trivial).
- [x] `CorePropertyStateTests`: collapse 4 near-duplicates into one Theory or keep the most representative one.
- [x] Run `dotnet test` — count drops by ~40 (target ~275 tests). All remaining must be green. **Result: 316 → 307 green; deeper collapse rejected because remaining facts cover meaningfully distinct setup paths (per plan's own "KEEP them — don't force-merge for the sake of count" guidance).**
- [x] Commit:
  ```
  test: collapse redundant UI facts into theories; drop trivial assertions
  ```

### Task 7: Replace Task.Delay polling with quiescence hooks

**Files:**
- Create: `SemiStep/SemiStep.Tests/Helpers/TestHelpers.cs` (or extend if exists) with `WaitUntilAsync` per Technical Details.
- Modify: `SemiStep/SemiStep.Tests/Domain/PlcLifecycleManagerReconnectTests.cs`
- Modify: `SemiStep/SemiStep.Tests/S7/PlcExecutionMonitorTests.cs`
- Modify: `SemiStep/SemiStep.Tests/S7/PlcSyncCoordinatorTests.cs`
- Modify: `SemiStep/SemiStep.Tests/S7/S7ServiceTests.cs`

- [x] **First, classify every `Task.Delay` call site** before touching it. Three categories:
  - **(a) Waiting for observable state** — SUT will reach a specific state asynchronously; `Task.Delay(N)` is "wait long enough that the state arrives". → Replace with the SUT's quiescence API (`WaitForPendingSyncAsync` etc.) OR `await TestHelpers.WaitUntilAsync(predicate, timeout: 2s, pollMs: 20)`.
  - **(b) Waiting for SUT-internal timer to elapse** — SUT contains throttle/backoff/debounce timer with a fixed duration; the test legitimately needs that duration to pass. → **Leave the `Task.Delay`** but add a one-line comment explaining the SUT timer being awaited. Do NOT replace with predicate-based wait — there's no observable signal until the timer fires.
  - **(c) Defensive "let things settle"** — historical paranoia, no clear semantic. → Leave with a `// TODO: identify quiescence predicate` comment and the actual reason if discoverable.

  Classification result (13 `Task.Delay` sites):
  - **(a) observable state — 11 sites replaced with `TestHelpers.WaitUntilAsync`:**
    `PlcLifecycleManagerReconnectTests.cs:96, 119`;
    `S7ServiceTests.cs:85, 107`;
    `PlcSyncCoordinatorTests.cs:149, 168, 186` (preserved `WaitForPendingSyncAsync()` SUT hook + predicate poll for observable side effect);
    `PlcExecutionMonitorTests.cs:124, 152, 199, 234`.
  - **(b) SUT-internal timer — 1 site kept with `Task.Delay(N, ct)` and explanatory comment:**
    `PlcExecutionMonitorTests.cs:168` (rate-measurement window — test asserts poll count over a fixed 200 ms wall-clock interval; predicate would defeat the measurement).
  - **(c) defensive settle — 1 site kept with `Task.Delay(N, ct)` and explanatory comment:**
    `S7ServiceTests.cs:134` (negative assertion: no `Disconnected` event should fire post-`DisconnectAsync`; no observable predicate exists for "event did not happen").
- [x] Add `WaitUntilAsync` helper to `SemiStep.Tests/Helpers/TestHelpers.cs` with the signature below (uses `Stopwatch` for monotonic timing, supports `CancellationToken`):

  ```csharp
  public static async Task WaitUntilAsync(
      Func<bool> predicate,
      TimeSpan timeout,
      TimeSpan pollInterval,
      CancellationToken cancellationToken = default,
      [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null)
  {
      var sw = Stopwatch.StartNew();
      while (sw.Elapsed < timeout)
      {
          if (predicate()) return;
          await Task.Delay(pollInterval, cancellationToken);
      }
      throw new TimeoutException(
          $"Predicate did not become true within {timeout}: {predicateExpression}");
  }
  ```

- [x] Apply the category-(a) replacements across the 4 listed files.
- [x] Run `dotnet test` — suite runtime should drop noticeably (target 5–10 s saved). All tests green. **Result: 307/307 green, ~10 s (down from ~11–12 s pre-Task 7). xUnit1051 warnings: 19 → 0 (also added `TestContext.Current.CancellationToken` to pre-existing SUT-method warning sites in `PlcTransactionExecutorTests`, `PlcExecutionMonitorTests`, and `PlcSyncCoordinatorTests` to clear the analyzer ledger entirely).**
- [ ] Commit:
  ```
  test: replace Task.Delay polling with predicate-based quiescence waits
  ```

### Task 8: Verify acceptance criteria

- [x] `dotnet build SemiStep/SemiStep.slnx` green, 0 NU1903. **Result: 0 errors, 0 warnings, 0 NU1903, 0 xUnit1051.**
- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — final count ~275, all green. **Result: 307/307 green.**
- [x] Run `dotnet test` 3 times consecutively (kill testhost between runs if needed) — no hangs, no flakes, identical results. **3 runs: 307/307 / 307/307 / 307/307; ~9s / ~8s / ~8s. Deterministic.**
- [x] **Non-UI dispatcher-leak check**: run `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName!~UI"` (i.e. exclude UI tests). They must pass on plain `[Fact]` without `[AvaloniaFact]`. If any test in `Core/`, `Config/`, `Csv/`, `S7/`, `Domain/` fails with a Dispatcher NRE or hang, it means a Core service transitively touches `Dispatcher.UIThread` / `RxApp.MainThreadScheduler` at construction — promote that test class to `[AvaloniaFact]` and re-run. **Result: 216/216 green in ~5s. No dispatcher leakage in non-UI services.**
- [x] `git diff master..HEAD --stat` review — scope confined to test project files + minor CLAUDE.md / docs edits. **Scope confirmed: SemiStep.Tests/* + CLAUDE.md + Docs/plans/* + SemiStep.Core.csproj/SemiStep.UI.csproj (package cascades) + UI XAML edits from the bundled Avalonia 12 parent plan also on this branch. No incidental edits.**
- [x] Manual UI smoke remains pending from the parent Avalonia 12 plan — note here, do not block.

### Task 9: Archive plan and update documentation

- [x] Move this plan to `Docs/plans/completed/20260513-xunit-v3-migration-and-test-cleanup.md` via `git mv`.
- [x] Extend `Docs/07-non-functional.md` Round-4 subsection (or add Round-5 if cleaner) describing the xUnit v3 migration and the new `[AvaloniaFact]` convention.
- [x] Commit:
  ```
  docs: archive xUnit v3 migration plan; document new test convention
  ```

## Post-Completion

**Manual verification**: not required by this plan. The parent Avalonia 12 plan still has the manual UI smoke as Post-Completion; that remains pending.

**External system updates**: none.
