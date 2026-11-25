# UI status bar redesign

Approved design: `.git/manager/ui/decisions.md` D2, D6 and
"D6 addendum 2" (the last-exchange age source and the 134 px step timer), and the addendum
"Execution slot sizing (2026-11-25)" in
`.git/manager/ui/designer-review.md`. Slot widths below are the
coordinator's revision of that addendum.

## Behavior

- Today the status bar is a row of `Auto` columns. The error/warning button appears only when a
  message exists and pushes everything right. Three texts describe sync ("Синхр. ВКЛ",
  "Синхронизировано", "Синхр.: 7 с назад"). The timers render at 20 px, and "FOR: 0 0 0" shows
  raw PLC counters. Any change of text length moves every item after it.
- After this change the bar only reports. Every item sits in a fixed-width slot, so no item moves
  when a text changes. Left to right: link slot 270 px, separator, execution slot (state and step
  200 px, gap 8, loops 190 px), separator, step timer 134 px, gap 8, recipe timer 154 px,
  flexible gap, message counts 80 px at the right end. Ground `#F7F8FA`, 12 px left and right
  inset, bar height 38 px as today.
- The link slot is one dot plus black text. It replaces the three sync texts. A click opens the
  notification log. Its tooltip names the PLC address and the age of the last successful
  exchange with the PLC.
- The last-exchange age counts from the last successful read from the PLC: the execution
  poll (every `polling_interval_ms`, 100 ms in all presets), the keep-alive and every other read. Today's "Синхр.:" age counts from the last completed
  recipe write instead.
- The execution slot shows the run state, the step "шаг K из N" and a lock glyph while the PLC
  runs the recipe. The loop part shows "цикл 2 из 5 › 1 из 3" while a loop runs. The window
  title gets " [выполняется]".
- The timers render in the bar font and say what they count: "Шаг: осталось 00:04:12",
  "Рецепт: осталось 01:12:40"; idle "Шаг: —", "Рецепт: всего 01:12:40".
- Digits have equal widths, so a counting timer does not jiggle.
- `time_hms` values (hh:mm:ss) lose the trailing "с" in the grid. The status bar already shows
  none. The YAML `units` text stays.
- Secondary chrome text becomes `#6C707E` everywhere. The main window gets `MinWidth` 1280.
- Scope guard: the toolbar and the sync toggle (batch A), cell templates (batch C), the grid
  palette, the running-row tint and auto-scroll (batch D), and the time-left numbers (branch
  `timeleft-loop-pass`) stay as they are. This batch formats the time-left values; it does not
  change how they are calculated. The notification strip (the message panel) stays above the
  status bar. YAML content text is not translated.

## Flow branches

The link state has one precedence, highest first: отключение… > синхронизация выключена >
подключение… > нет связи > ошибка синхронизации > подключено.

| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Disable in flight: the sync toggle command is executing, the sync flag is off, the connection is not yet `Disconnected` (a started write gets up to 5 s) | Hollow grey dot, "ПЛК: отключение…"; ranks above sync off | operator: D2; coordinator decision; `PlcLifecycleManager.DisableSync` clears the sync flag first, then waits, then disconnects | A1 |
| Sync off | No dot, "синхронизация выключена". The dot column stays reserved, so the text starts at the same x in every state | operator: D2; agent: fixed-slot rule | A1, A4 |
| Enable in flight: the command is executing and the sync flag is on (`EnableSync` sets the flag before it connects) | Hollow grey dot, "ПЛК: подключение…"; never "отключение…" | operator: D2; coordinator decision | A1 |
| Sync on, link up | Green dot, "ПЛК: подключено" | operator: D2 | A1 |
| Sync on, link lost, automatic reconnect retries running (command not executing) | Red dot, "нет связи, последний обмен {age}"; not "подключение…", so the slot stays steady while the link is down | coordinator decision | A1, A3 |
| Sync on, no link, no successful exchange this session | Red dot, "нет связи, последний обмен никогда" (lower-case mid-sentence) | operator: D2, D6 addendum 2 | A3 |
| Plain red state without an age | "ПЛК: нет связи" is the text when no age text applies | coordinator decision | A1 |
| A read from the PLC succeeds (execution poll, keep-alive, any other read) | The last-exchange timestamp moves to now; the age restarts in the #168 buckets ("7 с назад", "> 30 с назад", "> 1 мин назад", "> 1 ч назад") | operator: D6 addendum 2 | A12 |
| A read fails, or the link is lost | The timestamp keeps its last value, so the age grows; the no-link text shows the age since the last successful read | operator: D6 addendum 2 | A12, A3 |
| A snapshot with `SyncStatus` `Failed` arrives | Red dot, "ПЛК: ошибка синхронизации"; details stay in the notification log. The latch reads `SyncStatus` from each snapshot in sequence, never a live re-read | operator: D6a; coordinator decision | A2 |
| Error latched, then `Syncing`, `OutOfSync` or a pending reconnect conflict | The error stays | coordinator decision | A2 |
| Error latched, then `Synced` | The error clears; "ПЛК: подключено" | operator: D6a | A2 |
| Error latched, then sync disabled (`Failed`, then `Idle`) | The error clears; the slot follows the disable rows | coordinator decision | A2 |
| Error latched, then the link drops | "нет связи, …" wins by precedence; after reconnect the error shows again until `Synced` | agent: precedence | A2 |
| Connect while the PLC already runs a recipe (`RecipeActiveError` from the sync precheck) | Not a sync error: the slot stays "ПЛК: подключено", the execution slot shows the run, the notification log gets an info-level entry | coordinator decision; `PlcSyncExecutor` returns `RecipeActiveError`, which `PlcSyncCoordinator` turns into `Failed` today | A2 |
| `--plc-endpoint` override differs from the preset in `connection.yaml`, and the state is not "нет связи" | Bar text appends the host (no port): "ПЛК: подключено, 127.0.0.1" | operator: D2; designer: IP without the port | A3 |
| Override in the no-link state | The host is never appended; the age text keeps the room | coordinator decision | A3 |
| No override, or override equal to the preset | Address only in the tooltip | operator: D2 | A3 |
| Pointer on the link slot | Tooltip: "ПЛК: 192.168.0.150:102" and "Последний обмен: 7 с назад" | operator: D2 | A3 |
| Click on the link slot | The notification log opens; a click while it is open keeps it open. With an empty log the click does nothing (`MessagePanelViewModel.ShowPanel` needs entries) | operator: D6e | A5 |
| Recipe not running | Execution slot: "Не выполняется", no lock glyph, loop part empty; title without suffix | operator: D6b | A6, A10 |
| Recipe running, no loop | "▶ Выполняется: шаг 5 из 8" + 16 px lock glyph; loop part empty; title "… [выполняется]"; K = PLC line + 1, N = recipe step count, loop passes do not count | operator: D2 | A6, A10 |
| Pointer on the execution slot | One tooltip: the lock text "Пока ПЛК выполняет рецепт, правка недоступна. Копирование работает." first, then the loop levels "Цикл уровня 1: 2 из 5. Уровня 2: 1 из 3." in the text's order | designer: "What trims"; coordinator decision | A6, A7 |
| Step number with fewer digits than the total | The current number is padded with U+2007 figure spaces to the digit count of the total ("шаг  9 из 12"); same for loop counters | designer: "Digit stability" | A6 |
| Recipe running inside loops | Loop part "цикл 2 из 5 › 1 из 3", innermost first; pass from the PLC counter of that depth, total from the loop's count in the recipe; only levels whose counter is 1 or more | operator: D3 | A7 |
| Loop text wider than 190 px (three-digit counts at three levels) | Trims from the end with an ellipsis, outer levels first; the tooltip names every level | designer: "What trims" | A7 |
| PLC on a loop's own start or end row | That loop counts as running when its counter is 1 or more (`RecipeSnapshot.EnclosingLoops` excludes those rows, so the set is taken inclusive of start and end) | agent: see Caveats | A7, S5 |
| Empty recipe | Timers "Шаг: —", "Рецепт: —" | agent: today's `MissingSnapshotText` | A8 |
| Recipe idle | "Шаг: —", "Рецепт: всего 01:12:40" | operator: D2 | A8 |
| Recipe running | "Шаг: осталось 00:04:12", "Рецепт: осталось 01:12:40"; caption in secondary grey, value in the bar foreground | operator: D2 | A8 |
| Step time left 24 h or more | Value prints as today, "1.01:12:40": "Шаг: осталось 1.01:12:40" measures 132.9 px and fits the 134 px slot | operator: D6 addendum 2 | S6, S9 |
| Recipe time left 24 h or more | "Рецепт: осталось 1.01:12:40" measures 149.1 px in the 154 px slot | agent | S6, S9 |
| No errors or warnings | Message-count slot is empty but keeps its 80 px | designer: "The whole bar at 1280 px" | A4 |
| Errors or warnings exist | Red dot with the error count, amber dot with the warning count; tooltip "Ошибок: 3, Предупреждений: 2"; click toggles the log, as today | coordinator decision; today's "Ошибок: 3 Предупреждений: 2" needs about 200 px | A4, A5 |
| Grid shows a `time_hms` value | "00:10:00", no "с"; column widths measured without it | operator: D6c | A9 |

## Decisions

- Last-exchange timestamp: it lives in `PlcTransactionExecutor`, the one class that every protocol
  read passes through (the execution poll in `PlcExecutionMonitor` and the reads of `S7Service`),
  and the keep-alive's one-byte read in `S7Service` moves behind it. It is stamped after each
  successful read and never on a failure. It reaches the UI through the chain that carries the
  connection state today (`IS7Connection` gains one read-only member, then `PlcLifecycleManager`
  and `RecipeCoordinator`), and the view model reads it on its existing 1 s age tick. No new
  interface. Rejected: `S7Driver.ReadBytesAsync`, a single point too, but a successful read there
  cannot be unit-tested without a PLC (`S7DriverTests` cover only the not-connected path).
  Rejected: a field in `PlcSessionSnapshot`, because one emission per poll would log a state
  change on every poll (`RecipeCoordinator` logs each snapshot at Information).
- `LastSyncTime` loses its only UI reader. It is deleted end to end: `IPlcSyncService`,
  `PlcSyncCoordinator` (the property and its stamp on a completed write), `PlcLifecycleManager`,
  `RecipeCoordinator`, the test stub `StubPlcSyncService`. The `PlcSyncCoordinator` tests that
  used it as a completion signal or asserted it switch to the `Synced` status.
  `LastSyncFreshnessTests` keep their bucket cases and move to the new timestamp.
- Timer fonts: the timers inherit the status-bar font, and the six `status_bar.timer_label_*` /
  `timer_value_*` keys and `status_bar.item_spacing` are deleted (Core record, DTO, both mappers,
  style-editor draft, validation and window, installer keys, presets, tests, docs). Rejected:
  keeping the timer keys at 12 px. A style-editor value of 20 would break the slot widths again,
  and a key with no reader is dead config.
- `status_bar.padding` becomes vertical only (top and bottom, 5 in the presets). The 12 px side
  insets are fixed in the bar layout. The bar gets `MinHeight` 38, today's measured height.
- The link state is one pipeline over the PLC snapshot stream and the sync toggle command's
  `IsExecuting`, with the error latch inside it, ending in `ToProperty`. Rejected: re-reading the
  coordinator's live properties on each snapshot, which misses a `Failed` that a later snapshot
  already replaced.
- Sync dot colours come from palette keys, not literals: `chrome.connected` becomes `#208A3C`,
  `chrome.disconnected` becomes `#DB3B4B`, the hollow dot strokes with
  `chrome.secondary_foreground`. If `chrome.local_mode` and `chrome.connecting` have no reader
  after batch A, they are deleted with the same chain as the timer keys.
- Secondary grey `#6C707E` replaces both current values: `chrome.secondary_foreground` (`#888888`
  in the three presets and in `GridStyleOptions.Default`) and the `#818594` theme keys in
  `App.axaml` (`SemiColorText2`, `MenuItemInputGestureTextForeground`,
  `TextBoxPlaceholderForeground`).
- `status_bar.background` becomes `#F7F8FA` in the three presets and the Core default.
  `#6C707E` on `#F7F8FA` is 4.64:1.
- Link text is black (bar foreground); only the timer captions use the secondary grey.
- The link slot has no text trimming. Every shipped state fits 270 px (see Caveats).
- Equal-width digits: `TextElement.FontFeatures="+tnum"` on the status bar root, plus figure-space
  padding. Rejected: relying on the default font, because the style editor can set any family.
- Drop of "с": `TimeFormatHelper` stops appending units to `time_hms` values. Every display caller
  follows (grid display converter, column width measurement, step start time, transposed cells
  through `ParameterCellViewModel.Units`). Rejected: removing `units` from the YAML, because the
  config owners own that text.
- Obsolete strings (`StatusSyncOn`, `StatusSyncOff`, `StatusConnecting`, `StatusIdle`,
  `StatusSyncing`, `StatusSynced`, `StatusFailed`, `LastSyncPrefix`, `StatusForLabel`) are deleted
  from both resx files when nothing reads them after batch A.
- Title order: "SemiStep - name * [выполняется]".

## Caveats

- `+tnum` in Avalonia 12 with the chrome fonts.
  probed: scratch console app, Avalonia 12.0.5 + Skia + HarfBuzz, headless window with real
  drawing, `TextBlock` at 12 px, 2026-11-25. Results:
  - "Segoe UI Variable": "1111111111" 45.47 px without the feature, 64.69 px with `+tnum`;
    "0000000000" 64.69 px both ways. The feature works.
  - "Segoe UI" and `FontFamily.Default` (resolves to Segoe UI): 64.69 px with and without, so
    tabular by default.
  - U+2007 is exactly one digit wide in both fonts (10 figure spaces = 64.69 px).
  - `TextElement.FontFeatures` set on a parent `Border` reaches a child `TextBlock` in Segoe UI
    Variable (64.69 px).
  - The composite `ChromeFontFamily` "Segoe UI Variable Text, Segoe UI" resolved to Segoe UI.
- Worst-case widths. probed: same app, `FontFamily.Default` (Segoe UI) 12 px, 2026-11-25. Slot
  room is the slot width minus the 8 px dot and 4 px gap where a dot applies.

  | Slot (room) | Worst text | Width |
  |---|---|---|
  | Link 270 (258) | "нет связи, последний обмен > 1 мин назад" | 237.2 (249.2 with dot) |
  | Link | "нет связи, последний обмен никогда" | 203.4 |
  | Link | "ПЛК: подключено, 192.168.100.150" | 190.4 |
  | Link | "ПЛК: ошибка синхронизации" | 160.9 |
  | Link, English | "no link, last exchange > 1 min ago" | 181.4 |
  | State and step 200 | 16 px lock + 4 + "▶ Выполняется: шаг 888 из 888" | 20 + 172.2 = 192.2 |
  | State and step, English | "▶ Running: step 888 of 888" | 145.8 |
  | Loops 190 | "цикл 88 из 88 › 88 из 88 › 88 из 88" | 184.8 |
  | Loops, English | "loop 88 of 88 › 88 of 88 › 88 of 88" | 177.8 |
  | Step timer 134 | "Шаг: осталось 00:04:12" | 123.8 |
  | Step timer, 24 h or more | "Шаг: осталось 1.01:12:40" | 132.9 |
  | Step timer, English | "Step: left 1.01:12:40" | 102.7 |
  | Recipe timer, 24 h or more | "Рецепт: осталось 1.01:12:40" | 149.1 |
  | Recipe timer, English | "Recipe: left 1.01:12:40" | 114.1 |
  | Counts 80 | "● 999 ● 999": two 8 px dots + 4 px gaps + "999 999" 42.1 + 8 px gap | 74.1 |

  The English strings are the neutral resx candidates measured here; the implementing agent may
  word them differently and measures its own wording against the same rooms.
- Today's bar height. probed: screenshots `01`, `03` and `08` in
  `.git/manager/ui/screens/` (1280x800 client area), pixel rows
  of the bottom band, 2026-11-25: a 1 px `#D0D0D0` top border plus 37 px of `#F0F0F0` ground,
  38 px in total. The 20 px timer value sets that height; with 12 px text the bar would shrink
  to about 27 px, hence `MinHeight` 38.
- PLC read paths. probed: code read, 2026-11-25. The execution poll calls
  `PlcTransactionExecutor.ReadExecutionStateAsync` every `PollingIntervalMs`; `S7Service`'s
  managing-area, recipe and protocol-version reads go through `PlcTransactionExecutor`; the
  keep-alive loop in `S7Service` reads one byte straight from the transport. The timestamp is
  written from pool threads (poll, keep-alive, reads started by the UI) and read on the UI
  thread, so it is synchronized and its comment names those threads.
- Config compatibility. probed: code read, 2026-11-25. `GridStyleLoader` deserializes through
  `YamlConfigDeserializer`, which calls `IgnoreUnmatchedProperties`, so a `grid_style.yaml` that
  still holds a deleted key loads, and the key is ignored without a message. No file under
  `SemiStep.Tests/YamlConfigs/` holds a changed or deleted key (grep for `secondary_foreground`,
  `connected:`, `timer_*`, `item_spacing`), so none of them changes.
- Avalonia headless tests run without Skia and HarfBuzz (`TestAppBuilder` uses `UseHeadless` with
  default options), so text widths there are not real. Tests assert slot `Bounds`, texts and
  tooltips; `+tnum`, real widths and trimming are manual smoke items.
- ASSUMPTION: batch A merged before this branch starts; the status bar then holds no button, and
  the sync toggle command is still a `ReactiveCommand` whose `IsExecuting` is readable from the
  status bar's view model. fallback: if A is not merged, stop; if the command moved out of reach,
  expose its `IsExecuting` at its owner. checked by: Task 1.
- ASSUMPTION: on a loop's own start and end rows the PLC counter of that depth already shows the
  current pass (1..count), as inside the loop. fallback: the rule "only levels whose counter is 1
  or more" already hides a 0. checked by: smoke S5.
- ASSUMPTION: the counter of depth d (`ForLoopCount1..3`) belongs to the enclosing loop with
  `LoopInfo.Depth` d, depth 1 the outermost, as `ExecutionTimeEstimator` reads it. fallback:
  stop and ask. checked by: smoke S5.

## Constraints and non-goals

- The drop of "с" is display only; CSV, clipboard and PLC values are unchanged.
- The sync toggle, its command and the `DisableSync` timing are not changed.
- The time-left calculation (`ExecutionTimeEstimator`) is not changed; branch
  `timeleft-loop-pass` owns it.
- Only reads move the last-exchange timestamp; a recipe write moves it through its read-back.
- The slots are sized for the shipped 12 px Segoe UI. A larger font or another family set in the
  style editor may clip; nothing clamps it.
- A long host name passed as `--plc-endpoint` may clip in the link slot; nothing trims it.
- After a sync refused with `RecipeActiveError`, the recipe stays pending and `PlcSyncCoordinator`
  writes it when `RecipeActive` drops (autonomous-decisions 35).
- Site-edited `grid_style.yaml` files are out of scope; the change covers the shipped presets
  and the Core default.
- The notification strip stays where it is.
- The bar has no command except the link-slot click and the message-count click.
- No new interface; the override flag travels as a value from startup.
- Tests stay as simple as possible; no speculative abstractions.

## UI design

No demo stage. This restates the approved decisions with the revised widths; the implementing
agent builds this and does not redesign it.

| Slot | Width | Content |
|---|---|---|
| Inset left | 12 | |
| Link | 270 | 8 px dot (green `#208A3C`, red `#DB3B4B`, hollow `#6C707E`, none) + 4 px gap + black text; no trimming; whole slot clickable, hand cursor |
| Separator | 25 | 12 + 1 px line + 12 |
| Execution, state and step | 200 | 20 px lock column, always reserved, with a 16 px glyph in `#6C707E` shown while running + "▶ Выполняется: шаг 5 из 8" / "Не выполняется"; never trims |
| Gap | 8 | |
| Execution, loops | 190 | "цикл 2 из 5 › 1 из 3", left-aligned, trims at the end |
| Separator | 25 | |
| Step timer | 134 | "Шаг: осталось 00:04:12" / "Шаг: —" |
| Gap | 8 | |
| Recipe timer | 154 | "Рецепт: осталось 01:12:40" / "Рецепт: всего 01:12:40" |
| Flexible gap | rest | |
| Message counts | 80 | right-aligned dots with counts; words in the tooltip |
| Inset right | 12 | |

- Total without the flexible gap: 1118 px; 162 px spare at `MinWidth` 1280.
- Bar height 38 px (`MinHeight`), vertical padding 5. Font: the bar font, 12 px, for every text
  including the timers.
- Strings, Russian: "ПЛК: подключено", "ПЛК: нет связи", "ПЛК: подключение…", "ПЛК: отключение…",
  "синхронизация выключена", "ПЛК: ошибка синхронизации", "нет связи, последний обмен {age}",
  "никогда" (mid-sentence), "Последний обмен: {age}", "▶ Выполняется: шаг {k} из {n}",
  "Не выполняется", "цикл {pass} из {count}" joined by " › ", "Шаг: осталось", "Шаг:",
  "Рецепт: осталось", "Рецепт: всего", " [выполняется]", the lock tooltip above. English neutral
  strings carry the same meaning and fit the same rooms.

## Gates

- Any slot width or position that differs between two states in the A4 test stops the run.

## Acceptance

All status-bar tests carry `[Trait("Area", "StatusBar")]`; the format tests carry
`[Trait("Area", "TimeFormat")]`; the timestamp tests carry `[Trait("Area", "LastExchange")]`.

- A-SB `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=StatusBar" -- RunConfiguration.TreatNoTestsAsError=true`
  → exit code 0, at least 37 tests, covering A1 to A8, A10 and A11:
  - A1 (8): sync off; "подключение…" while an enable is in flight; "отключение…" while a disable
    is in flight; "отключение…" ranks above sync off; an enable in flight never shows
    "отключение…"; reconnect retries show "нет связи, …" with the age, not "подключение…";
    "нет связи"; "подключено". Each with its dot kind (green, red, hollow, none).
  - A2 (7): `Failed` sets the error; it holds through `Syncing`, through `OutOfSync`, through a
    pending reconnect conflict; `Synced` clears it; `Failed` then `Idle` (disable) clears it;
    `RecipeActiveError` at connect keeps "ПЛК: подключено" and adds an info-level log entry.
  - A3 (5): the tooltip carries host:port and the last-exchange age in the #168 buckets; the bar
    text carries the host only when the override differs from the preset; never in the no-link
    state; the no-link text shows the age since the last successful read; with no successful
    exchange this session it shows "никогда".
  - A4 (1): layout and order guard only. At window width 1280 the realised `Bounds.Width` of link
    270, state and step 200, loops 190, step timer 134, recipe timer 154, counts 80, and each
    slot's x position, are equal in every state (each link state, idle, running, running in a
    3-level loop, no messages, errors and warnings). It does not check text fit; headless text
    widths are not real.
  - A5 (4): a click on the link slot opens the log; a second click keeps it open; with an empty
    log the click does nothing; a click on the counts slot toggles the log.
  - A6 (3): idle "Не выполняется" without lock; running "▶ Выполняется: шаг 5 из 8" with lock and
    the lock tooltip; step 9 of 12 is padded with one U+2007.
  - A7 (3): loop text innermost first with " › ", only levels with a counter of 1 or more, total
    from the recipe; the tooltip has the lock text first, then every level; the loop text trims
    at the end.
  - A8 (4): timers for an empty recipe, idle, running; timer text uses the bar font size.
  - A10 (1): the window title ends with " [выполняется]" while the recipe runs and loses it when
    the run ends.
  - A11 (1): a `grid_style.yaml` that still holds the deleted `timer_*` and `item_spacing` keys
    loads with no error.
- A9 `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=TimeFormat" -- RunConfiguration.TreatNoTestsAsError=true`
  → exit code 0, at least 3 tests: a `time_hms` value with units "с" formats as the literal
  "00:10:00"; a numeric value keeps its unit; the step start time column measures without the
  unit.
- A12 `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=LastExchange" -- RunConfiguration.TreatNoTestsAsError=true`
  → exit code 0, at least 4 tests: before any read the timestamp is empty; a successful execution
  poll moves it to now (the age resets); a successful keep-alive read moves it; a failed read and
  a lost link leave it unchanged, so the age keeps growing.
- `dotnet build SemiStep.slnx` → exit code 0 (warnings fail the build).
- `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → exit code 0.
- `dotnet format SemiStep.slnx --verify-no-changes` → exit code 0.
- manual smoke, emulator and app with the MBE preset and `--plc-endpoint 127.0.0.1:10102`
  (`.run/` configuration "Emulator and SemiStep MBE"), window at 1280 px:
  1. S1 (skipped - manager live check) sync off → no dot, "синхронизация выключена", ground `#F7F8FA`, bar 38 px high.
  2. S2 (skipped - manager live check) turn sync on, then stop the emulator → hollow dot "подключение…", green "подключено,
     127.0.0.1" with the tooltip age restarting on every poll and never reaching "> 30 с назад",
     then red "нет связи, последний обмен …" in full, no host, the age growing while the app
     retries.
  3. S3 (skipped - manager live check) turn sync off during a write → hollow "отключение…" for up to 5 s, then "синхронизация
     выключена".
  4. S4 (skipped - manager live check) click the link slot → the notification log opens.
  5. S5 (skipped - manager live check) run a recipe with nested loops in the emulator → step text, lock, loop text innermost
     first, correct totals, also on the loop's start and end rows; title " [выполняется]".
  6. S6 (skipped - manager live check) timers count down without horizontal jiggle; captions grey, values black, 12 px.
  7. S7 (skipped - manager live check) grid "Время" and "Время абс." show "00:10:00" without "с".
  8. S8 (skipped - manager live check) no item in the bar moves while any text changes; the counts slot stays at the right end
     when an error appears.
  9. S9 (skipped - manager live check) worst cases at 1280 px, in Russian and English UI: the no-link text with "> 1 мин
     назад", "● 999 ● 999" counts, a 3-digit step count, a 3-level loop, a step and a recipe
     longer than 24 h; each reads in full in its slot except the loop trim.
- not visible to headless tests: `+tnum` effect, real text widths and trimming, dot colours as
  rendered, the emulator's counters on loop boundary rows.

---

## Context

- `SemiStep/SemiStep.UI/MainWindow/AppStatusBar.axaml`: the bar; today a `Grid` of seven `Auto`
  columns, sync button styles, "FOR" counters.
- `SemiStep/SemiStep.UI/MainWindow/MainWindowViewModel.cs`: the `IsSync*` flags,
  `PlcSyncStatusText`, `LastSyncTimeText` with the #168 buckets, `BuildWindowTitle`, the 1 s
  refresh tick for the age; today it ignores the snapshot payload of `PlcStateChanged`.
- `SemiStep.Core/Plc/PlcLifecycleManager.cs`: `EnableSync` sets the flag, then connects;
  `DisableSync` clears the flag, waits, then disconnects. `PlcSyncCoordinator`: `LastSyncTime`
  set on a completed write; precheck failures become `Failed`. `PlcSyncExecutor`:
  `RecipeActiveError`.
- `SemiStep.Core/Plc/Sync/PlcTransactionExecutor.cs`, `PlcExecutionMonitor.cs` and
  `SemiStep.Core/Plc/S7/S7Service.cs` (keep-alive loop, `IS7Connection`): the read paths for the
  last-exchange timestamp. Test doubles: `SemiStep.Tests/S7/Helpers/FakeS7Driver.cs`.
- `SemiStep/SemiStep.UI/Plc/PlcMonitorViewModel.cs`: timer texts, `ForLoopCount1..3`,
  `IsRecipeActive`, `ActualLine`; idle step text is "00:00:00" today.
- `SemiStep/SemiStep.UI/RecipeGrid/TimeFormatHelper.cs`: `FormatValue` appends units after
  hh:mm:ss.
- `SemiStep.Core/Recipes/RecipeSnapshot.cs` and `LoopInfo.cs`: loops with `Depth` and
  `Iterations`.
- `SemiStep/SemiStep.UI/Program.cs`: `LoadConfigurationAsync` applies the `--plc-endpoint`
  override before DI, so the override flag is computed there and passed on. `PlcEndpoint.cs`,
  `StartupOptions.cs`.
- `SemiStep/SemiStep.UI/MessageService/MessagePanelViewModel.cs`: `ToggleCommand`, `IsVisible`,
  `ShowPanel`, `StatusErrorSummary`; `MessageSeverity.Info` exists.
- `SemiStep/SemiStep.UI/Styles/CellPaletteInstaller.cs`: status-bar and chrome resources;
  `StatusBarPadding` is a uniform `Thickness` today.
- `ConfigFiles/{MBE,MOCVD,RIE}/ui/grid_style.yaml`: `status_bar:` and `chrome:` are identical in
  the three presets; keep them in sync.
- `SemiStep.Core/Configuration/GridStyleOptions.cs` default instance; the style-editor drafts in
  `SemiStep.UI/StyleEditor/`.
- `SemiStep/SemiStep.UI/App.axaml`: the `#818594` theme keys.
- Rules: `~/.claude/rules/avalonia.md` (compiled bindings, derived state through `ToProperty`,
  `FuncValueConverter`), `~/.claude/rules/csharp.md` (shared mutable fields synchronized and
  commented), `docs/architecture/headless-ui-testing.md`, `docs/architecture/ui-localization.md`
  (both resx files), `docs/architecture/error-reporting.md`.

## Boundaries

- Task 5 creates the last-exchange timestamp as a read-only `DateTimeOffset?` on
  `RecipeCoordinator`, empty until the first successful read of the session; Task 6 consumes it.

## Tasks

### Task 1: Rebase onto batch A and take the baseline

**Goal:** the branch starts from master with batch A merged, and its premises hold
**Entry:** `git fetch origin && git rebase origin/master`

- [x] confirm batch A is on `origin/master`; if not, stop and report (c683d18 #223)
- [x] confirm the status bar holds no button; record where the sync toggle command lives and that
      its `IsExecuting` is readable (no sync button; only the counts button remains;
      `MainWindowViewModel.ToggleSyncCommand`, a `ReactiveCommand` built in its constructor)
- [x] list which of `StatusSyncOn`, `StatusSyncOff`, `StatusConnecting`, `LocalModeBrush`,
      `ConnectingBrush` still have readers (the three strings are gone from both resx files; the
      two brushes are installed but no AXAML or code reads them)
- [x] run `dotnet build SemiStep.slnx` and the full test suite; record the baseline counts
      (build 0 warnings; 1838 passed, 0 failed)

### Task 2: Palette, ground, padding and deleted style keys

**Goal:** secondary text `#6C707E` everywhere, ground `#F7F8FA`, dot colours from keys, vertical
padding, timers in the bar font, the timer keys and `item_spacing` gone
**Entry:** `GridStyleOptions.Default`, the three `grid_style.yaml`
**Likely touches:** `StatusBarStyle.cs`, `StatusBarStyleDto.cs`, `GridStyleMapper.cs`,
`GridStyleDtoMapper.cs`, `CellPaletteInstaller.cs`, `StatusBarStyleDraft.cs`,
`GridStyleEditorViewModel.cs`, `GridStyleEditorWindow.axaml`, `App.axaml`

- [x] set `chrome.secondary_foreground` `#6C707E`, `chrome.connected` `#208A3C`,
      `chrome.disconnected` `#DB3B4B`, `status_bar.background` `#F7F8FA` in the presets and the
      Core default; set the three `#818594` theme keys in `App.axaml` to `#6C707E`
- [x] make `status_bar.padding` vertical only
- [x] delete the six timer font keys and `item_spacing` end to end; delete `local_mode` /
      `connecting` if Task 1 found no reader
- [x] tests that change: `ThemeTests` (secondary pin `#818594`), `CellPaletteInstallerTests`,
      `GridStyleMapperTests`, `GridStyleEditorViewModelTests`, `GridStyleOptionsTestData`;
      `AppStatusBarTimerFontTests` is replaced by A8's font-size case; add A11
- [x] run tests - must pass before the next task

### Task 3: Drop "с" from hh:mm:ss values

**Goal:** `time_hms` values show no unit in the grid, the transposed grid and width measurement
**Entry:** `TimeFormatHelper.FormatValue`
**Likely touches:** `PropertyTimeMultiConverter.cs`, `ColumnWidthCalculator.cs`,
`RecipeGridSurfaceBase.cs`, `RecipeRowViewModel.cs`, `ParameterCellViewModel.cs`

- [x] `time_hms` values format as hh:mm:ss with no unit; other kinds keep theirs
- [x] remove unit plumbing that only fed `time_hms` display, if it has no other reader
- [x] tests (A9): the literal "00:10:00" for a `time_hms` value with units "с"; a numeric value
      keeps its unit; the step start time column measures without the unit; update existing
      tests that expect "с" after hh:mm:ss
- [x] run tests - must pass before the next task

### Task 4: Fixed-slot frame

**Goal:** the bar layout from `## UI design`, with equal-width digits, the counts slot at the right
end, `MinHeight` 38 and `MinWidth` 1280
**Entry:** `AppStatusBar.axaml`
**Likely touches:** `MainWindow.axaml`, resx files

- [x] build the slot row with the widths, separators, gaps, 12 px side insets and flexible gap;
      remove the "FOR" counters and the old sync texts
- [x] set `+tnum` on the bar root and `MinHeight` 38 on the bar
- [x] move the counts into the 80 px right slot: dots with counts, words in the tooltip, click
      toggles the log; the slot keeps its width when empty
- [x] set `MinWidth` 1280 on the main window
- [x] tests: A4 frame part (each slot realises its width and x position at 1280, with and without
      messages), A5 counts case
- [x] run tests - must pass before the next task

### Task 5: Last-exchange timestamp

**Goal:** Core records the time of each successful PLC read and the UI can read it; `LastSyncTime`
is gone
**Entry:** `PlcTransactionExecutor`, the keep-alive loop in `S7Service`
**Likely touches:** `PlcExecutionMonitor.cs`, `IS7Connection.cs`, `PlcLifecycleManager.cs`,
`RecipeCoordinator.cs`, `IPlcSyncService.cs`, `PlcSyncCoordinator.cs`,
`SemiStep.Tests/Helpers/StubPlcSyncService.cs`, the UI test fixture

- [x] stamp the timestamp after each successful read in `PlcTransactionExecutor`; move the
      keep-alive read behind it; never stamp on a failure; synchronize the field
- [x] expose it read-only through `IS7Connection`, `PlcLifecycleManager` and `RecipeCoordinator`
      (Boundaries)
- [x] delete `LastSyncTime` end to end
- [x] tests (A12): empty before any read; a successful poll and a successful keep-alive read move
      it; a failed read and a lost link leave it; tests that change: the `PlcSyncCoordinator`
      tests that read `LastSyncTime` switch to the `Synced` status
- [x] run tests - must pass before the next task

### Task 6: Link slot

**Goal:** the link states, latch, tooltip, override host and click from Flow branches
**Entry:** `MainWindowViewModel` connection-state properties
**Likely touches:** `Program.cs`, `UiDi.cs`, `MessagePanelViewModel.cs`, `PlcSyncCoordinator.cs`,
`RecipeCoordinator.cs`, resx files

- [x] one link-state pipeline over the snapshot stream and the toggle command's `IsExecuting`,
      with the error latch, ending in `ToProperty`
- [x] a sync refused with `RecipeActiveError` is not `Failed` and reaches the log at info level
- [x] compute the override flag in `Program.LoadConfigurationAsync` and pass it with the effective
      endpoint to the view model
- [x] texts, dot kinds, tooltip with host:port and the last-exchange age from the Task 5
      timestamp in the #168 buckets; no-link text with that age or "никогда" and no host; no
      trimming; a click opens the log and never hides it; delete the obsolete resx strings
- [x] tests: A1, A2, A3, A5 link cases, A4 link states; tests that change:
      `LastSyncFreshnessTests` (its bucket cases move to the last-exchange timestamp, shown in the
      tooltip and the no-link text), `MainWindowViewModelSyncStateTests`
- [x] run tests - must pass before the next task

### Task 7: Execution slot and window title

**Goal:** state and step, lock, loop part with trim, one tooltip, title suffix
**Entry:** `PlcMonitorViewModel`
**Likely touches:** `MainWindowViewModel.BuildWindowTitle`, resx files

- [x] state and step text with figure-space padding; 16 px lock glyph while running
- [x] loop text innermost first from the PLC counters and the recipe's loop counts, inclusive of
      start and end rows, only levels with a counter of 1 or more
- [x] one tooltip on the execution slot: lock text first, then the loop levels
- [x] title suffix " [выполняется]"; remove `ForLoopCount1..3` from the view model's public
      surface if nothing else reads them
- [x] tests: A6, A7, A10, A4 execution states
- [x] run tests - must pass before the next task

### Task 8: Timer texts

**Goal:** state-dependent captions and values in the 134 and 154 px slots
**Entry:** `PlcMonitorViewModel` timer texts

- [x] captions "Шаг: осталось" / "Рецепт: осталось" while running, "Шаг:" / "Рецепт: всего" when
      idle; idle step value "—"; empty recipe "—" for both
- [x] caption in secondary grey, value in the bar foreground, both in the bar font
- [x] tests: A8 and the timer slots in A4; test that changes: `PlcMonitorViewModelTimingTests`
      (the idle step text "00:00:00" becomes "—")
- [x] run tests - must pass before the next task

### Task 9: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] A-SB, A9 and A12 pass with at least 37, 3 and 4 tests (47, 3, 4)
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` exit code 0 (1863 passed)
- [x] `dotnet build SemiStep.slnx` and `dotnet format SemiStep.slnx --verify-no-changes` exit
      code 0

### Task 10: Update documentation

**Goal:** docs describe the new status bar, palette keys, last-exchange age and formatting
**Entry:** `docs/architecture/grid-style-configuration.md`

- [x] `grid-style-configuration.md`: remove the merged four-colour toggle text, the timer font
      roles and `item_spacing`; describe the link dot keys, vertical padding, the new defaults,
      the deleted keys and that an old file holding them still loads
- [x] `ui-localization.md`: the status strings; `recipe-grid-column-sizing.md` if it describes the
      `time_hms` width
- [x] `docs/readme.md` (Russian, keep the language): the sync status table and the status bar
      description, with the age counted from the last successful exchange with the PLC
- [x] update any doc that names `LastSyncTime`

## Post-Completion

- the operator's manual test: smoke steps S1 to S9 at 1280 px against the emulator, plus the
  items not visible to headless tests.
- the PR body: `Closes #201` (link state, address and lock in the status bar; the switch itself
  is batch A), `Closes #202` for its status-bar and timer parts (items 2, 4, 5, 6). Items that
  remain for batch D: item 1 (running-row tint and "▶" glyph), item 3 (keep the running step in
  view), item 7 (`current_step_marker` colour). Batch D's plan carries them; if D is not planned
  when this merges, use `Refs #202` instead of `Closes`.
- `Refs #204`: secondary text contrast (item 6), "Ожидание" replaced (item 7), "FOR:" removed
  (part of item 5). The grid items of #204 stay open for batches C and D.
