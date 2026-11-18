## The failure

Recipe rows have no fixed size on the wire. Any PLC program that indexes DB997/DB998 by row number
reads wrong values for every step after the first short one — silently. The misread value is in
range and plausible: a 10-second step duration read as a 10 sccm argon flow. Every check on the PC
side compares the PLC against the PC's own serialization, so sync stays green and no log line
appears.

This issue replaces the layout: fixed-width rows, one declared slot per column, protocol version 2.

## Terms

| term | meaning |
| --- | --- |
| **row** | one recipe step, as written to the PLC |
| **slot** | one element position inside a row, inside one array |
| **`r`** | row number, 0-based; equal to the step number |
| **`s`** | slot number of a column; declared in config, fixed for that column forever |
| **`N`** | slots reserved per row in DB997 and DB998; config key `row_slots` |
| **`STR_N`** | slots reserved per row in DB999; config key `string_slots_per_row` |
| **`capacity`** | declared element count of a data array; owned by the PLC |
| **`current_size`** | elements currently in use in a data array |
| **`recipe_lines`** | number of steps in the current recipe; DB995 |
| **`committed`** | flag saying the recipe in the data blocks is complete and consistent; DB995 |

Counts are always in **elements**, never bytes.

## What is wrong today

`RecipeConverter.SerialiseStep` (`SemiStep.Core/Plc/S7/Serialization/RecipeConverter.cs:66`) writes
the action id to the int list, then walks *the step's own action* column list and appends each value
to whichever of the three lists matches the column's `system_type`. Nothing reserves space; values
are appended one after another.

Two independent problems follow.

**Rows have different sizes.** An action whose resolved column list has 15 float columns appends 15
floats. An action with 1 appends 1.

**A column's position depends on the action.** `step_duration` is the 9th float of
`200 Стабилизация газа` and the 15th float of `300 Травление`.

Making every row the same size fixes the first and leaves the second.

> **Resolved column lists.** Every count in this document uses the *resolved* column list produced by
> `ActionTreeResolver`, which splices subaction columns into the parent action at the selector site
> (see `Docs/architecture/nested-actions.md`). Action 300 declares 11 float columns in
> `ConfigFiles/RIE/actions/process.yaml`; its resolved list has 15, after subactions 3002 and 3003
> contribute `icp_load`, `icp_tune`, `rie_load`, `rie_tune`. Counting columns in the yaml alone will
> not reproduce these numbers.

### Worked example

Three-step RIE recipe `[300 Травление, 10 Пауза по времени, 300 Травление]`.

| row | action | ints written | floats written | strings written |
| --- | --- | --- | --- | --- |
| 0 | 300 | 4 | 15 | 1 |
| 1 | 10 | 1 | 1 | 1 |
| 2 | 300 | 4 | 15 | 1 |

The float array ends up 31 elements long. DB998 `current_size` on the reference tool reads
`16#0000_001F` = 31 in a TIA watch table, confirming this is what actually reaches the PLC.

Where each row starts:

| row | first int | first float |
| --- | --- | --- |
| 0 | 0 | 0 |
| 1 | 4 | 15 |
| 2 | 5 | 16 |

The two columns of that table do not advance together, and neither can be computed from the row
number.

### What it costs

Take a PLC program that assumes a fixed row size, sized from the full row (4 ints, 15 floats), and
reads each row's values packed from `r × width`:

- row 0 — correct
- row 1 — correct, but only because row 0 happened to be a full row
- row 2 — reads float index 30, which holds step 2's `step_duration` of `10.0`, as `ar`

A two-step test recipe passes. Drift begins after the first short row and accumulates.

### Why nothing detects it

- `DeserialiseStep` mirrors `SerialiseStep` exactly, so the app reads back its own data perfectly.
- `PlcTransactionExecutor.VerifyWriteAsync` compares the read-back against the app's **own** expected
  arrays. That checks the transport, not the contract. It passes.
- Sync goes green. No exception, no failed `Result`, no log line.

## The wire format

This section is the contract. It is sufficient to write the PLC side without reading any C#.

### Data blocks

All five blocks must be created with **standard (non-optimized) block access**. Optimized access —
the TIA default — has no stable byte offsets and will not work.

**DB995, managing block.** Byte offsets, total size 10.

| offset | field | type | written by |
| --- | --- | --- | --- |
| 0 | `version` | DInt | PLC only |
| 4 | `committed` | Byte | PC |
| 6 | `recipe_lines` | DInt | PC |

**DB997, DB998, DB999, data blocks.** Same header shape, different element types.

| offset | field | type |
| --- | --- | --- |
| 0 | `capacity` | UDInt — declared element count of `data` |
| 4 | `current_size` | UDInt — elements in use |
| 8 | `data` | array, see below |

| block | element type | element size |
| --- | --- | --- |
| DB997 | **`DInt`** | 4 bytes |
| DB998 | **`Real`** | 4 bytes |
| DB999 | **`WString[64]`** | 132 bytes — `UInt` max chars, `UInt` actual chars, 64 UTF-16 chars |

All multi-byte values are big-endian, S7 native. Note DB997 is `DInt`, **not** `Int` — the elements
are 4 bytes, not 2.

`capacity` is owned by the PLC and must be left alone by the PC. `current_size` is written by the PC
and, under this layout, counts **reserved** slots including filler: `recipe_lines × N` for DB997 and
DB998, `recipe_lines × STR_N` for DB999.

### Addressing

> A column declares one slot number `s`. Its value lives at slot `s` of its row, in the array chosen
> by the column's `system_type`.

| `system_type` | address |
| --- | --- |
| `int` | `DB997.data[r * N + s]` |
| `float` | `DB998.data[r * N + s]` |
| `string` | `DB999.data[r * STR_N + s]` |

`s` never depends on the action. It is the same number in every row of every recipe.

### Two slot-number spaces

`int` and `float` **share** one space of width `N`. A slot number belongs to exactly one column. If
that column is float, DB998 holds the value at that slot and DB997 holds `0` in the same slot; if
int, the reverse.

```
columns:  s0 action(int)  s1 float  s2 float  s3 float  s4 int  s5 float  s6 float
N = 7

DB997 row r:  [ act,  0,    0,    0,    val,  0,    0   ]
DB998 row r:  [ 0.0,  val,  val,  val,  0.0,  val,  val ]
                 ^ both arrays reserve all 7 slots; each slot is real in exactly one of them
```

Reserving each slot in both arrays wastes 4 bytes per slot. What it buys is that a column's slot
number does not depend on its type — the PLC computes one index and picks the data block from the
column's declared type. Adding a float column does not shift any int column.

`string` has its **own** space of width `STR_N`. Same rule, different width. It is a separate space
because a WString element is 132 bytes, 33× an int or float slot: putting `comment` into the shared
space would make the string array `132 × 20` = 2640 B per row for RIE, capping a 64 KB data block at
24 steps. A 100-step recipe would need 264 KB. The rule is still "each array reserves a fixed width
per row"; the arrays simply do not all have the same width.

Because the spaces are separate, slot numbers repeat across them. `action` is slot 0 in the shared
space and `comment` is slot 0 in the string space. That is legal and expected.

### Slot 0 of the shared space is the action id

The `action` column is declared in `columns.yaml` with `wire_slot: 0`, and the validator enforces
that it is 0. The serializer routes `Step.ActionKey` through the same slot map as every other column
rather than hardcoding a position, but the resulting slot is fixed by contract.

### Filler is not a marker

An unused slot holds `0` / `0.0` / an empty string. `0` is also a legitimate value — a closed gas
flow, a zero setpoint. The PLC must not infer from the filler whether a column applies to a row; it
reads the action id in slot 0 for that. This layout provides addressing, not applicability.

### Write sequence and stale rows

The PC writes in this order, so the PLC never sees a half-written recipe:

1. `committed` = false, `recipe_lines` = 0
2. DB997, DB998, DB999 data
3. `recipe_lines` = step count
4. `committed` = true

The PLC must treat the recipe as valid only while `committed` is true, and must never read rows at or
beyond `recipe_lines` — those hold leftovers from a previous, longer recipe.

### Consistency check before `committed` is honoured

```
DB997.current_size = recipe_lines * N
DB998.current_size = recipe_lines * N
DB999.current_size = recipe_lines * STR_N
```

Any mismatch means the PC and the PLC disagree about the layout.

## Config schema

One new field per column in `columns/columns.yaml`.

```yaml
gate_setpoint:
  column_type: property_field
  ui:
    ui_name: "Шибер зад."
  business_logic:
    property_type_id: gate_setpoint
    read_only: false
    save_to_csv: true
    wire_slot: 9

comment:
  column_type: text_field
  ui:
    ui_name: "Комментарий"
  business_logic:
    property_type_id: string
    read_only: false
    save_to_csv: true
    wire_slot: 0        # string space, independent of the shared space
```

The field is named `wire_slot`, not `plc_slot`, because `connection.yaml` already uses `plc_slot` for
the S7 CPU slot of the endpoint (`ConnectionDto.PlcSlot`). Two unrelated meanings under one name in
the same config folder would be a trap.

`wire_slot` is required for every column any action uses; the validator fails load otherwise. Columns
no action references — such as `step_start_time`, which is computed and never serialized — omit it.

Which array a column lands in is **derived** from `system_type`, never declared, so the two cannot
disagree.

Row widths are declared in `connection/connection.yaml`, because they are the contract with the PLC:

```yaml
# -- Recipe row layout --
row_slots: 24             # N — reserved per row in DB997 and DB998
string_slots_per_row: 1   # STR_N — reserved per row in DB999
```

**Slot numbers must be declared, not taken from file order.** If position in the file decided the
slot, inserting a column in the middle would shift every column after it and the PLC would silently
decode garbage. Declared numbers let a new column take an unused slot while nothing else moves. For
the same reason, `N` should be frozen with spare slots above today's count.

## Memory

Per row, with `N` set to exactly today's column count:

| config | int cols | float cols | string cols | N | row size | size today |
| --- | --- | --- | --- | --- | --- | --- |
| RIE | 4 | 15 | 1 | 19 | **284 B** | 136–208 B |
| MBE | 2 | 4 | 1 | 6 | **180 B** | 136–156 B |
| MOCVD | 3 | 6 | 1 | 9 | **204 B** | 136–168 B |

Spare slots cost real memory. The reference tool is a CPU 1214C (identified from the TIA Portal
project in the field report), which has 100 KB of work memory for program and data combined. RIE,
200-step recipe, all three data blocks:

| N | row size | DB997 | DB998 | DB999 | total |
| --- | --- | --- | --- | --- | --- |
| 19 (no spare) | 284 B | 14.9 KB | 14.9 KB | 25.8 KB | 55.6 KB |
| 24 (5 spare) | 324 B | 18.8 KB | 18.8 KB | 25.8 KB | 63.4 KB |
| 32 | 388 B | 25.0 KB | 25.0 KB | 25.8 KB | 75.8 KB |

`N = 24` is the recommendation, subject to a real PLC memory audit.

At `max_length: 64` a WString slot is 132 B — 46% of an RIE row, for a comment. At `32` it is 68 B:
the RIE row drops to 220 B and DB999 for 200 steps drops from 25.8 KB to 13.3 KB. Step ceiling on a
64 KB data block is set by DB999 in every variant: `(65536-8)/132` = **496 rows**.

## Load-time validation

Fail config load when:

1. A column used by any action has no `wire_slot`.
2. Two non-string columns share a `wire_slot` — int and float share one space precisely so this is a
   collision.
3. A `wire_slot` falls outside `0 .. row_slots-1`.
4. The `action` column's `wire_slot` is not 0, or `action` is not `system_type: int`.
5. Two string columns share a slot, or a string slot falls outside `0 .. string_slots_per_row-1`.
6. The highest declared slot plus one exceeds its space's declared width. **This is the drift gate** —
   editing a column without updating the declared contract fails at startup instead of silently
   reshaping the wire.
7. The same column key is reachable from different root actions with a different `system_type`.
   `ActionTreeResolver` today checks `property_type_id` conflicts only within one root
   (`ActionTreeResolver.cs:164`), and MBE legitimately varies `property_type_id` across roots, so this
   check must be on `system_type`.

A column that declares a `wire_slot` but is used by no action is a warning, not a failure — it
reserves a slot for planned hardware.

At connect time, **the app** verifies each data block's `capacity` is a whole multiple of its row
width and large enough for the intended maximum step count, and refuses to enable sync otherwise.

## Protocol version

Bump `ProtocolConstants.ProtocolVersion` from 1 to 2.

The handshake already handles this. `PlcLifecycleManager.ValidateProtocolVersionAsync`
(`SemiStep.Core/Plc/PlcLifecycleManager.cs:154`) reads DB995 offset 0 when sync is enabled and on
reconnect; on mismatch `FailProtocolVersionHandshakeAsync` rolls back sync and disconnects before a
single recipe byte is written. The version field is owned by the PLC and never written by the app.

New app against an old PLC program: refused. Old app against an updated PLC: refused. Deployment has
to be coordinated per tool, which is the correct behaviour for a change of this kind.

## Scope note: the step count on the read path

`PlcTransactionExecutor.ReadRecipeDataAsync` currently returns `StepCount: intCount`
(`PlcTransactionExecutor.cs:101`) — the number of elements in the int array, not the number of steps.
The new reader cannot work with that, so **this issue fixes it**: the row count comes from DB995
`recipe_lines`, which `ManagingAreaCodec` already decodes and nothing currently uses.

This is also a live bug on its own today. See adjacent defect 1 below; the separate issue for it
should reference this one.

## Work list

| file | change |
| --- | --- |
| `SemiStep.Core/Plc/S7/Serialization/RecipeConverter.cs` | replace the append-walk in `SerialiseStep` (:66) and `DeserialiseStep` (:93) with slot addressing over pre-filled fixed-size rows |
| new `SemiStep.Core/Plc/S7/Serialization/PlcSlotMap.cs` | column key → (type, slot) plus the two row widths; built once from the registry, injected into `RecipeConverter` |
| `SemiStep.Core/Plc/Sync/PlcTransactionExecutor.cs` | derive the row count from `recipe_lines` (:101); write the data blocks from `CurrentSizeOffset` instead of offset 0 so `capacity` is preserved (:254-256); read `capacity` and check the recipe fits before writing |
| `SemiStep.Core/Plc/S7/Serialization/ArrayCodec.cs` | stop emitting the `capacity` field (:78, :95, :113); it is a pure codec, the offset change lives in the executor above |
| `SemiStep.Core/Configuration/Dto/`, `Mapping/ColumnMapper.cs`, `Loaders/ColumnsSectionLoader.cs`, `Recipes/GridColumnDefinition.cs` | carry `wire_slot` |
| `SemiStep.Core/Configuration/Dto/ConnectionDto.cs`, `Mapping/ConnectionMapper.cs` | carry `row_slots`, `string_slots_per_row` |
| new validator in `SemiStep.Core/Configuration/Validation/` | the rules above, wired next to `CrossReferenceValidator` |
| `SemiStep.Core/Plc/S7/Protocol/ProtocolConstants.cs` | `ProtocolVersion = 2` |
| `ConfigFiles/{RIE,MBE,MOCVD}/columns/columns.yaml`, `connection/connection.yaml` | slot numbers and row widths |
| `SemiStep.Tests/S7/RieEtchManualBranchSerializationTests.cs` | golden layout rewrite |
| new tests | round-trip over a recipe mixing wide and narrow actions; validator cases under `SemiStep.Tests/YamlConfigs/Invalid/` |
| new `Docs/architecture/plc-wire-layout.md` | the wire-format section above, as the standing reference |
| `Docs/architecture/nested-actions.md` | correct the claim at :134 that "the PLC writes values into fixed slots… the layout never shifts" — true only within one action, which is likely why this went unnoticed |

## Adjacent defects found while investigating

1. **The read path uses the wrong step count.** `PlcTransactionExecutor.cs:101` returns
   `StepCount: intCount`. For a 3-step RIE recipe that is 9, so `ToRecipe` loops 9 times over data
   holding 3 steps and fails with "Insufficient int values at step 3". `ReadRecipeFromPlcAsync`
   therefore fails for any recipe containing a step with an int column beyond the action id — on RIE,
   every process step. `ReconcileWithPlcAsync` swallows that failure
   (`PlcLifecycleManager.cs:340-346`) and calls `NotifyLocalRecipe()`, silently pushing the local
   recipe with no conflict dialog. Likely root cause of #158. Fixed as part of this issue, see the
   scope note above; worth its own issue because it is live today.
2. **`ArrayCodec` overwrites the PLC-owned capacity header** on every write, storing the used count
   in `total_capacity_offset`, so the PLC cannot range-check anything.
3. **RIE and MOCVD `For` loops carry no iteration count.** Action 20 declares only `comment`
   (`ConfigFiles/RIE/actions/service.yaml:13`, `ConfigFiles/MOCVD/actions/service.yaml:69`), and
   `LoopParser.ExtractIterationCount` (`Recipes/Analysis/LoopParser.cs:77`) silently defaults to 1,
   despite `for_loop_count1..3` existing in the execution data block. MBE has `task` on its For; the
   other two do not.
4. **All three `connection.yaml` are byte-identical, including the IP `192.168.0.150`.** MBE and
   MOCVD presumably need their own endpoints.

## Open decisions

- Final `N` per config, and how many spare slots.
- Whether `max_length` drops from 64 to 32 in the same change.
- Slot assignment per config, agreed with the PLC programmer. A RIE assignment following the current
  order of `300 Травление` keeps that action byte-identical to the present format, so a known-good
  data block dump can be diffed during migration.
