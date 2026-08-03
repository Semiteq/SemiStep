# Slice 6c — Thread CancellationToken through the PLC reader (#120 part 1)

## Overview

Slice 6b closed the second half of #120 (exception type/stack preserved via `PlcCommandFailedError.CausedBy(ex)` +
logging). This slice closes the first half: **`CancellationToken` is not threaded through `IS7Reader`**, so PLC reads
run un-cancellable. The low-level transport already threads it — `IS7Transport.ReadBytesAsync(..., CancellationToken ct)`
and `PlcTransactionExecutor.ReadXxxAsync(CancellationToken ct = default)` both take and honor a token. The break is at the
`IS7Reader` seam in the middle:

```csharp
public interface IS7Reader
{
    Task<Result<PlcManagingAreaState>> ReadManagingAreaAsync();   // no ct
    Task<Result<Recipe>> ReadRecipeFromPlcAsync();                // no ct
    Task<Result<int>> ReadProtocolVersionAsync();                 // no ct
}
```

`S7Service` implements these and calls `transactionExecutor.ReadXxxAsync()` with **no token**, so the executor defaults
to `CancellationToken.None`. Downstream, `PlcLifecycleManager.PerformReconnectReconciliationAsync` holds the lifetime
token and compensates with **manual polling** — five scattered `if (cancellationToken.IsCancellationRequested) return;`
checks (lines 287/302/309/331/389). But a read that hangs *between* checks (unresponsive PLC) blocks anyway: the token
cannot interrupt the in-flight transport read because it never reaches it. And `EnableSync`'s catch (137-143) swallows
`OperationCanceledException` into `Result.Fail(ex.Message)`, conflating a genuine shutdown-cancel with a real failure.

This slice threads the token through the reader, uses it in the reconnect and enable paths, and drops the manual
polling. Pure plumbing — no localization, no resx, no new error types.

**The load-bearing subtlety — timeout-as-`TaskCanceledException`.** S7.Net surfaces a socket/read timeout as a
`TaskCanceledException` (a subclass of `OperationCanceledException`), even though **no `CancellationToken` was
cancelled**. The `StubS7Service.ProtocolVersionReadShouldThrowCanceled` flag (its XML doc says so) exists precisely to
exercise this. So the distinguisher between "abort, the app is shutting down" and "the read timed out, that's a real
failure" is **not** the exception type — both are `OperationCanceledException`. It is `token.IsCancellationRequested`:

- OCE **and** our token is cancelled → genuine cancellation → abort quietly.
- OCE **and** our token is NOT cancelled → a timeout wearing an OCE costume → a real failure, handled as one.

Every catch this slice touches uses that filter. A consequence worth stating up front: the existing
`EnableSync_WhenProtocolVersionReadThrowsCanceled_...` test (ownership tests :171) throws its `TaskCanceledException`
while the lifetime token is NOT cancelled, so it lands in the failure branch and **still asserts `IsFailed`,
unchanged** — the new handling adds a genuine-cancel branch above it, it does not reclassify the timeout.

Note (6b interaction): `PlcTransactionExecutor`'s catches are `catch (Exception ex) when (ex is not
OperationCanceledException)`, so **both** a genuine-cancel OCE and a timeout `TaskCanceledException` already bypass the
executor's `PlcCommandFailedError` wrapping and propagate as a raw throw. This slice does not change that; it just makes
the propagated OCE reach a token-aware catch at the `PlcLifecycleManager` layer instead of manual polling.

**Scope decisions:**
- **Reader interface, not `ConnectAsync`.** #120 names the reader. `S7Service.ConnectAsync(settings)` /
  `transport.ConnectAsync` take no token and stay that way; the keep-alive and reconnect loops manage their own
  `CancellationTokenSource` internally and are already cancellable. Only the three `IS7Reader` reads gain a token. A
  consequence to state plainly: since `EnableSync` awaits `ConnectAsync` (token-less) *before* the version read, a
  dispose landing during the connect is not interrupted — the token first bites at the version read immediately after
  connect returns, so `EnableSync` still aborts promptly. Coherent with #120 naming the reader.
- **User-initiated load stays `None`.** `RecipeCoordinator:204` → `PlcLifecycleManager.ReadRecipeFromPlcAsync()` is a
  button-click read with no cancellation source. `PlcLifecycleManager.ReadRecipeFromPlcAsync()` keeps its no-arg shape
  (forwards `CancellationToken.None` by default). The real token is wired only where cancellation matters: the reconnect
  reconciliation and the enable handshake.
- **No new error type.** A genuine `EnableSync` cancellation is a shutdown race, not an operator-facing fault — it needs
  no resx/localizer arm. It returns a plain internal `Result.Fail` (English) or is otherwise handled per Task 2; do NOT
  type it (that would wrongly enroll it in the localization coverage gate).
- Tests use a concrete `StubS7Service` (not Moq) as `IS7Reader` — so adding `CancellationToken ct = default` to the
  interface is source-compatible for every caller and the only implementor edits are `S7Service` + `StubS7Service`. No
  mock-setup churn.

Completes #120. After this, the config-load-culture boundary and slice 7 (style-editor `.Message`-join) are what remain
of the error/localization roadmap.

## Acceptance Evidence

- All three `IS7Reader` reads take `CancellationToken ct = default`; `S7Service` forwards it to the executor; `dotnet build SemiStep.slnx` is 0 warnings and every existing no-arg caller still compiles.
- `PerformReconnectReconciliationAsync` passes `cancellationToken` to every read and has no `IsCancellationRequested` polling left; its genuine-cancel wrapper (`catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)`) returns quietly.
- `EnableSync` captures `lifeToken` at entry and its catch splits into a genuine-cancel branch (`when (lifeToken.IsCancellationRequested)`) and the existing failure branch; the ownership timeout test (:171) stays green unchanged.
- The reconnect-cancel test proves a cancelled token stops reconciliation *at* the read (downstream recipe-read count stays 0 after the gate is released); the enable-cancel test proves the genuine-cancel branch runs on dispose-mid-read. Both deterministic (TCS-gated; the reconnect negative proof uses a bounded real-time settle window, incurred only on the passing path).
- Full `dotnet test` green; `dotnet format` clean.

## Task 1: Thread CancellationToken through IS7Reader and S7Service

**Files:**
- Modify: `SemiStep/SemiStep.Core/Plc/IS7Reader.cs` (3 methods).
- Modify: `SemiStep/SemiStep.Core/Plc/S7/S7Service.cs` (3 impls, lines 117/128/138).
- Modify: `SemiStep/SemiStep.Tests/Helpers/StubS7Service.cs` (3 impls, lines 107/119/129).

- [x] `IS7Reader`: add `CancellationToken ct = default` to `ReadManagingAreaAsync`, `ReadRecipeFromPlcAsync`, `ReadProtocolVersionAsync`. Add the `System.Threading` using if needed (file-scoped, System first). (No using added — implicit usings cover `CancellationToken`; an explicit one would be a redundant-using error under IDE0005.)
- [x] `S7Service`: each of the three methods accepts `CancellationToken ct = default` and forwards it — `transactionExecutor.ReadManagingAreaAsync(ct)`, `.ReadRecipeFromPlcAsync(ct)`, `.ReadProtocolVersionAsync(ct)`. (The executor already takes `CancellationToken ct = default`; confirm the exact method signatures.) The existing `LogWarning` failure logging stays.
- [x] `StubS7Service`: each of the three methods accepts `CancellationToken ct = default`. Honor genuine cancellation — at the top of each, `ct.ThrowIfCancellationRequested();` so a test that cancels the token sees the read throw an OCE (this is what makes the reconnect-cancel test in Task 3 exercise the real token path). Keep the `ProtocolVersionReadShouldThrowCanceled` flag exactly as is (a token-**independent** `throw new TaskCanceledException(...)` — it models a timeout, not a cancel). Update that flag's XML doc only if wording drift is needed to keep "token not cancelled = timeout" accurate.
- [x] `dotnet build SemiStep.slnx` 0 warnings. Every existing no-arg caller (`PlcLifecycleManager`, `RecipeCoordinator`, the test helpers `CoreTestHelper`/`CsvTestHelper`/`RecipeCoordinatorLoadRecipeTests`/`RecipeCoordinatorSaveGateTests` that register `IS7Reader`) still compiles via the default. Full `dotnet test` green.

## Task 2: Use the token in PlcLifecycleManager — drop manual polling, cancellation-aware enable

**Files:** `SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs`.

- [x] `ValidateProtocolVersionAsync` → `ValidateProtocolVersionAsync(CancellationToken ct)`: pass `ct` to `_reader.ReadProtocolVersionAsync(ct)`. Update both callers: `EnableSync` passes the `lifeToken` it captured at entry (see the catch item — capture once, use it for both the read arg and the filter, so a dispose mid-read cancels the same token the filter checks); `PerformReconnectReconciliationAsync` passes its `cancellationToken`.
- [x] `PerformReconnectReconciliationAsync`: pass `cancellationToken` to each read — `_reader.ReadManagingAreaAsync(cancellationToken)` (307), `_reader.ReadRecipeFromPlcAsync(cancellationToken)` (329), and the version read via the updated `ValidateProtocolVersionAsync(cancellationToken)` (292). **Remove the manual `if (cancellationToken.IsCancellationRequested) return;` checks** — four in the reconciliation body (287, 302, 309, 331) plus the one at 389, which lives in the separate helper `ApplyReconnectPlcRecipeAsync` (verify each by content; they are now redundant because a cancelled token makes the in-flight read throw). Wrap the reconciliation body in a genuine-cancel catch so the fire-and-forget `ContinueWith(OnlyOnFaulted)` does not log a shutdown-cancel as an error:
  ```csharp
  try { /* the reconciliation body */ }
  catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
  {
      return;   // genuine cancellation (dispose/disconnect): abort quietly
  }
  ```
  A timeout-OCE (token not cancelled) is left to propagate exactly as today — do NOT broaden the catch to swallow it (that would silently drop a real read failure instead of the current fault-and-log). The apply-callback path (`ApplyReconnectPlcRecipeAsync`) keeps its own try/catch; its post-callback `IsCancellationRequested` check at 389 sat *after* `await callback(...)`, so it never guarded the apply itself — it only suppressed the failure-log, and the genuine-cancel wrapper subsumes that.
  **On "redundant": the checks were best-effort, not a guarantee, and one window is traded, not eliminated.** A cancel arriving *during* an (uncancellable) read used to be caught at the next checkpoint before `NotifyLocalRecipe` (319/325/365) or the conflict event (361); now such a cancel throws at the read (strictly better — that was the multi-second window). What remains is the microsecond window between a read *completing* and the synchronous branch that fires `NotifyRecipeChanged`/`PlcRecipeConflictDetected` — a cancel landing there proceeds during shutdown. That window existed under polling too (a cancel one instruction after a check), so no behavioral guard is lost; it is called out here so it is not mistaken for an unexamined regression. The apply callback's own `catch (Exception ex)` (383-387) will still log a shutdown-cancelled dispatcher marshal as "Reconnect apply callback threw" — pre-existing, unchanged, left alone.
- [x] `EnableSync` catch (137-143): split the single `catch (Exception ex)` into two, genuine-cancel first:
  ```csharp
  catch (OperationCanceledException) when (_lifetimeCts.Token.IsCancellationRequested)
  {
      _syncService.SetSyncEnabled(false);
      ReleaseOwnershipLease();
      return Result.Fail("PLC sync enable cancelled");   // internal, English; a shutdown race, not a localized fault
  }
  catch (Exception ex)   // includes a timeout-as-TaskCanceledException while the token is NOT cancelled
  {
      _syncService.SetSyncEnabled(false);
      ReleaseOwnershipLease();
      _logger.LogWarning("Enabling PLC sync failed: {Message}", ex.Message);
      return Result.Fail(ex.Message);
  }
  ```
  The rollback (SetSyncEnabled(false) + ReleaseOwnershipLease) is identical in both, so the genuine-cancel branch is not a leak. This preserves the ownership timeout test (`ProtocolVersionReadShouldThrowCanceled` → token not cancelled → second catch → `IsFailed`).
  **Capture the token unconditionally at method entry — `var lifeToken = _lifetimeCts.Token;` — and use `lifeToken.IsCancellationRequested` in the filter.** `Dispose` (95-101) cancels then disposes `_lifetimeCts` while `EnableSync` may be awaiting the connect or the version read on another context — the exact shutdown race this branch exists for — and `CancellationTokenSource.Token` on a *disposed* CTS throws `ObjectDisposedException`. A `CancellationToken` struct captured before dispose stays readable, and since `Dispose` cancels before disposing, the captured token reads `true`. An exception thrown inside a `when` filter is treated as `false` by the CLR, so without the capture the shutdown-cancel misclassifies into the generic failure branch — it does not crash; the capture is free and removes the misclassification. Reading `_lifetimeCts.Token` at entry makes `EnableSync`-after-`Dispose` throw ODE at entry, which is calling-a-disposed-object misuse and acceptable.
- [x] `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green.

## Task 3: Cancellation tests + verify

**Files:** `SemiStep/SemiStep.Tests/Domain/PlcLifecycleManagerReconnectTests.cs`, `PlcLifecycleManagerOwnershipTests.cs`, `StubS7Service.cs` (if a blocking-read hook is needed).

- [x] **Reconnect honors the token (replaces the polling):** prove a cancelled lifetime token aborts reconciliation *at the in-flight read*, not via post-hoc polling. **The naive sketch is vacuous** — disposing before the trigger short-circuits at `OnConnectionStateChanged`'s `_disposed` guard (273-276) so reconciliation never spawns, and disposing after the trigger races because the stub's `Task.FromResult` reads complete synchronously inside `RaiseStateChanged` before dispose runs. **A TCS gate is mandatory, and it must be token-aware.** Construction:
  1. Add a `TaskCompletionSource`-gated read to `StubS7Service` (following the existing flag pattern): the first `ReadManagingAreaAsync` (or version read) signals a "read entered" TCS, then does `await _releaseGate.Task.WaitAsync(ct)` — the `WaitAsync(ct)` overload is what observes a cancel arriving *mid-wait* (a bare `ct.ThrowIfCancellationRequested()` at entry cannot).
  2. Gate the **managing-area** read (the first read after the version check), and add a `ReadRecipeFromPlcCallCount` counter to the stub (its recipe read is downstream of the managing-area read). Sequence: raise `Connected` to trigger reconciliation → `await` the "read entered" signal → `plc.Dispose()` (cancels `_lifetimeCts`) → **then release the gate** (`_releaseGate.SetResult(...)`).
  3. **Assert the discriminator, not just the no-op.** "No recipe applied / no conflict" alone is vacuous — a silently-ignored token would leave the gated read *hung* forever, which also never applies/conflicts, so hang ≡ abort under those assertions. The proof is a downstream call that must NOT happen: after releasing the gate, assert (via a bounded poll, no real delay) that `ReadRecipeFromPlcCallCount == 0`. Genuine cancel → `WaitAsync(ct)` threw → reconciliation never advanced past the managing-area read → recipe read count stays 0 even though the gate was released. A broken/ignored token → releasing the gate lets the managing-area read return → reconciliation proceeds to the recipe read → count 1. That divergence is what separates working plumbing from a hang. Also assert no recipe applied (register an apply callback that flips a flag) as a secondary check. **Do not assert on log output** — the builders use a bare `.AddLogging()` (`PlcLifecycleManagerReconnectTests.cs:44`), no capturing provider.
  Keep it deterministic (TCS, bounded poll, a bounded real-time settle window incurred only on the passing path).
- [x] **Enable genuine-cancel vs timeout stay distinct:** the existing `EnableSync_WhenProtocolVersionReadThrowsCanceled_ReleasesLeaseAndDisablesSync` (:171) must stay green unchanged (timeout → `IsFailed`, lease released, sync disabled). Add a sibling for the genuine-cancel branch. **It cannot dispose-then-enable** — Task 2 makes `EnableSync` read `_lifetimeCts.Token` at entry, so a call after `Dispose` throws `ObjectDisposedException` at entry, never reaching the branch. It must cancel *while `EnableSync` is mid-await on the version read.* Construction (same gate machinery as the reconnect test): gate the **version** read in the stub (signal "read entered", then `await _releaseGate.Task.WaitAsync(ct)`) → start `EnableSync` on a background task (it captures `lifeToken` at entry, connects, enters the gated version read) → `await` the "read entered" signal → `plc.Dispose()` (cancels `_lifetimeCts`, which the captured `lifeToken` and the passed read token both observe) → the gated `WaitAsync(ct)` throws OCE with the token cancelled → the first catch (`when (lifeToken.IsCancellationRequested)`) runs → `await` the `EnableSync` task. Assert: the result is failed but **is the genuine-cancel outcome, not the timeout path** — assert it is NOT the timeout error rather than pinning the exact English string `"PLC sync enable cancelled"` (brittle); the strongest available discriminator is that the lease is released and sync disabled via the cancel branch while the version read was gated (never returned a value), distinguishing it from `:171` where the read threw a token-independent TCE. Bounded, deterministic (TCS, no real delays).
- [x] **Existing reconnect suite green:** `PlcLifecycleManagerReconnectTests` (the `HasError<ProtocolVersionMismatchError>` / reconnect-reconciliation cases) unaffected — the reads now take a token but default/observe correctly.
- [x] Fragment sweep: grep the Tests tree for any `.ReadProtocolVersionAsync()` / `.ReadManagingAreaAsync()` / `.ReadRecipeFromPlcAsync()` call that a signature change would break (none should — the interface default covers callers; the `StubS7Service` impls gained the param). Report what, if anything, moved.
- [x] full `dotnet build SemiStep.slnx` 0 warnings; full `dotnet test` green; `dotnet format`.

## Post-Completion

**Closes #120** (part 1 here + part 2 in 6b). Remaining roadmap: **slice 7** — style-editor
`GridStyleEditorViewModel` `.Message`-join surface (pairs with #118). After slice 7 the config-load-culture boundary is
the last English-by-design surface in the error/localization roadmap.

**Executed by exec:**
- branch: plc-cancellation-plumbing

## Verify it yourself

The behavior — an un-cancellable read now aborts on the lifetime token — has no reliable manual repro (it needs a
hung/unresponsive PLC read to observe; the UI masks the difference). Verify by the tests and the diff:

1. **Token threads to the transport:** read `git show master..HEAD -- SemiStep/SemiStep.Core/Plc/IS7Reader.cs SemiStep/SemiStep.Core/Plc/S7/S7Service.cs` — all three reads take `CancellationToken ct = default` and `S7Service` forwards each to `transactionExecutor.ReadXxxAsync(ct)` (which already reaches `_transport.ReadBytesAsync(..., ct)`).
2. **Manual polling gone, cancellation-aware catches in:** `git show master..HEAD -- SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs` — no `IsCancellationRequested` polling remains; `ReconcileWithPlcAsync` passes `cancellationToken` to every read; the wrapper and `EnableSync` both split on `when (token.IsCancellationRequested)`; `lifeToken` is captured at `EnableSync` entry.
3. **The reconnect-cancel test is non-vacuous** (proves the token stops the in-flight read):
   `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~PlcLifecycleManagerReconnectTests"` — the gated managing-area read is cancelled by `Dispose`, and `ReadRecipeFromPlcCallCount` stays 0 after the gate is released (a token-ignoring regression would advance to the recipe read → count 1).
4. **The enable-cancel test discriminates the genuine-cancel branch** (via the shared `SyncEnableCancelledMessage` constant):
   `dotnet test ... --filter "FullyQualifiedName~PlcLifecycleManagerOwnershipTests"` — asserts the cancel message equals the constant; deleting the production genuine-cancel branch routes the OCE to the generic catch ("A task was canceled.") and fails the test. The `:171` timeout test stays green (token not cancelled → failure branch).
5. **Whole suite:** `dotnet build SemiStep.slnx` (0 warnings) and `dotnet test` (1663 passed, 0 failed).
