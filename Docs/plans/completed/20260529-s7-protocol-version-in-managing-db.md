# S7 Protocol Version Field in the Managing DB

## Overview

GitHub issue #13: the S7 recipe protocol carries no version marker, so PC and PLC cannot
detect an incompatible on-wire format — a future layout change would be read as garbage with
no signal. This plan adds a **protocol version** field so a build refuses to talk to a PLC
speaking a different version.

Decision (pre-v1.0.0, breaking layout change is acceptable):
- Version field at the **front of the Managing DB** (`VersionOffset = 0`); `Committed`/`RecipeLines`
  shift down. The version is the one field whose offset must never move (a future build reads it first
  to decide how to parse the rest), so offset 0 is the permanent anchor.
- **Int32 (DWORD) big-endian** value.
- **Model A — the PLC firmware owns the version.** It initializes the DWORD to its protocol version and
  never changes it at runtime. The PC **never writes** it.
- **Connect-time handshake.** On `EnableSync`, after `ConnectAsync` succeeds and **before any write**, the
  PC reads the version and compares it to `ProtocolConstants.ProtocolVersion`. On mismatch it clears the
  sync flag, disconnects, and returns a typed `ProtocolVersionMismatchError`.

## Context (from discovery)

Current `ManagingDbLayout.Default`: `DbNumber=2`, `CommittedOffset=0`, `RecipeLinesOffset=2`, `TotalSize=6`.

**Interface layering (verified — important):** `IS7Reader` is implemented by **`S7Service`**
(`S7Service.cs:18` — `: IS7Connection, IS7Reader, IS7ExecutionStream`), which **delegates** to the plain
internal `PlcTransactionExecutor` (e.g. `ReadManagingAreaAsync` at `S7Service.cs:117`). So adding
`ReadProtocolVersionAsync` to `IS7Reader` requires: the executor method **and** a delegating override in
`S7Service`. `StubS7Service` (`SemiStep.Tests/Helpers/StubS7Service.cs`) also implements `IS7Reader`
directly and needs the method too.

**Connect path (verified):** `PlcLifecycleManager.EnableSync` (`PlcLifecycleManager.cs:96-116`) sets the
sync flag, then `await _connection.ConnectAsync(...)`, inside a try/catch that only handles *exceptions* and
otherwise `return Result.Ok()`. `S7Service.ConnectAsync` sets `State=Connected` before returning
(`S7Service.cs:174-175`) and the executor reads guard on `_transport.IsConnected` (`:190`), so a
post-`ConnectAsync` version read finds the transport connected — no layering blocker. `PlcLifecycleManager`
holds `_reader` (`IS7Reader`), `_connection`, `_syncService`. `OnConnectionStateChanged` (`:202`) calls
`_syncService.Reset()` on a Disconnected event while sync is enabled — so the handshake-failure path must
`SetSyncEnabled(false)` **before** `DisconnectAsync()`.

Files/components involved (verified):
- `SemiStep/SemiStep.Core/Plc/S7/Protocol/ProtocolConstants.cs` — add `ProtocolVersion`.
- `SemiStep/SemiStep.Core/Plc/S7/Protocol/NotConnectedError.cs` — error to mirror (`sealed class : Error(message)`).
- `SemiStep/SemiStep.Core/Plc/Configuration/Memory/ManagingDbLayout.cs` — record + `Default`; add `VersionOffset`.
- `SemiStep/SemiStep.Core/Configuration/Dto/ConnectionDto.cs` + `Configuration/Mapping/ConnectionMapper.cs` — add/wire version offset.
- `SemiStep/SemiStep.Core/Plc/Configuration/PlcConfigurationValidator.cs` — `ValidateManagingDb` (pairwise overlap is 2-arg; restructure to a fields array like `ValidateExecutionDb` at `:80`).
- `SemiStep/SemiStep.Core/Plc/S7/Serialization/ManagingAreaCodec.cs` — `Decode` unchanged re version (only its length guard grows); `EncodePcData` unchanged.
- `SemiStep/SemiStep.Core/Plc/IS7Reader.cs`, `S7/S7Service.cs`, `Sync/PlcTransactionExecutor.cs` — add `ReadProtocolVersionAsync`; partial managing write.
- `SemiStep/SemiStep.Core/Plc/PlcLifecycleManager.cs` — `EnableSync` handshake.

Data records (`ManagingAreaPcData`, `PlcManagingAreaState`) intentionally **unchanged**.

**Breakage inventory (verified):**
- **Positional `new ManagingDbLayout(...)`** shift on the new 2nd ctor param: `SemiStep.Tests/S7/ManagingAreaCodecTests.cs:185`, `Core/Configuration/PlcConfigurationValidatorTests.cs:42,146,210`.
- **Pinned config offsets**:
  - `SemiStep.Tests/YamlConfigs/Standard/connection/connection.yaml:17-20` → new offsets/size + version offset.
  - `SemiStep.Tests/YamlConfigs/Invalid/BrokenManagingDbLayout/connection/connection.yaml` — currently `committed_offset:0, recipe_lines_offset:4, total_size:4`; with `version_offset` defaulting to 0 it now ALSO fails on version overlap + size, so the asserted error may change. Set the fixture's `version_offset` to a valid value (so its originally-intended break — RecipeLines not fitting — remains the asserted reason) and re-check the assertion.
- **Managing block size**: `Decode` length guard grows to 10; hand-sized `new byte[6]` blocks must grow.
- **Connect-handshake doubles** (must return version 1, else `EnableSync` fails): `StubS7Service.cs` (`IS7Reader` impl) and, for real-`S7Service` paths, the transport at `VersionOffset` — `SemiStep.Tests/S7/Helpers/FakeS7Transport.cs` (`ReadBytesAsync` returns zero-filled `new byte[count]` for unregistered reads), `FakeS7Driver.cs`, `StubS7ServiceForSync.cs`, and `SetReadResponseForDb` seeds in `PlcSyncCoordinatorTests.cs`. **`SemiStep.Tests/Domain/PlcLifecycleManagerReconnectTests.cs` calls `EnableSync` in ~6 tests** asserting success — all need version 1 seeded.
- **Partial managing write**: `PlcTransactionExecutorTests` assertions on the managing write offset/length change (writes from `CommittedOffset`, not 0). Confirmed the only offset-0 managing write is `WriteManagingAreaAsync` (`:257`); the keep-alive read at `S7Service.cs:229` only reads 1 byte.

Convention notes (verified): SDK-style implicit globbing — no `<Compile Include>`. `internal const` reachable
from `PlcLifecycleManager` (same assembly). FluentResults `IError`; big-endian via `BinaryPrimitives`.

## Development Approach

- **Testing approach**: Regular (code first, then tests within the same task).
- Each task leaves the build green and the **full** suite passing. The layout/ctor change (Task 2) and the
  interface addition + handshake (Task 4) each bundle the fixtures they break.
- Build: `dotnet build SemiStep/SemiStep.slnx`. Test: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`.
- `dotnet format` before commit.

## Testing Strategy

- **Unit**: codec round-trip at new offsets + short-block length fail; validator flags version overlap/size/negativity and the new `Default` validates clean; `ReadProtocolVersionAsync` returns the BE int; `WriteManagingAreaAsync` leaves bytes 0-3 untouched.
- **Handshake**: `EnableSync` proceeds on matching version; on mismatch fails with `ProtocolVersionMismatchError`, disconnects, leaves sync disabled, performs no write.
- **Integration**: `Standard` loads/validates clean; `Invalid/BrokenManagingDbLayout` still errors (assertion re-checked).
- **No e2e**: manual PLC verification in Post-Completion.

## Progress Tracking

- mark `[x]` immediately; ➕ for new tasks; ⚠️ for blockers.

## Solution Overview

New `Default`: `VersionOffset=0` (bytes 0-3), `CommittedOffset=4` (1-byte flag, slot 4-5), `RecipeLinesOffset=6`
(bytes 6-9), `TotalSize=10`.

- `ProtocolConstants.ProtocolVersion = 1`.
- **Read/validate:** `ReadProtocolVersionAsync` on `IS7Reader` (implemented in `S7Service` delegating to the
  executor) reads 4 bytes at `VersionOffset`. `EnableSync` calls it after `ConnectAsync`; on failure/mismatch it
  `SetSyncEnabled(false)`, `DisconnectAsync()`, and returns `ProtocolVersionMismatchError` — before any write.
- **Write (preserve version):** `WriteManagingAreaAsync` writes only `bytes[CommittedOffset..]` at DB offset
  `CommittedOffset`; `EncodePcData` still builds the full buffer (bytes 0-3 stay zero and are never sent).
- **Decode:** unchanged re version; only its length guard grows to `TotalSize`.

## Technical Details

`ManagingDbLayout`:
```
record ManagingDbLayout(int DbNumber, int VersionOffset, int CommittedOffset, int RecipeLinesOffset, int TotalSize)
Default => new(DbNumber: 2, VersionOffset: 0, CommittedOffset: 4, RecipeLinesOffset: 6, TotalSize: 10)
```

`EnableSync` (revised flow — explicit Result branch; sync-flag cleared before disconnect):
```
_syncService.SetSyncEnabled(true);
await _connection.ConnectAsync(config.Connection);              // existing, inside try/catch

var versionResult = await _reader.ReadProtocolVersionAsync();   // NEW, after connect, before any write
if (versionResult.IsFailed || versionResult.Value != ProtocolConstants.ProtocolVersion)
{
    _syncService.SetSyncEnabled(false);                         // BEFORE disconnect (avoids spurious Reset)
    await _connection.DisconnectAsync();
    return versionResult.IsFailed
        ? versionResult.ToResult()
        : Result.Fail(new ProtocolVersionMismatchError(ProtocolConstants.ProtocolVersion, versionResult.Value));
}
return Result.Ok();
```

`WriteManagingAreaAsync`: `var bytes = _managingCodec.EncodePcData(data); var start = _layout.ManagingDb.CommittedOffset;`
`await _transport.WriteBytesAsync(_layout.ManagingDb.DbNumber, start, bytes[start..], ct);`

`ValidateManagingDb`: restructure to a fields array `[(Version,off,4),(Committed,off,1),(RecipeLines,off,4)]`;
non-negative + `ValidateOffsetFits(TotalSize,...)` per field; pairwise `ValidateNoOverlap` over all three pairs
(mirroring `ValidateExecutionDb`).

## What Goes Where

- **Implementation Steps**: code + tests + config + docs in this repo.
- **Post-Completion**: PLC firmware adopts the layout AND initializes the version DWORD to 1; manual verification.

## Implementation Steps

### Task 1: ProtocolVersion constant + mismatch error

**Files:**
- Modify: `SemiStep/SemiStep.Core/Plc/S7/Protocol/ProtocolConstants.cs`
- Create: `SemiStep/SemiStep.Core/Plc/S7/Protocol/ProtocolVersionMismatchError.cs`

- [x] add `public const int ProtocolVersion = 1;`
- [x] create `ProtocolVersionMismatchError : Error` (`int expected, int actual`, descriptive message), mirroring `NotConnectedError`
- [x] build — clean

### Task 2: Shift the layout and fix layout-dependent fixtures (full suite green)

**Files:**
- Modify: `ManagingDbLayout.cs`, `ConnectionDto.cs`, `ConnectionMapper.cs`, `ManagingAreaCodec.cs` (length guard only)
- Modify: `SemiStep.Tests/S7/ManagingAreaCodecTests.cs` (positional ctor + new offsets)
- Modify: `SemiStep.Tests/Core/Configuration/PlcConfigurationValidatorTests.cs` (positional ctor calls)
- Modify: `SemiStep.Tests/YamlConfigs/Standard/connection/connection.yaml`, `Invalid/BrokenManagingDbLayout/connection/connection.yaml`

- [x] add `VersionOffset` as the 2nd `ManagingDbLayout` ctor param; update `Default` (`0/4/6/10`)
- [x] add nullable `ManagingDbVersionOffset` to `ConnectionDto`; wire in `ConnectionMapper.MapManagingDb`
- [x] grow `Decode`'s length guard to the new `TotalSize` (no version logic)
- [x] rewrite positional `new ManagingDbLayout(...)` (codec test :185; validator tests :42/:146/:210)
- [x] update `Standard` YAML offsets/size + version offset; set `Invalid/BrokenManagingDbLayout` `version_offset` to a valid value so its intended break (RecipeLines/TotalSize) stays the asserted reason; re-check the assertion message
- [x] codec tests: round-trip Committed/RecipeLines at new offsets; short block → length failure
- [x] `dotnet build` + **full** `dotnet test` — green

### Task 3: Validate the version field in PlcConfigurationValidator

**Files:**
- Modify: `PlcConfigurationValidator.cs`, `PlcConfigurationValidatorTests.cs`, `Config/Integration/Validation/PlcLayoutValidationTests.cs`

- [x] restructure `ValidateManagingDb` to a 3-field array (Version 4B, Committed 1B, RecipeLines 4B): non-negative + fits-in-`TotalSize` per field + all-pairs `ValidateNoOverlap`
- [x] tests: version overlapping Committed/RecipeLines fails; too-small `TotalSize` fails; new `Default` validates clean
- [x] audit existing overlap/happy-path assertions (:42/:146/:210)
- [x] `dotnet test --filter "FullyQualifiedName~PlcConfigurationValidator | FullyQualifiedName~PlcLayoutValidation"` — green

### Task 4: Version handshake at connect + version-preserving write

**Files:**
- Modify: `SemiStep/SemiStep.Core/Plc/IS7Reader.cs` (add method), `S7/S7Service.cs` (delegating override, mirror `ReadManagingAreaAsync`), `Sync/PlcTransactionExecutor.cs` (impl + partial write), `PlcLifecycleManager.cs` (handshake)
- Modify: `SemiStep.Tests/Helpers/StubS7Service.cs` (implement `ReadProtocolVersionAsync`, default `Result.Ok(1)` + override hook)
- Modify: `SemiStep.Tests/Domain/PlcLifecycleManagerReconnectTests.cs` (the ~6 `EnableSync` tests — ensure the stub returns version 1)
- Modify: `SemiStep.Tests/S7/Helpers/FakeS7Transport.cs`, `FakeS7Driver.cs`, `StubS7ServiceForSync.cs` (default version 1 at `VersionOffset`), `SemiStep.Tests/S7/PlcSyncCoordinatorTests.cs` (`SetReadResponseForDb` seeds), `PlcTransactionExecutorTests.cs`

- [x] add `Task<Result<int>> ReadProtocolVersionAsync()` to `IS7Reader`; implement in `PlcTransactionExecutor` (read 4 bytes at `VersionOffset`, BE int); add the delegating override in `S7Service`
- [x] change `WriteManagingAreaAsync` to write `bytes[CommittedOffset..]` at DB offset `CommittedOffset`
- [x] `EnableSync`: insert the handshake after `ConnectAsync` per Technical Details (explicit Result branch; `SetSyncEnabled(false)` before `DisconnectAsync`)
- [x] add the interface method to `StubS7Service` (default 1) and update all transport/reader doubles + `SetReadResponseForDb` managing seeds to supply version 1 at `VersionOffset`
- [x] tests: `ReadProtocolVersionAsync` returns the value; managing write does not touch bytes 0-3; `EnableSync` proceeds on match; `EnableSync` fails with `ProtocolVersionMismatchError`, disconnects, sync stays disabled, no write occurs; the reconnect tests still pass
- [x] `dotnet test --filter "Component=S7"` + full suite — green

### Task 5: Documentation

**Files:**
- Modify: the `Docs/` protocol/data-model doc; `CLAUDE.md` (only if a one-liner is warranted)

- [x] document: version DWORD at the front of the Managing DB, firmware-owned, validated by a connect-time handshake (fail-loud, before any write), PC never writes it
- [x] full suite green

### Task 6: Verify acceptance criteria
- [x] PLC owns the version; PC validates once at connect and refuses (no write) on mismatch — confirmed: `PlcLifecycleManager.EnableSync` (`PlcLifecycleManager.cs:116-124`) reads version after `ConnectAsync`, and on mismatch clears sync + disconnects + returns `ProtocolVersionMismatchError` before any write; covered by `EnableSync_WhenProtocolVersionMismatches_FailsAndDisconnectsAndLeavesSyncDisabled` and `EnableSync_WhenVersionReadFails_FailsAndDisconnects` (`PlcLifecycleManagerReconnectTests.cs:244,261`)
- [x] PC never overwrites the version DWORD (managing write starts at `CommittedOffset`) — confirmed: `WriteManagingAreaAsync` (`PlcTransactionExecutor.cs:281-282`) writes `bytes[CommittedOffset..]` at offset `CommittedOffset`; asserted by `WriteRecipeWithRetryAsync_NeverWritesManagingAreaBytesZeroToThree` (`PlcTransactionExecutorTests.cs:312`)
- [x] `Default` layout, validator, `Standard` config agree; override path works — confirmed: `ManagingDbLayout.Default` is `0/4/6/10` (`ManagingDbLayout.cs:10-15`), `PlcConfigurationValidator.ValidateManagingDb` validates the version field (`PlcConfigurationValidator.cs:31`, tests at `PlcConfigurationValidatorTests.cs:273,295`), `Standard/connection.yaml` matches `0/4/6/10` and loads clean (integration tests in `ConfigLoadingTests.cs`); override path wired via `ConnectionMapper.cs:73`
- [x] full suite + `dotnet format SemiStep/SemiStep.slnx` — confirmed: 684 passed / 0 failed; `dotnet format` reformatted nothing

### Task 7: Plan archival
- [x] move this plan to `Docs/plans/completed/`

## Post-Completion
*Manual / external — informational only*

**PLC-side:** the S7 program must adopt the identical Managing-DB layout (version DWORD at offset 0,
`Committed`/`RecipeLines` shifted) AND initialize the version DWORD to 1. Until then `EnableSync` fails the handshake.

**Manual verification:** connect and confirm the handshake passes; set a wrong version on the PLC and confirm
`EnableSync` fails before writing; confirm a recipe write leaves the version DWORD intact.
