# PLC Write Concurrency Protection (issue #48)

## Overview

Issue #48 reports that two concurrent writers to the same PLC can interleave the
`committed = false → data → recipe_lines → committed = true` sequence and leave a
corrupted recipe in the PLC.

Investigation established two distinct, separately-reachable failure modes:

1. **Cross-process (the reachable form of #48).** The app is single-window-per-process
   (`App.EnsureSingleStart`), and in-process writes are already serialized
   (`PlcSyncExecutor` is the only writer and queues rather than parallelizes). The real
   reachable race is **two app instances on one machine** both enabling PLC sync against
   the same PLC: each process opens its own independent S7 connection with zero
   cross-process arbitration. The design intent is that only one instance may sync to the
   PLC at a time; the rest are local-only editors. This is foolproofing, not a security
   boundary.

2. **In-process transport interleaving (adjacent finding).** Within the single syncing
   process, several background tasks share the **one** `S7.Net.Plc` TCP connection with no
   transport-level lock: the execution monitor poll loop reads, the keep-alive loop reads,
   the reader paths (reconnect reconciliation, read-from-PLC) read, and the sync executor
   writes. Concurrent request/response round-trips on one socket can corrupt PDU framing.
   This is a different bug from #48 and was discovered during investigation.

The work is split into **two PRs** (one logical change each, per the project git
workflow), implemented back-to-back:

- **PR1 — cross-process sync ownership** via an exclusive OS file lock.
- **PR2 — in-process transport serialization** via a per-round-trip gate in the driver.

A finding documenting failure mode 2 is also posted to issue #48 during the work.

## Context (from discovery)

- Single PLC writer path in production: `PlcSyncExecutor.WriteSyncAsync` →
  `PlcTransactionExecutor.WriteRecipeWithRetryAsync` (`PlcTransactionExecutor.cs:109`).
- Sync enable/disable chokepoint: `PlcLifecycleManager.EnableSync` (`PlcLifecycleManager.cs:97-124`),
  which couples `ConnectAsync` with sync; release points: `DisableSync` (`:149`),
  `FailProtocolVersionHandshakeAsync` (`:143`), `Dispose` (`:72`).
- "Window" in this codebase == app **instance/process**: `App.EnsureSingleStart`
  (`App.axaml.cs:129`) allows one window per process; VMs are singletons (`UiDi.cs`); the
  per-window edit/connect plan (`Docs/plans/completed/20260520-per-window-edit-connect-mode.md`)
  treats windows as independent because each is a separate process. A refused instance
  stays sync-disabled, so `RecipeCoordinator.CanEditRecipe` keeps it a full local editor.
- UI already handles `EnableSync` failure: `MainWindowViewModel.ExecuteToggleSyncAsync`
  reports `Result.Fail` via `MessagePanel.ReportError` and leaves the toggle off
  (`MainWindowViewModel.cs:139-144`). No UI rework needed beyond message text.
- `PlcConnectionSettings(IpAddress, Port, Rack, Slot)` (`PlcConnectionSettings.cs`) — the
  ownership key.
- DI: `PlcLifecycleManager` registered in `RecipeDi.cs:21`; S7 stack in `S7Di.cs`.
- **All in-process socket users funnel through one `S7Driver` singleton**, reached via two
  interfaces (both registered to the same instance, `S7Di.cs:16-17`):
  - `IS7Transport`: `PlcTransactionExecutor` (`PlcTransactionExecutor.cs:25`) — covers the
    monitor read (`PlcExecutionMonitor.PollLoopAsync` → `ReadExecutionStateAsync`,
    read at `PlcExecutionMonitor.cs:104`), the sync write (`PlcSyncExecutor.StartDebounce`
    → `PlcTransactionExecutor.WriteRecipeDataAsync`, `PlcTransactionExecutor.cs:236-269`),
    and the reader paths (`ReadRecipeFromPlcAsync`/`ReadManagingAreaAsync`/
    `ReadProtocolVersionAsync`).
  - `IS7Driver`: `S7Service` (`S7Service.cs:13`) — the **keep-alive loop**
    (`S7Service.KeepAliveLoopAsync`, `S7Service.cs:229-253`) reads 1 byte of `ManagingDb`
    via `transport.ReadBytesAsync` (`S7Service.cs:239`). `ManagingDb` IS mutated by the
    writer (`PlcTransactionExecutor.WriteManagingAreaAsync`, `:282-303`).
- Tests: `SemiStep.Tests/Domain/` (`PlcLifecycleManagerReconnectTests.cs` uses
  `[Trait("Component","Domain")]`, `[Trait("Category","Integration")]`),
  `SemiStep.Tests/S7/`. Traits: `[Trait("Component","S7|Core|Domain")]`,
  `[Trait("Category","Unit|Integration")]`.

## Development Approach

- **Testing approach: Regular** (implement, then write tests in the same task) — matches
  project convention.
- Complete each task fully before the next; all tests pass before moving on.
- Small, focused changes; constructor injection; `var`; UTC timestamps; FluentResults for
  fallible operations (refusal is `Result.Fail`, not an exception).
- `dotnet format SemiStep/SemiStep.slnx` before any commit (pre-commit hook).
- New files are auto-included (SDK-style projects); no manual csproj edits.
- **Each task MUST include new/updated tests** covering success and failure paths.

## Testing Strategy

- **Unit tests:**
  - (Component=S7) Endpoint-token builder produces a stable, filesystem-safe token from
    `PlcConnectionSettings`.
  - (Component=Domain) `PlcLifecycleManager.EnableSync` with a mocked `IPlcSyncOwnership`:
    acquire succeeds → proceeds to connect; acquire refused → returns `Result.Fail`
    carrying owner info and does **not** connect / does not set sync enabled;
    `DisableSync` / failed handshake / `Dispose` release the lease exactly once
    (idempotent). Tests live next to `PlcLifecycleManagerReconnectTests.cs` and reuse its
    `[Trait("Component","Domain")]`.
  - (Component=S7) `TransportSerializer`: concurrent operations never overlap (probe
    delegate records max concurrency == 1); a `WaitAsync` canceled before entry does not
    `Release` (no `SemaphoreFullException`); throwing operations still release the gate.
- **Integration tests (Component=S7, Category=Integration):**
  - Real `FileSyncOwnership` with an injected temp lock root (no real `%ProgramData%`
    writes): first `TryAcquire` succeeds; a second `TryAcquire` for the same endpoint is
    refused while the first lease is held; after disposing the first, re-acquire succeeds.
  - Owner metadata written by the holder is readable by the refused caller.
  - Lease double-dispose is safe (idempotent).
  - Acquire when the lock file/dir is inaccessible (`UnauthorizedAccessException`) returns
    a refusal `Result.Fail`, not an unhandled throw.

## Progress Tracking

- Mark completed items `[x]` immediately. Add discovered tasks with ➕. Blockers with ⚠️.
- Keep this plan in sync if scope changes.

## Solution Overview

**PR1 — cross-process ownership (file lock).** A small Core service `IPlcSyncOwnership`
with one production implementation `FileSyncOwnership`. Acquisition opens a dedicated
lock file (`FileMode.Create`, `FileAccess.ReadWrite`, `FileShare.Read` — see the Task 2
note for why `FileShare.Read` rather than `FileShare.None`); the open **is** the atomic
test-and-set (no TOCTOU). The
handle is held for the whole ownership lifetime in the returned `ISyncOwnershipLease`;
disposing it closes the handle. Chosen over named `Mutex` (thread-affine — breaks
acquire-on-one-thread / release-on-another across `await`), named `Semaphore` (leaks the
permit on crash — not crash-safe), and `EventWaitHandle`/PID-file/PLC-flag (no atomic
ownership / TOCTOU). The file lock is crash-safe because Windows closes the process's
handles on termination, releasing the lock with no manual cleanup. Scope is **machine-wide**:
the lock file lives under `%ProgramData%\SemiStep\locks\`, keyed by the PLC endpoint, so a
second Windows user/RDP session cannot become a second writer.

`PlcLifecycleManager` acquires the lease at the start of `EnableSync` (before
`ConnectAsync`); on refusal it returns `Result.Fail` with the current owner's metadata and
does not connect. It releases the lease in `DisableSync`, in the failed-handshake rollback,
and in `Dispose`, guarding against double-release. The UI surfaces the refusal message via
the existing `MessagePanel.ReportError` path.

`TryAcquire` is **synchronous** (opening a file handle does no awaitable work) to avoid
fake-async; it is called from the async `EnableSync` without issue.

**PR2 — in-process transport serialization.** First confirm whether `S7.Net.Plc` already
serializes calls internally. If not, place a `SemaphoreSlim(1,1)` **inside `S7Driver`**
(extracted into a tiny `TransportSerializer` helper that the driver delegates to), gating
every `ReadBytesAsync`/`WriteBytesAsync` round-trip.

The gate must live in `S7Driver`, not in a decorator over `IS7Transport`: every socket user
reaches the single `S7Driver` singleton, but through **two** interfaces — `IS7Transport`
(the executor: monitor reads, sync writes, reader paths) and `IS7Driver` (the keep-alive
read). A decorator on `IS7Transport` alone would miss the keep-alive path. Putting the gate
in `S7Driver` covers all of them.

Granularity and scope (explicit, answering the design question): per-round-trip locking
fixes **PDU-framing corruption only**. It intentionally does **not** make the multi-round-trip
write transaction (`PlcTransactionExecutor.WriteRecipeDataAsync`, `:236-269`) atomic with
respect to concurrent reads. That is acceptable because:
- cross-process arbitration is handled by PR1; and
- a mid-transaction reader degrades gracefully: `ReadRecipeFromPlcAsync` checks the
  `committed` flag first and bails when it is `false` (`PlcTransactionExecutor.cs:203-206`),
  and the writer sets `committed = false` at the very start of the sequence (`:240-241`), so
  an interleaving read sees `committed = false` and returns "not committed" rather than a
  half-written mix; the keep-alive read fetches 1 byte and discards it (harmless).

The `TransportSerializer` is unit-testable on its own (probe delegate asserting no overlap,
cancellation, and exception-path release) without needing a test seam under `S7.Net.Plc`.

## Technical Details

- **Ownership contracts (Core, `SemiStep.Core/Plc/Sync/Ownership/`, namespace
  `SemiStep.Core.Plc.Sync.Ownership`; imports `PlcConnectionSettings` from
  `SemiStep.Core.Plc.Configuration`):**
  - `interface IPlcSyncOwnership { Result<ISyncOwnershipLease> TryAcquire(PlcConnectionSettings endpoint); }`
  - `interface ISyncOwnershipLease : IDisposable { OwnerInfo Owner { get; } }` (sync
    `IDisposable` — closing a `FileStream` is synchronous and cheap, keeping
    `PlcLifecycleManager.Dispose` simple; `Dispose` is idempotent).
  - `record OwnerInfo(int ProcessId, string MachineName, string UserName, DateTimeOffset AcquiredUtc)`.
- **`FileSyncOwnership`:** lock root defaults to
  `Path.Combine(Environment.GetFolderPath(SpecialFolder.CommonApplicationData), "SemiStep", "locks")`;
  the root is injectable (constructor parameter) so tests use a temp dir. File name:
  `plc-sync-<token>.lock`, token = sanitized `{IpAddress}_{Port}_{Rack}_{Slot}`. On
  `TryAcquire`: ensure root exists with a machine-wide ACL (see Task 2), open
  `FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read)` (see the Task 2
  note for the `FileShare.Read` rationale), write `OwnerInfo` (UTF-8 JSON) and flush; on
  `IOException`, open the file `FileShare.ReadWrite` read-only to parse the current
  `OwnerInfo` and return `Result.Fail` carrying it (or a generic refusal if the metadata is
  empty/corrupt); on `UnauthorizedAccessException` (ACL-blocked open), return a refusal
  `Result.Fail` (treated as "owned/unavailable", never an unhandled throw).
- **`TransportSerializer` (Core, alongside `S7Driver`):** wraps a `SemaphoreSlim(1,1)`;
  exposes `Task RunAsync(Func<Task> op, CancellationToken ct)` and
  `Task<T> RunAsync<T>(Func<Task<T>> op, CancellationToken ct)`. Pattern:
  `await _gate.WaitAsync(ct);` **outside** the try, then `try { return await op(); } finally { _gate.Release(); }`
  so a canceled acquire never reaches `Release`. Disposed with the driver.
- **Refusal message:** `PlcLifecycleManager` maps the failed `Result` to a message like
  `"PLC sync is owned by another instance (user {UserName}, since {AcquiredUtc:HH:mm} UTC)"`.

## What Goes Where

- **Implementation Steps** (checkboxes): all code, tests, the issue-#48 finding comment
  (doable in-session via `gh`).
- **Post-Completion** (no checkboxes): two-instance manual verification (needs a real
  second process and ideally a PLC), and any installer-time ACL provisioning if runtime
  ACL setup proves insufficient.

## Implementation Steps

### PR1 — Cross-process PLC sync ownership

#### Task 1: Define ownership contracts and endpoint-token builder

**Files:**
- Create: `SemiStep/SemiStep.Core/Plc/Sync/Ownership/IPlcSyncOwnership.cs`
- Create: `SemiStep/SemiStep.Core/Plc/Sync/Ownership/ISyncOwnershipLease.cs`
- Create: `SemiStep/SemiStep.Core/Plc/Sync/Ownership/OwnerInfo.cs`
- Create: `SemiStep/SemiStep.Core/Plc/Sync/Ownership/SyncOwnershipEndpointToken.cs`
- Create: `SemiStep/SemiStep.Tests/S7/SyncOwnershipEndpointTokenTests.cs`

- [x] Define `IPlcSyncOwnership` with `Result<ISyncOwnershipLease> TryAcquire(PlcConnectionSettings endpoint)`.
- [x] Define `ISyncOwnershipLease : IDisposable` exposing `OwnerInfo Owner`.
- [x] Define `OwnerInfo` record (ProcessId, MachineName, UserName, AcquiredUtc).
- [x] Implement `SyncOwnershipEndpointToken.For(PlcConnectionSettings)` (static helper, no
      interface — single pure function) producing a stable, filesystem-safe token.
- [x] Write tests: token is deterministic, stable across calls, and contains no
      path-invalid characters for representative endpoints.
- [x] Run tests — must pass before next task.

#### Task 2: Implement `FileSyncOwnership`

**Files:**
- Create: `SemiStep/SemiStep.Core/Plc/Sync/Ownership/FileSyncOwnership.cs`
- Create: `SemiStep/SemiStep.Core/Plc/Sync/Ownership/FileSyncOwnershipLease.cs`
- Create: `SemiStep/SemiStep.Tests/S7/FileSyncOwnershipTests.cs`

- [x] Implement `FileSyncOwnership` with an injectable lock-root path (default
      `%ProgramData%\SemiStep\locks`); `TryAcquire` opens `FileShare.Read` (see note),
      writes `OwnerInfo`, returns a lease holding the open `FileStream`.
- [x] On `IOException`, read the holder's `OwnerInfo` (open `FileShare.ReadWrite`,
      read-only) and return `Result.Fail` carrying it (`OwnedByAnotherInstanceError`); if
      metadata is unreadable, fail with a generic "owned by another instance" message.
- [x] On `UnauthorizedAccessException`, return a refusal `Result.Fail` (do not let it
      propagate as an unhandled throw).
- [x] Decide and implement ACL provisioning (first-class, not deferred): on first use,
      create the lock root and grant `Users: Modify` so a different Windows user can open the
      file. `DirectoryInfo` ACL APIs (`GetAccessControl`/`SetAccessControl`,
      `DirectorySecurity`, `FileSystemAccessRule`, `SecurityIdentifier`) resolve on `net10.0`
      from the shared framework — **no** `System.IO.FileSystem.AccessControl` package needed;
      csproj unchanged. ACL is gated by `OperatingSystem.IsWindows()` and is best-effort
      (`SyncLockRootProvisioner`): if it fails, the `UnauthorizedAccessException` → refusal
      mapping keeps the cross-user case a clean refusal rather than a crash.
- [x] Implement `FileSyncOwnershipLease.Dispose` to close the handle (any thread) and be
      **idempotent** (guard a `_disposed` flag).
- [x] Write integration tests (temp lock root): acquire → second acquire refused while held
      → dispose → re-acquire succeeds; refused caller reads holder `OwnerInfo`; double-dispose
      is safe; inaccessible path → refusal `Result.Fail`.
- [x] Run tests — must pass before next task.

> Note (`FileShare`): the holder opens with `FileShare.Read`, not `FileShare.None`. A
> `FileShare.None` holder denies all sharing, so the refused caller could not open the file
> to read holder `OwnerInfo`, defeating the "refused caller reads holder OwnerInfo"
> requirement. `FileShare.Read` still denies a second writer (a second `TryAcquire` requests
> `FileAccess.ReadWrite`, which the holder's read-only share mode rejects with `IOException`),
> so the test-and-set remains atomic and exclusive for write access, while metadata readers
> (`FileAccess.Read` + `FileShare.ReadWrite`) succeed.

#### Task 3: Wire ownership into `PlcLifecycleManager` and DI

**Files:**
- Modify: `SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs`
- Modify: `SemiStep/SemiStep.Core/Plc/S7/S7Di.cs`
- Create: `SemiStep/SemiStep.Tests/Domain/PlcLifecycleManagerOwnershipTests.cs`

- [x] Inject `IPlcSyncOwnership` into `PlcLifecycleManager`; hold the `ISyncOwnershipLease`
      in a field.
- [x] In `EnableSync`: call `TryAcquire(config.Connection)` **before** `ConnectAsync`; on
      failure return `Result.Fail` with the owner-info message and do not connect or set
      sync enabled.
- [x] Release the lease in `DisableSync`, `FailProtocolVersionHandshakeAsync`, and `Dispose`
      (release at most once; guard against double-release).
- [x] Register `IPlcSyncOwnership` → `FileSyncOwnership` as a singleton in `S7Di`.
- [x] Write tests (mock `IPlcSyncOwnership`, `[Trait("Component","Domain")]` to match the
      sibling file): success path acquires then connects; refusal path returns `Fail` with
      owner message and skips connect (`IsSyncEnabled == false`); disable / failed-handshake /
      dispose release the lease exactly once.
- [x] Run tests — must pass before next task.

#### Task 4: PR1 close-out

- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — full suite green (710 passed, 0 failed, 0 skipped).
- [x] `dotnet format SemiStep/SemiStep.slnx` — clean, no changes (verify-no-changes exit 0).
- [x] Confirm PR1 scope contains only ownership changes (no PR2 transport edits). Verified
      via `git diff origin/master...HEAD --stat`: only the plan doc, the new
      `SemiStep.Core/Plc/Sync/Ownership/*` files (`IPlcSyncOwnership.cs`,
      `ISyncOwnershipLease.cs`, `OwnerInfo.cs`, `OwnedByAnotherInstanceError.cs`,
      `SyncOwnershipEndpointToken.cs`, `FileSyncOwnership.cs`, `FileSyncOwnershipLease.cs`,
      `SyncLockRootProvisioner.cs`), `PlcLifecycleManager.cs`, `S7/S7Di.cs`, and test
      files/helpers (`Domain/PlcLifecycleManagerOwnershipTests.cs`,
      `S7/FileSyncOwnershipTests.cs`, `S7/SyncOwnershipEndpointTokenTests.cs`,
      `Helpers/StubPlcSyncOwnership.cs`, `Helpers/StubSyncOwnershipLease.cs`,
      `Helpers/StubS7Service.cs`, plus trait/helper touch-ups in
      `Core/Helpers/CoreTestHelper.cs`, `Csv/Helpers/CsvTestHelper.cs`,
      `Domain/PlcLifecycleManagerReconnectTests.cs`,
      `UI/RecipeCoordinatorLoadRecipeTests.cs`, `UI/RecipeCoordinatorSaveGateTests.cs`).
      No `S7Driver.cs` / `TransportSerializer.cs` / transport edits present.

### Finding on issue #48

#### Task 5: Document the in-process transport race as a finding

- [x] Post a comment on issue #48 (via `gh issue comment 48`) describing failure mode 2
      (monitor read, keep-alive `ManagingDb` read, and reader paths interleaving with the
      sync write on one `S7.Net.Plc` socket), noting it is distinct from the cross-process
      race and is addressed in PR2. (User-approved external action.)

### PR2 — In-process transport serialization

#### Task 6: Confirm `S7.Net.Plc` threading and serialize transport round-trips

**Files:**
- Create: `SemiStep/SemiStep.Core/Plc/S7/TransportSerializer.cs`
- Modify: `SemiStep/SemiStep.Core/Plc/S7/S7Driver.cs`
- Create: `SemiStep/SemiStep.Tests/S7/TransportSerializerTests.cs`

- [x] Inspect the `S7.Net` package sources to confirm whether `Plc` already serializes
      concurrent read/write on one connection; record the finding in this plan.
      Finding (S7.Net 0.20.0, decompiled): individual PDU round-trips ARE already serialized
      via an internal `TaskQueue`, but multi-PDU `ReadBytesAsync`/`WriteBytesAsync` enqueue
      each PDU separately, so a read can slip a PDU between a recipe write's PDUs. The gate
      therefore provides multi-PDU atomicity (defense-in-depth), not a missing socket lock.
- [x] If not serialized: implement `TransportSerializer` (a `SemaphoreSlim(1,1)` with
      `RunAsync(Func<Task>, ct)` / `RunAsync<T>(Func<Task<T>>, ct)`; `WaitAsync(ct)` outside
      the try, `Release()` in `finally`).
- [x] In `S7Driver`, route every `ReadBytesAsync` and `WriteBytesAsync` round-trip through
      the `TransportSerializer` (this covers both the `IS7Transport` and `IS7Driver`
      consumers, since both resolve to the one `S7Driver` instance). Keep the existing
      `ct.ThrowIfCancellationRequested()` semantics; decide whether the pre-check sits
      before or after the gate wait and document it.
- [x] Dispose the `SemaphoreSlim` with the driver; ensure the cancellation path never leaks
      or over-releases a slot.
- [x] Write tests for `TransportSerializer`: concurrent ops never overlap (max concurrency
      == 1 via a probe delegate); a wait canceled before entry does not `Release`; a throwing
      op still releases the gate.
- [x] Run tests — must pass before next task.

#### Task 7: PR2 close-out

- [x] `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` — full suite green.
- [x] `dotnet format SemiStep/SemiStep.slnx`.
- [x] Confirm PR2 scope contains only transport-serialization changes.

### Task 8: Verify acceptance criteria

- [x] Second instance enabling sync against the same PLC is refused with an owner-info
      message and remains a local editor (covered by ownership tests).
- [x] Lease released on disable / failed handshake / dispose (no orphaned lock between
      runs of the same process); double-release and double-dispose are safe.
- [x] Cross-user case (inaccessible lock file) degrades to a clean refusal, not a crash.
- [x] All transport round-trips (monitor read, keep-alive read, reader paths, sync write)
      are serialized within one process (covered by `TransportSerializer` tests).
- [x] Full test suite green; `dotnet format` clean.

### Task 9: Documentation and close-out

- [x] Update `Docs/*` user guide if the "PLC busy in another instance" behavior is
      user-visible and warrants a note. Evaluated — no separate guide prose added: the
      behavior is surfaced directly to the operator by the in-app refusal message
      ("PLC sync is owned by another instance ..."), which is self-explanatory.
- [x] Update `CLAUDE.md` only if a genuinely new, reusable pattern emerged. No change —
      `CLAUDE.md` is an entry document (project rule: no specifics there); no new convention
      warranted an edit.
- [x] Move this plan to `Docs/plans/completed/`. Done in this commit via `git mv`.

## Post-Completion

*Manual / external — informational only.*

**Manual verification:**
- Launch two instances on one machine pointed at the same PLC. Enable sync in instance A;
  attempt to enable sync in instance B — B is refused with the owner-info message and stays
  a local editor. Disable sync in A; B can now enable sync. Kill A while it holds sync
  (Task Manager) — B can enable sync without reboot/manual cleanup (crash-safety).
- If feasible, two different Windows users on one machine: only one can hold sync
  (validates the machine-wide `%ProgramData%` scope and ACL).

**External provisioning (only if runtime ACL proves insufficient):**
- Provision `%ProgramData%\SemiStep\locks` with a Users:Modify ACL at install time so all
  accounts can open the lock file.

**Out of scope (by design):**
- Two **different physical machines** syncing one PLC — a local file lock cannot coordinate
  across machines; that would require PLC-side arbitration. Recorded as a known limitation.
- Making the multi-round-trip write transaction atomic w.r.t. concurrent reads — not needed
  (PR1 handles cross-process; the `committed` gate makes mid-transaction reads bail safely).
