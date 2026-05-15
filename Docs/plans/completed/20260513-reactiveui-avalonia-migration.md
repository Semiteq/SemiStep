# Migrate Avalonia.ReactiveUI 11.3.8 → ReactiveUI.Avalonia 12.0.1

## Overview

Replace the deprecated `Avalonia.ReactiveUI 11.3.8` package with its maintained successor `ReactiveUI.Avalonia 12.0.1`. The two packages share the same author intent (ReactiveUI integration with Avalonia) but differ in package name and namespace; the public type/method API is unchanged at v12.0.1.

Motivation:
- `Avalonia.ReactiveUI` is marked deprecated on NuGet. No further 12.x releases. Future Avalonia patch/minor bumps risk transient incompatibility.
- `ReactiveUI.Avalonia 12.0.1` (released 2026-04-20) is the maintained replacement, taught by the ReactiveUI team, targets Avalonia 12.0.1+. Compatible with our Avalonia 12.0.2.
- Closes a documentation lie in `Docs/07-non-functional.md` Round-4 / Round-5 which currently claims "no 12.x release exists" — wrong, it just lives under a different package name.

## Context (from discovery)

- **Inventory of `using Avalonia.ReactiveUI`** (3 sites):
  - `SemiStep/SemiStep.UI/App.axaml.cs:4`
  - `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs:7`
  - `SemiStep/SemiStep.Tests/TestAppBuilder.cs:3`
- **Inventory of `using ReactiveUI`** (10 sites) — these all come from the `ReactiveUI` package (the core library, separate from the Avalonia integration). No change needed.
- **`ReactiveWindow<TViewModel>` base class:** one usage at `SemiStep.UI/MainWindow/MainWindow.axaml.cs:18`. Type and signature unchanged at v12.0.1.
- **`AppBuilder.UseReactiveUI()`:** two sites (`App.axaml.cs`, `TestAppBuilder.cs`). Extension method unchanged.
- **No XAML uses the `Avalonia.ReactiveUI` XML namespace.** Grep confirmed.
- **No direct Splat usage.** Splat is a transitive dependency only; whatever version the new package pulls in is fine.
- **Branch state:** `feature/avalonia-12-migration` is currently 2 commits ahead of master (Avalonia 12 migration + xUnit v3 + test cleanup, after squash). This plan adds a 3rd commit before the PR opens.

## Development Approach

- **Testing approach:** Regular. The migration is a package + namespace swap with zero API surface change at v12.0.1. The existing 307-test suite is the regression net — every test must stay green.
- Complete each task fully before moving to the next.
- `dotnet build SemiStep/SemiStep.slnx` and `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` after each task; both must be green.
- No new tests required — this is not new behaviour, only a package rename. If the suite stays at 307/307, the migration is verified.
- Stage changes per task; final commit lands on `feature/avalonia-12-migration`.

## Testing Strategy

- **Existing 307 tests** are the regression net. Must stay green at every checkpoint.
- **No new tests** required — no new behaviour.
- **Manual 1x `dotnet test`** is enough; the xUnit v3 migration already proved suite is deterministic, so 1 run + smoke check covers it.
- **Manual UI smoke** still pending from the parent Avalonia 12 plan — doesn't change here; Round-4 notes already cover it.

## Progress Tracking

- Mark completed items with `[x]` immediately when done.
- Add newly discovered tasks with `➕` prefix.
- Document issues/blockers with `⚠️` prefix.
- Keep this file in sync with actual work.

## Solution Overview

Three tasks:

1. **Package swap + namespace edits** — replace `Avalonia.ReactiveUI 11.3.8` with `ReactiveUI.Avalonia 12.0.1` in csproj, update 3 `using` directives. Single mechanical change. Build green, 307/307 tests pass.
2. **Documentation fix** — Update `Docs/07-non-functional.md` Round-4 and Round-5 subsections: replace the "no 12.x release" claim with the migration to `ReactiveUI.Avalonia`. Also update `CLAUDE.md` if it mentions the package by name (verify).
3. **Verify acceptance + archive plan**.

Single atomic commit recommended (per research findings) covering tasks 1+2; task 3 is verify + archive.

## Technical Details

### Package change (Task 1)

`SemiStep/SemiStep.UI/SemiStep.UI.csproj`:

```xml
<!-- before -->
<PackageReference Include="Avalonia.ReactiveUI" Version="11.3.8"/>

<!-- after -->
<PackageReference Include="ReactiveUI.Avalonia" Version="12.0.1"/>
```

### Namespace edits (Task 1)

Three files, one line each:

```csharp
// before
using Avalonia.ReactiveUI;

// after
using ReactiveUI.Avalonia;
```

Files:
- `SemiStep/SemiStep.UI/App.axaml.cs`
- `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs`
- `SemiStep/SemiStep.Tests/TestAppBuilder.cs`

### What stays unchanged

These types live in the `ReactiveUI` namespace (from the core `ReactiveUI` package) and are unaffected:
- `ReactiveObject`, `RaiseAndSetIfChanged`, `RaisePropertyChanged`
- `ReactiveCommand`, `WhenAnyValue`, `WhenActivated`
- `RxApp.MainThreadScheduler`, `RxApp.TaskpoolScheduler`

These types move to the new `ReactiveUI.Avalonia` namespace (from the new package):
- `ReactiveWindow<T>`, `ReactiveUserControl<T>`
- `AppBuilder.UseReactiveUI()` extension

### Docs to update (Task 2)

`Docs/07-non-functional.md` Round-4 subsection currently says:

> Пакет `Avalonia.ReactiveUI` намеренно оставлен на версии `11.3.8` — релиз `12.x` на NuGet ещё не опубликован (latest на nuget.org — `11.3.8`). Бинарная совместимость с Avalonia 12.0.2 подтверждена сборкой и полным прогоном тестов. Обновить при появлении 12.x.

Replace with a retrospective note that this was the state at Round-4 time; Round-5 documented that the package is deprecated; Round-6 (this round) migrated to `ReactiveUI.Avalonia 12.0.1`.

`Docs/07-non-functional.md` Round-5 has a similar claim about Avalonia.ReactiveUI retention — annotate as "(superseded in Round-6)".

Add a Round-6 subsection summarizing the migration.

## What Goes Where

- **Implementation Steps** (`[ ]` checkboxes): csproj + 3 using directives + docs.
- **Post-Completion** (no checkboxes): nothing externally. The parent Avalonia 12 plan's manual UI smoke is still pending; doesn't change.

## Implementation Steps

### Task 1: Swap package and update using directives

**Files:**
- Modify: `SemiStep/SemiStep.UI/SemiStep.UI.csproj`
- Modify: `SemiStep/SemiStep.UI/App.axaml.cs`
- Modify: `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml.cs`
- Modify: `SemiStep/SemiStep.Tests/TestAppBuilder.cs`

- [ ] In `SemiStep.UI.csproj`: replace `<PackageReference Include="Avalonia.ReactiveUI" Version="11.3.8"/>` with `<PackageReference Include="ReactiveUI.Avalonia" Version="12.0.1"/>`.
- [ ] Replace `using Avalonia.ReactiveUI;` with `using ReactiveUI.Avalonia;` in all three files.
- [ ] Run `dotnet restore SemiStep/SemiStep.slnx` — should succeed; transitive dependencies may bump (e.g. Splat). Confirm no unexpected major bumps.
- [ ] `dotnet build SemiStep/SemiStep.slnx` — green, 0 warnings, 0 errors. If any binding fails to compile, surface the error and pause.
- [ ] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 307/307 green. Kill leftover testhost first if needed (`cmd //c "taskkill /F /IM testhost.exe"`).
- [ ] Grep across the repo for any remaining `Avalonia.ReactiveUI` reference — must be zero.

### Task 2: Update documentation

**Files:**
- Modify: `Docs/07-non-functional.md`
- Possibly modify: `CLAUDE.md` (only if it names the package; verify by grep)

- [ ] Edit `Docs/07-non-functional.md` Round-4 subsection: replace the "Avalonia.ReactiveUI намеренно оставлен на 11.3.8 — релиз 12.x не опубликован" paragraph with a retrospective note ("на момент Round-4 пакет оставался на 11.3.8; в Round-6 мигрирован на `ReactiveUI.Avalonia 12.0.1`").
- [ ] Edit `Docs/07-non-functional.md` Round-5 subsection: same retrospective annotation if it duplicates the claim.
- [ ] Add a new Round-6 subsection after Round-5 documenting the migration: package rename `Avalonia.ReactiveUI` → `ReactiveUI.Avalonia`, version `11.3.8 → 12.0.1`, namespace change in 3 files, API surface unchanged.
- [ ] Grep `CLAUDE.md` for `Avalonia.ReactiveUI`; update if present. Likely no mention.
- [ ] No tests changed by this task — docs only.

### Task 3: Verify + commit + archive

**Files:**
- Modify: this plan (Task 3 checkboxes + final state)
- Move: this plan to `Docs/plans/completed/`

- [ ] `dotnet build SemiStep/SemiStep.slnx` green, 0 warnings, 0 NU1903.
- [ ] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` 307/307 green. Single run is enough — xUnit v3 + AvaloniaFact has already proven the suite is deterministic.
- [ ] `git diff master..HEAD --stat` — confirm scope: 4 code files (csproj + 3 using directives) + 2 docs files + this plan. No incidental changes.
- [ ] Commit ALL Tasks 1+2+3 changes as ONE atomic commit:
  ```
  refactor: migrate from deprecated Avalonia.ReactiveUI to ReactiveUI.Avalonia 12.0.1
  ```
  Body: explain that the package is renamed (same authors, same API surface at v12.0.1), namespace `Avalonia.ReactiveUI` → `ReactiveUI.Avalonia`, only 3 using directives touched, build/tests unchanged at 307/307.
- [ ] `git mv Docs/plans/20260513-reactiveui-avalonia-migration.md Docs/plans/completed/`.
- [ ] Amend the commit to include the plan move (or do as a follow-up `chore: archive plan` commit if cleaner).

## Post-Completion

**Manual verification (optional):** Launch the app once to confirm `WhenActivated` and `ReactiveWindow<MainWindowViewModel>` lifecycle work as before. If the previous Avalonia 12 manual smoke is still pending from the parent plan, fold this into the same session.

**External system updates:** None. Pure dependency rename; no deployment changes.
