# Slice 6b — Type the PLC error surface + un-launder + #120 CausedBy

## Overview

Slice 6a added the transient single-`IError` fault seam (`ReportFailure(IError)`) and typed the connection-lost fault,
so `RecipeCoordinator.OnPlcFault` now localizes any typed fault. But the rest of the PLC producer surface is still
English: `NotConnectedError`/`ProtocolVersionMismatchError` are `internal` (invisible to the localizer), the
recipe-active / write-verification / not-committed faults are free-text `new Error(...)` / `Result.Fail(string)`, and
`PlcSyncExecutor` **launders** typed inners — it re-wraps a typed inner's `.Message` into a fresh untyped `Error` before
emitting the fault, discarding the type so the 6a seam can't localize it. On the diagnostics side (#120 part 2),
`PlcTransactionExecutor`'s five `catch { return Result.Fail(ex.Message); }` sites keep only the message string — the
exception type and stack are gone before any log sink, and no code walks `Result.Reasons`.

This slice types the PLC producer surface, un-launders the fault forwarding, and lands the exception in a log:

1. Make `NotConnectedError` (fieldless) and `ProtocolVersionMismatchError` (with `Expected`/`Actual` props) **public**.
   New leaves `RecipeActiveError`, `WriteVerificationFailedError(attempts)`, `RecipeNotCommittedError`, and the Rule-B
   envelope `PlcCommandFailedError`. Each with arm + resx + coverage sample.
2. **Un-launder `PlcSyncExecutor`**: `reportFault(new Error(inner.Message))` / `Result.Fail(inner.Message)` at 205/212/235
   → forward the typed inner directly, so it localizes through the 6a seam. Type the recipe-active fault (218/221) and
   the exception-stringify (155).
3. **#120 part 2** — the five `PlcTransactionExecutor` catches become `new PlcCommandFailedError().CausedBy(ex)` AND
   **log the exception at the catch**: writes at `LogError(ex, …)`, reads at `LogWarning(ex, …)`. The `CausedBy(ex)`
   preserves the type in the result chain; the `Log(ex, …)` line is the diagnostics sink (message + stack). Because the
   read catch now owns a Warning-with-exception, the `PlcExecutionMonitor:120` generic poll-error Warning
   (`"…: {Message}"`, which after typing would log the useless generic `"PLC command failed"` and double-log the same
   failure) is **removed** — the `ReadAndDecodeAsync:232` catch it fronts already Warns with the real exception.

**6 typed classes** (2 make-public, 4 new). After this the PLC **operator-facing** faults localize by type; 6c is the
#120 cancellation plumbing (orthogonal).

**Scope decisions:**
- `NotConnectedError` becomes fieldless (`public sealed class NotConnectedError() : Error("Not connected to PLC")`) —
  verified: its 4 call sites in `PlcTransactionExecutor` (63/113/171/222) all pass exactly `"Not connected to PLC"`, so
  the `(string message)` ctor is needless; update the 4 callers to `new NotConnectedError()`. (A typed error's message
  must be fixed for the resx to own it.)
- The un-laundering is byte-identical for the disconnected case: today `faultMessage = "Not connected to PLC"` when
  `isDisconnected`, which is exactly `NotConnectedError.Message` — so `reportFault(activeResult.Errors[0])` (forward the
  typed inner) preserves the message AND recovers the type. Keep the `isDisconnected` bool via
  `activeResult.Errors.OfType<NotConnectedError>().Any()` (free, multi-error-safe) for its `LogWarning` branch.
- `PlcCommandFailedError` is a **fieldless** Rule-B envelope (`"PLC command failed"`): the raw exception message (a
  socket/timeout string) is not operator-friendly and rides `CausedBy(ex)` + the new `Log(ex,…)` to the log; the
  localized headline is the operator-useful part. Same shape as 5b's `RecipeLoadFailedError`.
- `PlcSyncExecutor:194` `Result.Fail("Not connected")` and `:221` `Result.Fail("Recipe active")` are INTERNAL return
  values (not faults reaching the panel — `PlcSyncCoordinatorTests` confirms `CheckCanSync` failure emits its fault via
  `reportFault`, not the return). Type them too for consistency (`NotConnectedError` / `RecipeActiveError`); the message
  changes are internal-only.
- **Deferred, stated in the doc, NOT typed here:** `PlcTransactionExecutor:183` (short protocol-version read,
  `"…returned {n} bytes, expected {m}"`) is a malformed-wire diagnostic edge, not an operator-actionable fault — stays
  English by design. `PlcLifecycleManager:141` (enable catch) is a connect/cancellation concern — a test routes
  `TaskCanceledException` through it and the catch has no `OperationCanceledException` guard — so it belongs with 6c's
  cancellation pass, not here. #120 part 1 (cancellation threading) is 6c.

**Behavior for English:** the make-public and new-leaf messages keep byte-identical English base strings (resx en == the
current baked string). The one deliberate change is `PlcCommandFailedError`: the raw `ex.Message` is replaced by the
"PLC command failed" headline in the user/fault message; the exception rides `CausedBy` to the result chain and the new
`Log(ex,…)` line to the log. `HasError<T>` type assertions in the PLC tests survive the make-public (types preserved).
Each flagged change lists its test impact.

## The typed classes

resx key `Error<Name>`; en == current baked string unless flagged; ru guillemets «» where a value is quoted (none here).

| Class | Fields | en | ru | note |
|---|---|---|---|---|
| `NotConnectedError` | — | `Not connected to PLC` | `Нет соединения с ПЛК` | make public + fieldless (was `internal (string)`); update 4 callers |
| `ProtocolVersionMismatchError` | expected:int, actual:int | `PLC protocol version {0} does not match expected {1}` | `Версия протокола ПЛК {0} не соответствует ожидаемой {1}` | make public + expose `Expected`/`Actual`; **arm arg order (Actual, Expected)** — message reads "version {actual} … expected {expected}"; verified raise `PlcLifecycleManager:157` passes `(expected, actual)` |
| `RecipeActiveError` | — | `Recipe is being executed on PLC` | `Рецепт выполняется на ПЛК` | new leaf; replaces `PlcSyncExecutor:218` |
| `WriteVerificationFailedError` | attempts:int | `Recipe write verification failed after {0} attempts` | `Проверка записи рецепта не удалась после {0} попыток` | new leaf; replaces `PlcTransactionExecutor:154` |
| `RecipeNotCommittedError` | — | `Recipe not committed on PLC` | `Рецепт не зафиксирован на ПЛК` | new leaf; replaces `PlcTransactionExecutor:205` |
| `PlcCommandFailedError` | — | `PLC command failed` | `Ошибка команды ПЛК` | new Rule-B envelope; drops raw `ex.Message` (→ `CausedBy` + `Log(ex,…)`) |

`NotConnectedError`/`ProtocolVersionMismatchError` stay in `SemiStep.Core.Plc.S7.Protocol` (just make public). The four
new classes go in `SemiStep.Core.Plc.Sync` (alongside 6a's `ConnectionLostError`). The ru column is provisional.

## Task 1: Type the PLC producer surface + un-launder PlcSyncExecutor

Types every non-exception PLC producer error and recovers the types at the fault seam.

**Files:**
- Modify: `SemiStep/SemiStep.Core/Plc/S7/Protocol/NotConnectedError.cs` (public + fieldless), `ProtocolVersionMismatchError.cs` (public + props).
- Create: `Plc/Sync/RecipeActiveError.cs`, `Plc/Sync/WriteVerificationFailedError.cs`, `Plc/Sync/RecipeNotCommittedError.cs`.
- Modify: `Plc/Sync/PlcTransactionExecutor.cs` (4× `new NotConnectedError("…")` → `new NotConnectedError()` at 63/113/171/222; `:154` → `new WriteVerificationFailedError(_protocolSettings.MaxRetryAttempts)`; `:205` → `new RecipeNotCommittedError()`). `PlcLifecycleManager.cs` (~157 `ProtocolVersionMismatchError` ctor still compiles with props).
- Modify: `Plc/Sync/PlcSyncExecutor.cs` (194, 205, 212, 218, 221, 235 — the un-laundering).
- Modify: `ReasonLocalizer.cs` (5 arms + usings), `Resources.resx`, `Resources.ru.resx`, `Resources.Designer.cs` (5 keys), `CoreErrorLocalizationCoverageTests.cs` (5 samples).

- [x] `NotConnectedError.cs`: `internal sealed class NotConnectedError(string message) : Error(message);` → `public sealed class NotConnectedError()` + multi-line empty-brace body `: Error("Not connected to PLC")` (leaf-error family shape). Update the 4 `PlcTransactionExecutor` callers to `new NotConnectedError()`.
- [x] `ProtocolVersionMismatchError.cs`: make `public sealed`; keep ctor `(int expected, int actual)` and message; add `public int Expected { get; } = expected;` `public int Actual { get; } = actual;`.
- [x] Create `RecipeActiveError` (`: Error("Recipe is being executed on PLC")`), `RecipeNotCommittedError` (`: Error("Recipe not committed on PLC")`), `WriteVerificationFailedError(int attempts) : Error($"Recipe write verification failed after {attempts} attempts")` with `public int Attempts { get; } = attempts;`. All **`public sealed`** (required for the cross-assembly `ReasonLocalizer` arm + coverage auto-enrollment via `type.IsVisible`), `SemiStep.Core.Plc.Sync`, BOM, empty-brace shape.
- [x] `PlcTransactionExecutor`: `:154` `Result.Fail($"Recipe write verification failed after {…} attempts")` → `Result.Fail(new WriteVerificationFailedError(_protocolSettings.MaxRetryAttempts))`; `:205` `Result.Fail("Recipe not committed on PLC")` → `Result.Fail(new RecipeNotCommittedError())`.
- [x] **Un-launder `PlcSyncExecutor.CheckCanSyncAsync`** (~194-222): `:194` `Result.Fail("Not connected")` → `Result.Fail(new NotConnectedError())`; `:205` `reportFault(new Error(faultMessage))` → `reportFault(activeResult.Errors[0])` (forward typed inner; keep `var isDisconnected = activeResult.Errors.OfType<NotConnectedError>().Any();` for the LogWarning branch, drop the redundant `faultMessage`); `:212` `return Result.Fail(activeResult.Errors[0].Message)` → `return activeResult.ToResult()` (forward the failed `Result<bool>`'s errors — API proven at `PlcTransactionExecutor:119/135/200`); `:218` `reportFault(new Error("Recipe is being executed on PLC"))` → `reportFault(new RecipeActiveError())`; `:221` `Result.Fail("Recipe active")` → `Result.Fail(new RecipeActiveError())`.
- [x] **Un-launder `WriteSyncAsync`** (~235): `reportFault(new Error(writeResult.Errors[0].Message))` → `reportFault(writeResult.Errors[0])` (forward typed inner); keep the `:238` `OfType<NotConnectedError>()` LogError guard.
- [x] `ReasonLocalizer`: arms `NotConnectedError => Resources.ErrorNotConnected`, `RecipeActiveError => Resources.ErrorRecipeActive`, `RecipeNotCommittedError => Resources.ErrorRecipeNotCommitted` (bare); `ProtocolVersionMismatchError e => Format(Resources.ErrorProtocolVersionMismatch, e.Actual, e.Expected)` (arg order Actual then Expected); `WriteVerificationFailedError e => Format(Resources.ErrorWriteVerificationFailed, e.Attempts)`. Add the `SemiStep.Core.Plc.S7.Protocol` using (`SemiStep.Core.Plc.Sync` is already imported — `ConnectionLostError` uses it).
- [x] resx: 5 keys per the table, en/ru + Designer accessors. Coverage: 5 samples (`ProtocolVersionMismatchError(2, 1)`, `WriteVerificationFailedError(3)`, the 3 fieldless; add usings). New files BOM; Designer/resx BOM as-is.
- [x] **Test blast radius (Task 1):** grep the Tests tree for `Not connected`, `Recipe active`, `Recipe is being executed`, `Recipe not committed`, `verification failed`, `protocol version`, and `HasError<NotConnectedError>` / `HasError<ProtocolVersionMismatchError>` / `OfType<NotConnectedError>`. The `HasError<T>`/`OfType<T>` type asserts (`PlcTransactionExecutorTests:268/298`, `PlcLifecycleManagerReconnectTests:286/305/328`, `PlcExecutionMonitorTests:250`, `S7ServiceTests:111`) SURVIVE the make-public (types preserved). Update any test asserting the raw `.Message` string of a re-wrapped fault or the `"Recipe active"`/`"Not connected"` internal literals to the typed form / byte-identical message. Note `StubS7Service.cs:116/126` `Result.Fail("Not connected")` are stub-internal test doubles — leave them untyped (they do not flow through the production localizer). Localized-panel assertions under ambient ru → `ResourcesCultureScope.Use("en")`.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## Task 2: PlcCommandFailedError envelope + #120 exception logging

**Files:**
- Create: `Plc/Sync/PlcCommandFailedError.cs`.
- Modify: `PlcTransactionExecutor.cs` (the 5 `catch { return Result.Fail(ex.Message); }` — 105, 191, 232, 267, 301), `PlcSyncExecutor.cs` (~155), `Plc/Sync/PlcExecutionMonitor.cs` (`:120` delete).
- Modify: `ReasonLocalizer.cs` (1 arm), resx trio, coverage test, `PlcSyncCoordinatorTests.cs` (the contract test).

- [x] Create `PlcCommandFailedError` (`public sealed class PlcCommandFailedError() : Error("PLC command failed")`, `SemiStep.Core.Plc.Sync`, BOM, empty-brace shape).
- [x] `PlcTransactionExecutor` — each catch keeps its `when (ex is not OperationCanceledException)` guard; body becomes **log-then-typed-return**, severity by path:
  - `:267` `WriteRecipeDataAsync` → `_logger.LogError(ex, "PLC recipe data write failed"); return Result.Fail(new PlcCommandFailedError().CausedBy(ex));`
  - `:301` `WriteManagingAreaAsync` → `_logger.LogError(ex, "PLC managing-area write failed"); return Result.Fail(new PlcCommandFailedError().CausedBy(ex));`
  - `:105` `ReadRecipeDataAsync` → `_logger.LogWarning(ex, "PLC recipe data read failed"); return Result.Fail(new PlcCommandFailedError().CausedBy(ex));`
  - `:191` `ReadProtocolVersionAsync` → `_logger.LogWarning(ex, "PLC protocol version read failed"); return …`
  - `:232` `ReadAndDecodeAsync` → `_logger.LogWarning(ex, "PLC read/decode failed for DB {DbNumber}", dbNumber); return …`
  (Writes at Error — infrequent, operator-significant. Reads at Warning — matches the volume `PlcExecutionMonitor:120` already produced per poll failure, but now carries the real exception+stack instead of a message string.)
- [x] **`PlcExecutionMonitor.cs:120`** — delete the `_logger.LogWarning("Execution monitor poll error: {Message}", result.Errors[0].Message);` line (and its `continue;` stays). After typing, `result.Errors[0].Message` is the generic `"PLC command failed"` and the `ReadAndDecodeAsync:232` catch it fronts already Warns with the real exception — this line would only double-log a useless generic. Keep the `:108-118` `NotConnectedError` branch untouched (that is control flow: stop + `onConnectionLost`).
- [x] `PlcSyncExecutor.cs:155`: `reportFault(new Error(ex.Message))` → `reportFault(new PlcCommandFailedError().CausedBy(ex))`. The `_logger.LogError(ex, "Unhandled exception in sync task")` at `:153` already logs the exception+stack — do NOT add logging here.
- [x] `ReasonLocalizer`: arm `PlcCommandFailedError => Resources.ErrorPlcCommandFailed`. resx `ErrorPlcCommandFailed` en `PLC command failed` + ru + Designer accessor. Coverage: 1 sample. BOM.
- [x] **Rewrite the fault-contract test** `PlcSyncCoordinatorTests.SyncWriteFailure_EmitsFaultCarryingMessage` (~323-350): today asserts `faults[0].Message == "transport write blew up"` (the raw `ex.Message`). After un-laundering + typing, the write fault is `PlcCommandFailedError` carrying the exception on `CausedBy`. Rewrite to assert `faults[0].Should().BeOfType<PlcCommandFailedError>()` AND the exception (`"transport write blew up"`) is retained on `faults[0].Reasons` (the `ExceptionalError` from `CausedBy`). This doubles as the Task 3 CausedBy-preservation proof.
- [x] **Test blast radius (Task 2):** grep for any other test asserting a raw exception message out of `PlcTransactionExecutor`/`PlcSyncExecutor` (the `ex.Message` catches). The severity/type-only reporting tests (`MainWindowViewModelReportingTests`, `PlcExecutionMonitorTests:250`) survive. `PlcTransactionExecutorTests`'s `verification failed` assert is `.Message.Contains("verification failed")` — SURVIVES unchanged (message byte-identical after Task 1); converting it to `HasError<WriteVerificationFailedError>()` is optional polish, not required. Also grep `PlcExecutionMonitorTests` for any assert on the deleted `:120` poll-error Warning text (adjust if one pins it). Localized-panel assertions under ru → scope en.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## Task 3: Cross-path tests + doc + verify

**Files:** `ReasonLocalizerTests.cs`, the PLC/panel test home, `Docs/architecture/error-reporting.md`.

- [x] ru/en render cases for the 6 types (`ProtocolVersionMismatchError(2, 1)` → composed ru with actual/expected; `WriteVerificationFailedError(3)` → the attempts value; the 4 leaves) + the en `Localize == .Message` pins. (`ReasonLocalizerTests.cs`: 6 ru `[Fact]`s + one grouped en pin.)
- [x] **Fault-chain end-to-end (the payoff):** drive `PlcSyncExecutor` (or the seam directly) so `reportFault` emits a typed PLC inner (`NotConnectedError` / `RecipeActiveError` / `PlcCommandFailedError`), surfaced through `OnPlcFault` → the 6a seam, and assert Russian in the operation slot under `ru`. (`ResultReportingExtensionsTests.cs`: 3 `[AvaloniaFact]` operation-slot tests mirroring the 6a `ConnectionLostError` case. The full-executor drive of `PlcCommandFailedError` is already the Task 2 `SyncWriteFailure_EmitsTypedFaultCarryingException` contract test.)
- [x] **CausedBy + log preservation:** covered by the Task 2 contract-test rewrite (exception retained on `Reasons`). No separate unit assertion added — the contract test already asserts `faults[0].Reasons.OfType<ExceptionalError>()...Exception.Message == FailureMessage`; a bare `CausedBy` unit test would only re-cover FluentResults' own behavior.
- [x] fragment sweep across the Tests tree for all Task 1/2 fragments; confirm the `HasError<T>` asserts stay green and only the flagged sites changed. Sweep result: `PlcLifecycleManagerReconnectTests:286/305/328` (`HasError<ProtocolVersionMismatchError>`) and `PlcTransactionExecutorTests:268/298` (`HasError<NotConnectedError>`) survive the make-public unchanged; `PlcTransactionExecutorTests:227` (`.Message.Contains("verification failed")`) survives byte-identical; `StubS7Service:116/126` (`Result.Fail("Not connected")`) stay stub-internal untyped by design; no test pins the deleted `PlcExecutionMonitor:120` poll-error text.
- [x] `Docs/architecture/error-reporting.md`: PLC operator-facing faults now localize by type (connection / version / active / write-verification / not-committed / command). Note the un-laundering (faults forward the typed inner; the 6a seam is single-error, so `Errors[0]` forwarding drops siblings by design), the Rule-B `PlcCommandFailedError` (`CausedBy(ex)` + `Log(ex,…)`, headline localizes), and the two **deliberately-deferred English** producers: `PlcTransactionExecutor:183` (short-version-read diagnostic) and `PlcLifecycleManager:141` (enable catch → 6c). (Added a "PLC faults localize by type" subsection; updated the "Still free-text" line and the PLC routing-rules bullet.)
- [x] full `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green (1659 passed, 0 failed); `dotnet format` (no changes).

## Post-Completion

**Next (slice 6c):** #120 part 1 — thread a `CancellationToken` through `IS7Reader` (`ReadManagingAreaAsync` /
`ReadRecipeFromPlcAsync` / `ReadProtocolVersionAsync`) → `S7Service` → `PlcTransactionExecutor`, drop the manual
`IsCancellationRequested` polling in `PlcLifecycleManager.PerformReconnectReconciliationAsync`, and give the
`PlcLifecycleManager:141` enable catch its cancellation-aware handling (the `TaskCanceledException` routed there today).
Then slice 7 (style-editor `GridStyleEditorViewModel` `.Message`-join surface, pairs with #118). After 6c/7 the
config-load-culture boundary is the last English-by-design surface.

**Executed by exec:**
- branch: plc-errors-typed

## Verify it yourself

The slice has no manual repro that a test does not already demonstrate (the payoff is a localized panel string under
Russian culture, and the failure paths are transport exceptions the tests fake). Verify by the tests and the diff:

1. **Types localize by type + parity gates hold:**
   `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~Localization"` —
   `ReasonLocalizerTests` pins the ru render of all six types (e.g. `ProtocolVersionMismatchError(2,1)` →
   "Версия протокола ПЛК 1 не соответствует ожидаемой 2") and the en `Localize == .Message` pins;
   `CoreErrorLocalizationCoverageTests` fails if any public Core error lacks an arm/sample; `ResourceSyncTests`
   fails on any en/ru/Designer key or placeholder mismatch.
2. **Un-laundered fault reaches the panel in Russian (the payoff):**
   `dotnet test ... --filter "FullyQualifiedName~ResultReportingExtensionsTests"` — the seam facts drive
   `NotConnectedError` / `RecipeActiveError` / `PlcCommandFailedError` through `ReportFailure(IError)` and assert
   Russian in the operation slot.
3. **Production emission is typed, not laundered:**
   `dotnet test ... --filter "FullyQualifiedName~PlcSyncCoordinatorTests"` — `SyncWhenRecipeActiveOnPlc_...` asserts
   the emitted fault is `RecipeActiveError`; `SyncWriteFailure_EmitsTypedFaultCarryingException` asserts
   `PlcCommandFailedError` with the original exception retained on `.Reasons` (#120 CausedBy).
4. **Exactly-once logging / no silenced path:** read `git show master..HEAD -- .../PlcExecutionMonitor.cs
   .../PlcSyncExecutor.cs .../PlcTransactionExecutor.cs` — each failure path logs once (read exception + decode-Fail
   via the guarded monitor Warning; write exception via the catch `LogError`; verification-exhausted via the guarded
   `WriteSyncAsync` line).
5. **Whole suite:** `dotnet build SemiStep.slnx` (0 warnings) and `dotnet test` (1661 passed, 0 failed).
