# Slice 6a — Transient-fault localizing seam + typed PLC connection-lost

## Overview

The panel has localizing seams for a Result's errors (`ReportFailure(IResultBase)`) and for a Result's warnings
(`ReportWarnings(IResultBase)`, slice 5a), plus the snapshot-validity channel (`RefreshReasons`). It has **no seam for a
single transient `IError`**. The PLC fault channel carries a fully-typed `IError` end to end
(`PlcSyncCoordinator.EmitFault` → `Faults` observable → `RecipeCoordinator.OnPlcFault`), but `OnPlcFault` flattens it to
`.Message` into the raw `ReportError(string)` sink — so the PLC fault renders raw English regardless of culture, and the
one fault it carries is an untyped `new Error("PLC connection lost")` anyway.

This slice builds the error-side twin of slice 5a's warning seam and proves it on the connection-lost fault:

1. **`ReportFailure(this MessagePanelViewModel, IError error)`** — a new localizing overload mirroring
   `ReportFailure(IResultBase)`: `panel.ReportError(ReasonLocalizer.Localize(error))`. (`IError` is not `IResultBase`,
   so this is an unambiguous overload of the existing method, not a new name.)
2. **`ConnectionLostError`** (typed) replaces `PlcSyncCoordinator`'s `new Error("PLC connection lost")`.
3. `RecipeCoordinator.OnPlcFault` routes through the new seam (`_messagePanel.ReportFailure(error)`), keeping the
   English `_logger.LogWarning("PLC fault: {Message}", error.Message)` (log-English invariant).

It is the small mechanism cut of slice 6 (PLC), before the bulk PLC error typing + un-laundering (6b) and the #120
cancellation plumbing (6c). The transient-error seam is the roadmap's missing error-side seam — the single-`IError`
localizing path that did not exist.

**Behavior-preserving for English.** `ConnectionLostError`'s English base message equals today's exact string, and the
resx en value equals it byte-for-byte. Under `ru` the PLC fault now reads Russian. The seam localizes any `IError`: an
untyped inner still falls through to `.Message` (English), so nothing else regresses.

**Scope guard:** only the connection-lost fault + the seam. The other PLC faults (`PlcSyncExecutor`'s recipe-active /
laundered inners), the make-public of `NotConnectedError`/`ProtocolVersionMismatchError`, `PlcCommandFailedError`, and
the `CausedBy(ex)` diagnostics are **6b**. The `IS7Reader` cancellation threading is **6c**. `RefreshReasons` and the
existing `ReportFailure(IResultBase)`/`ReportWarnings` seams are untouched.

## Acceptance

1. `ReportFailure(this MessagePanelViewModel, IError error)` localizes a single error via `ReasonLocalizer.Localize` and
   reports it to the transient operation slot — the single-`IError` twin of `ReportFailure(IResultBase)`.
2. `ConnectionLostError` (public, PLC) carries an English base message identical to the pre-slice string;
   `PlcSyncCoordinator.HandleConnectionLost` raises it instead of `new Error("PLC connection lost")`.
3. `RecipeCoordinator.OnPlcFault` routes through `ReportFailure(error)`; the connection-lost fault renders Russian under `ru`.
4. `ReasonLocalizer` localizes `ConnectionLostError`; en unchanged, ru Russian. resx parity + coverage test green.
5. `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## Task 1: Type the fault + build the seam + route OnPlcFault

**Files:**
- Create: `SemiStep/SemiStep.Core/Plc/Sync/ConnectionLostError.cs` (public sealed, `Error` base, BOM; `OwnedByAnotherInstanceError.cs` as the typed-PLC-error precedent — this one is fieldless).
- Modify: `SemiStep/SemiStep.Core/Plc/Sync/PlcSyncCoordinator.cs` (~166 the `EmitFault(new Error("PLC connection lost"))`).
- Modify: `SemiStep/SemiStep.UI/MessageService/ResultReportingExtensions.cs` — add the `ReportFailure(IError)` overload.
- Modify: `SemiStep/SemiStep.UI/Coordinator/RecipeCoordinator.cs` (`OnPlcFault`, ~530).
- Modify: `SemiStep/SemiStep.UI/Localization/ReasonLocalizer.cs` (arm + using), `Resources.resx`, `Resources.ru.resx`, `Resources.Designer.cs` (1 key), `SemiStep/SemiStep.Tests/UI/Localization/CoreErrorLocalizationCoverageTests.cs` (1 sample).
- Modify (test fix): `SemiStep/SemiStep.Tests/UI/RecipeCoordinatorTests.cs` (~377 `PlcFault_RoutesToMessagePanelOperationChannel`).

- [x] `public sealed class ConnectionLostError() : Error("PLC connection lost")` (fieldless — mirror the leaf-error precedent; primary ctor). Namespace `SemiStep.Core.Plc.Sync` (alongside `PlcSyncCoordinator`). `using FluentResults;` for the `Error` base. English base message byte-identical. BOM. (The coverage-test sample also needs `using SemiStep.Core.Plc.Sync;`.)
- [x] `PlcSyncCoordinator.cs`: `EmitFault(new Error("PLC connection lost"))` → `EmitFault(new ConnectionLostError())` (same namespace, no using needed; KEEP `using FluentResults;` — the file uses `IError`/`Subject<IError>` throughout).
- [x] Pin the type at the source: strengthen `PlcSyncCoordinatorTests.HandleConnectionLost_EmitsExactlyOneFault` (~:241) to also assert `faults[0].Should().BeOfType<ConnectionLostError>()` (currently asserts only `.Message` contains "connection", which survives byte-identical) — so a regression away from the typed fault fails at the emit source. Add the `using SemiStep.Core.Plc.Sync;`.
- [x] `ResultReportingExtensions.cs`: add `public static void ReportFailure(this MessagePanelViewModel panel, IError error) => panel.ReportError(ReasonLocalizer.Localize(error));` (or a block body). Overload of the existing `ReportFailure(IResultBase, string?)`. `IError` is `FluentResults` — the using is already present.
- [x] `RecipeCoordinator.OnPlcFault`: `_messagePanel.ReportError(error.Message)` → `_messagePanel.ReportFailure(error)`. KEEP the `_logger.LogWarning("PLC fault: {Message}", error.Message)` line (English log). Confirm `SemiStep.UI.MessageService` using is present (ReportFailure is already used elsewhere in this file).
- [x] `ReasonLocalizer`: arm `ConnectionLostError => Resources.ErrorConnectionLost` (bare, no fields) + `using SemiStep.Core.Plc.Sync;`.
- [x] resx: `ErrorConnectionLost` en `PLC connection lost` (== baked) + ru — match the existing PLC terminology in the ru resx (check how `ErrorOwnedByAnotherInstance` renders "PLC"; use the same term — likely `ПЛК`, e.g. `Соединение с ПЛК потеряно`) + hand-written Designer accessor. Coverage: 1 sample. New error file BOM; Designer/resx BOM as-is.
- [x] **Fix the routing test.** `RecipeCoordinatorTests.cs` `PlcFault_RoutesToMessagePanelOperationChannel` (~377) pushes `new Error("PLC connection lost")` and asserts `.Message.Should().Be("PLC connection lost")`. Update `PushFault(new ConnectionLostError())` so it matches production. Its real contract is "a PLC fault routes to the operation channel as one Error-severity entry" — keep `ContainSingle` + `Severity == Error` + `ErrorCount == 0`. For the text, **prefer** wrapping in `ResourcesCultureScope.Use("en")` and keeping `.Be("PLC connection lost")` — that keeps the exact-text guard on the byte-identical English contract (dropping the assert loses a guard for free). Add the usings `SemiStep.Core.Plc.Sync;` and `SemiStep.Tests.UI.Localization;`.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## Task 2: Cover the seam + doc + verify

**Files:** `ReasonLocalizerTests.cs`, a MessagePanel/report test home, `Docs/architecture/error-reporting.md`.

- [x] `ReasonLocalizerTests`: ru render case `new ConnectionLostError()` → the ru string (under `ResourcesCultureScope.Use("ru")`) + an en `Localize(sample).Should().Be(sample.Message)` pin (under its OWN `ResourcesCultureScope.Use("en")` — with `Resources.Culture` null the bare arm returns the ru satellite on this machine).
- [x] **Seam unit cases** in `SemiStep/SemiStep.Tests/UI/ResultReportingExtensionsTests.cs` (the home of the existing `ReportFailure(result)` cases): `panel.ReportFailure(new ConnectionLostError())` under `ResourcesCultureScope.Use("ru")` puts the Russian text in the transient operation slot (Severity=Error); an untyped-fallthrough case `panel.ReportFailure((IError)new Error("x"))` reports `"x"` (an untyped error localizes to its `.Message`). If a panel-level end-to-end fits better, `MessagePanelReportingTests.cs` is the panel-transient home.
- [x] fragment sweep: grep the Tests tree for `PLC connection lost` — confirm only the now-updated `RecipeCoordinatorTests` fault test references it, and it is culture-proof.
- [x] `Docs/architecture/error-reporting.md`: document the transient single-error seam — `ReportFailure(IError)` is the single-error twin of `ReportFailure(IResultBase)`/`ReportWarnings`, localizing one transient error into the operation slot; note `OnPlcFault` now uses it and `ConnectionLostError` is the first typed PLC fault.
- [x] full `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format`.

## Post-Completion

**Next (slice 6b):** the bulk PLC error-pipe work — make `NotConnectedError`/`ProtocolVersionMismatchError` public
(promote `Expected`/`Actual` to properties for the localizer) + arms + resx; new `RecipeActiveError` (`PlcSyncExecutor`'s
recipe-active fault) and `PlcCommandFailedError` envelope; **un-launder** `PlcSyncExecutor:205/212/235` (forward the
typed inner `NotConnectedError` instead of re-wrapping `.Message`); and #120 part 2 — the `PlcTransactionExecutor`
`Result.Fail(ex.Message)` catches (105/191/232/267/301) + `PlcLifecycleManager:142` become `PlcCommandFailedError(...)
.CausedBy(ex)` so the exception type+stack reach the log. Then **6c** (#120 part 1) — thread a `CancellationToken`
through `IS7Reader` → `S7Service` → `PlcTransactionExecutor`, dropping the manual `IsCancellationRequested` polling in
`PlcLifecycleManager.PerformReconnectReconciliationAsync`. After 6b/6c the PLC surface localizes and the config-load
boundary is the last English-by-design surface; slice 7 (style-editor) closes the panel-seam gaps.

---

**Executed by exec:**
- branch: plc-fault-seam
- commits: d3a86db (ConnectionLostError + ReportFailure(IError) seam + route OnPlcFault + arm/resx + test fixes) · 0b1bd01 (seam/render tests + doc) · 9d2af24 (review-1: doc overload qualifier) · c99814c (smells: leaf-error brace style)
- review chain: comprehensive (5 agents, all OUTCOME ACHIEVED) → fixer 9d2af24 (INFO doc) → smells → fixer c99814c (MINOR brace style) → comment audit (Ship) → critical (satisfied by comprehensive; doc/cosmetic delta since). codex skipped (not installed).

## Verify it yourself
1. `dotnet build SemiStep.slnx` — 0 warnings.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — 1643 passed, 0 failed.
3. The fault seam localizes: `--filter "FullyQualifiedName~ResultReportingExtensions|FullyQualifiedName~ReasonLocalizer|FullyQualifiedName~CoreErrorLocalizationCoverage"` — `ReportFailure(new ConnectionLostError())` under ru puts `Соединение с ПЛК потеряно` in the transient operation slot (Severity=Error); an untyped `IError` falls through to its `.Message`; coverage forces the case.
4. Routed + pinned at the source: `--filter "FullyQualifiedName~PlcSyncCoordinator|FullyQualifiedName~RecipeCoordinatorTests"` — `HandleConnectionLost` emits a `ConnectionLostError` (BeOfType pin); `PlcFault_RoutesToMessagePanelOperationChannel` shows it as one Error entry (en-scoped: "PLC connection lost"). Pre-slice the fault flattened to `ReportError(string)` with no localization.
5. English preserved: `ConnectionLostError.Message` == resx en byte-identical; `OnPlcFault` keeps the English `_logger.LogWarning("PLC fault: {Message}", ...)`.
6. Manual (optional): under a Russian UI, drop the PLC connection — the fault shows in Russian in the operation channel.
