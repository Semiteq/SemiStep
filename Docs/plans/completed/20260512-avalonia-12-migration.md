# Avalonia 11 → 12 migration

## Overview

Upgrade SemiStep from Avalonia `11.3.13` to `12.0.2` (current latest stable), plus the rest of the Dependabot batch that was closed automatically when PR #14 merged (`#16`). Reasons:

- Pulls in patched `Tmds.DBus.Protocol` 0.21.3 transitively, closing `NU1903`.
- Aligns SemiStep with the current Avalonia release line (faster releases promised for 12.x going forward).
- Picks up the `IClipboard.GetTextAsync()` deprecation fix — currently a `CS0618` warning in `ClipboardViewModel.cs:110`.

Three real breaking-change vectors based on prior research (verified by quick grep):

1. **Clipboard API**: `IClipboard.GetTextAsync()` is gone in 12. Replaced by `IClipboard.TryGetTextAsync()` (returns nullable string). Our only call site is `SemiStep.UI/Clipboard/ClipboardViewModel.cs:110`. `ClipboardSerializer.cs` (Core) does NOT touch the Avalonia clipboard API — it's pure TSV serialization, no migration needed there. Narrower than originally feared.
2. **`Avalonia.Diagnostics` package removed**: must be swapped for `AvaloniaUI.DiagnosticsSupport`. Note: `App.axaml.cs` does NOT call `AttachDevTools()` today (grep confirmed), so only the csproj reference needs to change. If we want to keep dev tools on `F12` we add `AttachDeveloperTools()` ourselves; otherwise just removing the package is also valid.
3. **Compiled bindings on by default**: 11 `.axaml` files in `SemiStep.UI/`. Every binding must resolve at XAML compile time. Loose bindings (no `x:DataType`, wrong path, etc.) will fail the build. Audit is "fix until green", count unknown until first build.

The other Avalonia 12 breaking changes do NOT affect SemiStep (verified by grep):
- No `IBinding` / `InstancedBinding` usages in production code.
- No `GotFocusEventArgs` / `RoutedEventArgs` for `LostFocus` handlers.
- No `Direct2D1` references.
- We don't use `Dispatcher.InvokeAsync` — only `Dispatcher.UIThread.Post` and `Dispatcher.UIThread.RunJobs`, both unchanged.

## Context (from discovery)

- **Branch:** `feature/avalonia-12-migration`, branched off `master` after PR #14 (architecture cleanup) and PR #15 (release-action v3) merged.
- **Current state:** 316/316 tests green; `Avalonia 11.3.13` everywhere; `Avalonia.Diagnostics 11.3.13` referenced but never invoked.
- **Clipboard usage** is localised to one file: `SemiStep.UI/Clipboard/ClipboardViewModel.cs` (private `IClipboard? _clipboard;`, `SetClipboard(IClipboard?)`, `var csvText = await _clipboard.GetTextAsync();`).
- **11 `.axaml` files** under `SemiStep.UI/`: scope of compiled-binding audit.
- **No Direct2D, no IBinding, no GotFocusEventArgs** in this codebase.
- **No e2e tests**; UI smoke is manual.

## Development Approach

- **Testing approach:** Regular. Implementation is mostly mechanical (package bumps, API swap, XAML attribute audit). Existing 316 tests are the regression net for non-UI behaviour; XAML compile errors surface UI binding issues at build time.
- Complete each task fully before moving to the next.
- Run `dotnet build SemiStep/SemiStep.slnx` and `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` after each task; both must be green.
- Stage changes per task. New commits land on `feature/avalonia-12-migration`.

## Testing Strategy

- **Unit / integration tests**: existing 316 tests cover Core. They must stay green. The Avalonia migration touches UI mainly — most tests won't even rebuild against the new Avalonia.
- **XAML compile**: `dotnet build` is the test for binding correctness. If the build is green after Task 3, compiled bindings are happy.
- **Manual UI smoke** (Post-Completion): launch the app, open a recipe, exercise clipboard copy/paste, verify grid renders, verify dialogs. Avalonia 12 changes have visible effects only at runtime; the suite cannot catch them.
- **New tests**: not required by this plan — none of the changes introduce new behaviour, only swap APIs. Existing `ClipboardSerializerTests` (if any) keep working because `ClipboardSerializer` does not touch Avalonia.

## Progress Tracking

- Mark completed items with `[x]` immediately when done.
- Add newly discovered tasks with `➕` prefix (likely candidate: stray XAML binding fixes discovered during Task 3).
- Document issues/blockers with `⚠️` prefix.
- Keep this file in sync with actual work.

## Solution Overview

Sequential migration:

1. Bump all packages in one csproj change (Avalonia family to 12.0.2, DataGrid to 12.0.0, replace Avalonia.Diagnostics with AvaloniaUI.DiagnosticsSupport, plus the seven safe NuGet bumps).
2. Fix the clipboard call site.
3. Iterate on XAML compile errors until `dotnet build SemiStep/SemiStep.slnx` is green.
4. Run tests and manual smoke.
5. Archive plan and document the upgrade.

After each task the build MUST be green before moving on. Task 1 is the only one allowed to leave the build in a broken state — the package bump itself doesn't compile until Tasks 2 and 3 land. We accept this exception because the alternative (bump packages piecemeal) is impossible — Avalonia 12 packages don't co-exist with 11.x.

## Technical Details

### Package bumps (Task 1)

In `SemiStep/SemiStep.UI/SemiStep.UI.csproj`:

| Package | From | To | Notes |
|---|---|---|---|
| Avalonia | 11.3.13 | 12.0.2 | core |
| Avalonia.Controls.DataGrid | 11.3.13 | 12.0.0 | DataGrid is independent version line |
| Avalonia.Desktop | 11.3.13 | 12.0.2 | |
| Avalonia.Themes.Fluent | 11.3.13 | 12.0.2 | |
| Avalonia.Win32 | 11.3.13 | 12.0.2 | |
| Avalonia.Diagnostics | 11.3.13 | **REMOVE** | replaced by AvaloniaUI.DiagnosticsSupport |
| AvaloniaUI.DiagnosticsSupport | n/a | **ADD** (latest) | only if we keep dev tools — decide during task |

In `SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` (or wherever it lives — verify with grep):

| Package | From | To |
|---|---|---|
| Microsoft.NET.Test.Sdk | 18.3.0 | 18.5.1 |

In whichever csproj currently references them (likely SemiStep.UI or Core):

| Package | From | To |
|---|---|---|
| YamlDotNet | 16.3.0 | 17.1.0 |
| System.Reactive | 6.0.1 | 6.1.0 |
| Microsoft.Extensions.DependencyInjection | 10.0.5 | 10.0.7 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.5 | 10.0.7 |
| System.IO.Hashing | 10.0.5 | 10.0.7 |

**Decision point during Task 1:** keep DiagnosticsSupport or drop entirely. SemiStep is a Windows production app; F12 dev tools are a debug convenience. If never used, the simplest choice is to remove `Avalonia.Diagnostics` and not add the replacement. Decide based on whether anyone on the team relies on it.

### Clipboard fix (Task 2)

Current (`SemiStep.UI/Clipboard/ClipboardViewModel.cs:110`):

```csharp
var csvText = await _clipboard.GetTextAsync();
```

Avalonia 12 equivalent:

```csharp
var csvText = await _clipboard.TryGetTextAsync();
```

`TryGetTextAsync()` returns `Task<string?>` (nullable). The current code likely handles null already (`csvText` is checked downstream). Verify the existing null-handling still applies; if `GetTextAsync` returned empty string for "no text", confirm `TryGetTextAsync` does the same (or returns null — whichever, the caller must handle).

### XAML compiled-binding audit (Task 3)

11 `.axaml` files under `SemiStep.UI/`. The build will surface bindings that the XAML compiler cannot resolve. Typical fixes:

- Add `x:DataType="vm:SomeViewModel"` on the root or container where the DataContext type is known.
- Replace `{Binding SomeProperty}` with explicit paths if compiled binding fails to infer.
- Use `{CompiledBinding ...}` markup explicitly only where Avalonia's default heuristic fails.
- Add `x:CompileBindings="False"` as a targeted escape hatch on specific containers if a binding fundamentally cannot be resolved at compile time (e.g. dynamic DataContext, generic Controls). Use sparingly — defeats the new default.

Iteration loop: build, read error, fix, rebuild. Continue until `dotnet build SemiStep/SemiStep.slnx` is green.

## What Goes Where

- **Implementation Steps** (`[ ]` checkboxes): all package/code/XAML changes inside the repo.
- **Post-Completion** (no checkboxes): manual UI smoke (cannot be automated), and a note for the project owner about Avalonia 12's diagnostics-package change in case they want F12 dev tools back.

## Implementation Steps

### Task 1: Bump packages to Avalonia 12 + dependent versions

**Files:**
- Modify: `SemiStep/SemiStep.UI/SemiStep.UI.csproj`
- Modify: `SemiStep/SemiStep.Core/SemiStep.Core.csproj`
- Modify: `SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`
- Modify: `SemiStep/Directory.Build.props` (if relevant — verify)

- [ ] Grep all `*.csproj` for `Avalonia` references; update each to 12.0.2 (or 12.0.0 for DataGrid).
- [ ] Remove `Avalonia.Diagnostics` from SemiStep.UI.csproj. Decide whether to add `AvaloniaUI.DiagnosticsSupport` (latest version) — default: do NOT add it unless we know someone uses F12 dev tools. Document the decision in the commit message.
- [ ] Bump `YamlDotNet` 16.3.0 → 17.1.0 wherever referenced.
- [ ] Bump `System.Reactive` 6.0.1 → 6.1.0 wherever referenced.
- [ ] Bump `Microsoft.NET.Test.Sdk` 18.3.0 → 18.5.1 in SemiStep.Tests.csproj.
- [ ] Bump `Microsoft.Extensions.DependencyInjection` 10.0.5 → 10.0.7 wherever referenced.
- [ ] Bump `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.5 → 10.0.7 wherever referenced.
- [ ] Bump `System.IO.Hashing` 10.0.5 → 10.0.7 wherever referenced.
- [ ] `dotnet restore SemiStep/SemiStep.slnx`. Build is expected to FAIL at this point — clipboard and XAML errors land in Tasks 2 and 3. **Acceptable for this task only.** Note the failure list and confirm it matches the expected categories (CS0117 / CS0103 for `GetTextAsync`, XAML compile errors). Anything else is unexpected and gets flagged.
- [ ] **No commit yet** — Task 1 is incomplete by itself (build is red). It will be combined with Tasks 2 and 3 into a single migration commit at the end of Task 3.

### Task 2: Fix Clipboard API call

**Files:**
- Modify: `SemiStep/SemiStep.UI/Clipboard/ClipboardViewModel.cs`

- [ ] Replace `await _clipboard.GetTextAsync()` with `await _clipboard.TryGetTextAsync()` at line ~110.
- [ ] Verify null/empty handling at the call site still makes sense (`TryGetTextAsync` returns `Task<string?>`; if caller treated empty string as "no clipboard data", make sure null is handled the same way — adjust if needed).
- [ ] Grep `SemiStep/SemiStep.UI` for any other `GetTextAsync`, `SetTextAsync`, `IDataObject`, `DataObject` references that the new API doesn't support. Fix or flag.
- [ ] `dotnet build SemiStep/SemiStep.slnx` — clipboard CS errors should be gone; XAML errors remain (handled in Task 3).
- [ ] **No commit yet** — bundled into Task 3 commit.

### Task 3: XAML compiled-binding audit and fix

**Files:**
- Modify: every `.axaml` under `SemiStep/SemiStep.UI/` that produces compile errors after Tasks 1 + 2.

- [ ] `dotnet build SemiStep/SemiStep.slnx` — capture the full list of XAML compile errors.
- [ ] For each error, apply the smallest fix: usually `x:DataType=...` on the root, occasionally explicit `{CompiledBinding ...}`, rarely `x:CompileBindings="False"` on a container.
- [ ] Rebuild after each fix until `dotnet build` is fully green.
- [ ] List in the plan body all `.axaml` files touched and the kind of fix applied (DataType / explicit binding / escape hatch). If escape hatch was used, justify briefly.
- [ ] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — must stay 316/316 green. (Tests don't load XAML but DI graph composition could break if Avalonia bumps broke ReactiveUI/Avalonia integration.)
- [ ] Commit ONE migration commit bundling Tasks 1 + 2 + 3:
  pwsh -NoProfile -File C:\Users\admin\.claude\skills\exec\scripts\stage-and-commit.ps1 "refactor: migrate to Avalonia 12.0.2 and bump related packages" <files>

### Task 4: Verify acceptance criteria

- [ ] `dotnet build SemiStep/SemiStep.slnx` green, 0 errors, 0 NU1903 (Tmds.DBus warning gone — confirm via build log).
- [ ] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` 316/316 green.
- [ ] `git diff master...HEAD --stat` review — scope confined to csproj files + ClipboardViewModel.cs + .axaml files + plan files. No incidental edits.
- [ ] Manual UI smoke (will be marked `[x] manual UI smoke (skipped - not automatable)` if executed in autonomous mode): launch app, open a recipe, copy-paste cells via clipboard, verify grid renders, verify dialogs (PLC conflict / exit confirmation) open and close correctly, verify F12 behaviour matches the diagnostics decision from Task 1.

### Task 5: Archive plan and update documentation

- [ ] Move this plan to `Docs/plans/completed/20260512-avalonia-12-migration.md` via `git mv`.
- [ ] Update `Docs/07-non-functional.md` with a brief Round-4 subsection: Avalonia 11 → 12 migration, what was clipboard / dev-tools-related, NU1903 closed.
- [ ] Update `CLAUDE.md` only if a new convention emerged (none expected — this is a dependency upgrade, not a convention change).
- [ ] Commit:
  pwsh -NoProfile -File C:\Users\admin\.claude\skills\exec\scripts\stage-and-commit.ps1 "docs: archive avalonia 12 migration plan" <files>

## Post-Completion

**Manual verification (REQUIRED before merge):**
- Launch `dotnet run --project SemiStep/SemiStep.UI/SemiStep.UI.csproj` against a real config. Confirm:
  - Recipe grid renders with all columns and styles intact.
  - Clipboard copy from the grid into a system text editor produces correct TSV.
  - Clipboard paste from a system text editor into the grid works (this exercises the `TryGetTextAsync` change directly).
  - PLC conflict dialog opens when triggered (subscribers + Dispatcher behaviour).
  - Exit confirmation dialog opens and closes cleanly.
  - Theme (FluentTheme) renders correctly — no visual regressions.
  - F12 dev tools behaviour matches the Task 1 decision (either opens via `AttachDeveloperTools()` or is absent if we removed the package without replacement).

**External system updates:**
- None. This is a code-only dependency upgrade; deployed configs are unaffected.
- Inform the team that `Avalonia.Diagnostics` package is gone — replaced by `AvaloniaUI.DiagnosticsSupport` (only if we kept dev tools) or removed entirely (default decision).
