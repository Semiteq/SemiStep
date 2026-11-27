# Style window rework

Design source: `.git/manager/ui/style-window-review.md` section 2 (captures in
`.git/manager/ui/style-window/`), adopted by `.git/manager/autonomous-decisions.md` item 56.

## Behavior

- Today the window "Настройки стиля таблицы" is a resizable 720 x 780 scroll of 2204 px of
  content. The operator sees one third of it. An empty field disables Save with no reason, a colour
  shows as a 200 px bar with no hex value, and Save closes the editor and opens a second modal
  (`RestartPromptDialog`) whose "Выйти сейчас" only closes the main window.
- After this change the window is "Настройки оформления", a fixed 756 x 560 dialog that cannot be
  resized and has no scroll. A 176 px section list on the left switches six form pages on the
  right: "Шрифты", "Таблица", "Цвета ячеек", "Подсветка", "Строка состояния", "Панель
  уведомлений". It opens on "Шрифты" every time.
- Every page uses one 152 px label column and one field column. Numbers are 120 px spinners.
  A colour is one stock `ColorPicker` across the field column: a swatch filled with the colour and
  a chevron. Its flyout has a palette, sliders and a hex field. The six weights are percent fields
  (0 to 100); the YAML keeps 0..1.
- An invalid field (an empty number or one out of range) gets a red border and a warning glyph. Its page gets a warning glyph in the section list. The message line names the first
  broken field in page order and field order, and Save is disabled while any field is invalid.
- Save writes the file and keeps the window open. After a save that changed a value the message
  line shows "Сохранено. Вступит в силу после перезапуска.". "Закрыть" closes without saving.
  "Стандартные значения" loads the built-in defaults into every page and writes nothing until Save.
- The menu item "Вид" > "Настройки стиля таблицы…" becomes "Настройки оформления…".
- Scope guard: what each YAML key means, how the app reads it, the loader, the writer and the
  presets stay as they are. The `colors.grid_line` value change (item 57) is on another branch.
  A general settings window, live preview and an app relaunch are not built.

## Flow branches

| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Operator opens the window | Section list on "Шрифты", page "Шрифты" shown, every field seeded from `grid_style.yaml` | review 2 "Section list" | A1, A2 |
| Operator picks another section | The page on the right changes; the window keeps its size | review 2 "Window" | A2 |
| Operator empties a number field | Red border and glyph on the field, glyph on the page's list row, message "Таблица › Высота строки: введите число от 12 до 400", Save disabled | review 2 "Footer, buttons and messages" | A3 |
| Operator picks a colour in the flyout (spectrum, sliders or its hex field) | When the flyout closes, the field takes the colour as opaque `#RRGGBB`; Save writes it upper case | item 62 | A3 |
| Operator opens a colour flyout and closes it without a change | Nothing is written, so a seeded `#AARRGGBB` value stays | item 62, agent | A3 |
| Two fields are invalid | The message names the first one in page order, then field order | review 2 "Message line" | A3 |
| Operator fixes the last invalid field | Border, glyphs and message clear, Save enabled | agent | A3 |
| Save with changed values | File written, window stays open, restart notice on the message line | review 2, item 56 | A4, A6 |
| Save with no change since the last load or save | Nothing written, no notice | review 2 "Сохранить" | A4 |
| Save fails (I/O error) | The failure text replaces the notice on the message line; window stays open | agent: existing `ErrorMessage` path | A4 |
| "Стандартные значения" | Every visible field takes `GridStyleOptions.Default`; `orientation`, `chrome.info` and `chrome.panel_background` keep their seeded values; nothing is written until Save | review 2, agent for the two hidden keys | A5 |
| "Закрыть" or Escape with unsaved edits | Window closes, edits are dropped, no prompt | review 2 "Закрыть" | A5 |
| Escape while a drop-down or a colour flyout is open | The drop-down or flyout closes, a picked colour stays; the window stays | review 2 "Закрыть" | A5 |
| Operator drags the window border | Nothing happens: the window is not resizable | review 2 "Window" | A2, smoke 3 |

## Decisions

- Pages, not a scroll: the content is more than two dialog heights (review finding 1). The window
  height is fixed at the tallest page, so a page switch never resizes it. Rejected:
  `SizeToContent="Height"`, because the pages differ in height (176 to 408 px).
- Title and menu item renamed to "Настройки оформления" / "Appearance settings": the window also
  styles the status bar and the notification panel (review finding 8). No other plan claims the
  name; the general settings window is a separate task (item 52). Title and menu change together.
- Restart notice without a "Перезапустить" button: SemiStep has no relaunch (item 56).
  `RestartPromptDialog` and its four strings are deleted.
- Rows for `chrome.info` and `chrome.panel_background` leave the window (item 56, review finding 5).
  The keys stay in the record and the file; Save and "Стандартные значения" both keep their seeded
  values, so a key the operator cannot see never changes.
- One stock `ColorPicker` per colour, as SemiPlot's pen editor has it (item 62): the flyout already
  has a hex field, so a separate hex box only doubled the input. The picker reads the colour draft
  one way; `ColorFieldView` writes the picked colour to the draft when the flyout closes, and only
  when it differs from the colour at open. Alpha is off, so a pick is stored as `#RRGGBB`.
  Rejected: a two-way binding, because an opened and dismissed flyout could then rewrite a seeded
  `#AARRGGBB` value.
- Validity is computed once in the view model, from a list of number fields in page and field
  order with their page, label and range. A colour cannot be invalid. Field borders, list glyphs, the message and Save all read it.
- Weights in percent through a two-way converter (draft value x 100). A hand-edited `0.125` shows
  12.5 and round-trips (review finding 9).
- The layout departs from the project skill `avalonia-form-layout` in four places listed in review
  section 3: a fixed size with no scroll, no `MaxWidth` column, a colour picker as wide as the field
  column, and 120 px numbers.

## Caveats

- `Carousel` shows one item at a time, and its `SelectedIndex` can follow the list. ASSUMPTION:
  the page controls inherit the window's `DataContext` and keep their bindings when hidden;
  fallback: six page grids in one cell, each visible for its list index. probed: headless tests
  that switch pages and edit fields, and the live run, 2026-11-26: holds.
- `NumericUpDown` clamps typed values to `Minimum`/`Maximum`. ASSUMPTION: typing 500 into a
  12..400 field leaves `Value` at 400 or at the last value, never out of range, and an emptied box
  sets `Value` to null; fallback: the range rule in the field list still disables Save. probed:
  headless test types 500 and moves focus, 2026-11-26: the value stays in range.
- A percent field with `FormatString="0.#"`. ASSUMPTION: focus passing through the field without
  typing does not re-parse its text, so a seeded `0.1234` survives; fallback: widen the format to
  `0.##`. probed: headless test focuses every spinner on every page, 2026-11-26: `0.1234` survives.
- `ColorPicker` and `ComboBox.SelectedValue` may write a value back when a page is realised.
  ASSUMPTION: visiting all six pages without typing leaves `BuildRecord()` equal to the seed, and
  the nested option records compare by value. probed: the same headless test, 2026-11-26:
  `BuildRecord()` equals the seed and Save writes nothing.
- Semi draws the `ColorPicker` swatch square when its `Content` is not overridden. The picker
  therefore sets its own `Content`, a `Border` with `Margin="4"` and the theme's
  `ColorPickerCornerRadius`, filled with the colour, and stretches to 344 px. probed: headless layout
  test and live capture at 96 DPI, 2026-11-27: the picker is 344 x 32 at the field column edge.
- Semi's flyout shows the hex field on its third tab, not the first. probed: headless test that
  types lower-case hex there and live capture, 2026-11-27: the hex field is `PART_HexTextBox` on the
  last tab, and Enter applies the typed value.
- `Button.IsCancel` closes the window on Escape. ASSUMPTION: an open `ComboBox` marks Escape
  handled, so the window stays. probed: headless test, 2026-11-26: the first Escape closes the
  drop-down, the second closes the window.
- Headless tests run without Skia, so text widths there are not real. Label fit (widest Russian
  label "Ячейки только для чтения", about 144 px) is read from the live UI Automation bounds in
  task 3, not from a headless test.
- A resize by `SetWindowPos` bypasses `CanResize="False"`. The live check proves "not resizable"
  by the missing resize frame (`WS_THICKFRAME`) and a border drag, not by a programmatic resize.

## Constraints and non-goals

- The YAML keys, their meaning, `GridStyleOptions`, the facade, the loader and the writer do not
  change. `BuildRecord` still takes `orientation` from the seed.
- Tests check which field is reported and its state, not the message wording
  (`docs/architecture/error-reporting.md`).
- No live preview, no relaunch, no persistence of the selected page.

## UI design

The design is review section 2 as written. Summary of what the code builds:

Window: `Width="756"`, `Height="560"`, `CanResize="False"`, `ShowInTaskbar="False"`,
`WindowStartupLocation="CenterOwner"`, margin 16, no `ScrollViewer`, every control 32 px high.

Horizontal: 16 + list 176 + 16 + label 152 + 12 + field 344 + 24 slack + 16 = 756.
Vertical: body 16 + 408 + 16 = 440; footer 1 border + 16 + 40 message + 8 + 32 buttons + 16 = 113.

Section list: `ListBox`, 176 wide, 408 high, 4 px border padding, rows 32 high, text inset 8, a
16 px warning glyph right-aligned in the row of a page with an invalid field.

Form page: one `Grid` per page, columns `152,12,344,*`, `RowSpacing="8"`. Labels centred
vertically, one line. Section headers bold 12 px, default text colour, 0 above the first, 16 above
the rest, 8 below.

Controls:

| Content | Control | Width |
|---|---|---|
| Whole number | `NumericUpDown`, `FormatString="0"`, integer parsing, right-aligned | 120 |
| Weight | `NumericUpDown` 0 to 100, step 1, `FormatString="0.#"` | 120 |
| Colour | `ColorPicker`, `IsAlphaEnabled` and `IsAlphaVisible` false, a swatch `Border` as `Content` | 344 |
| Font family | `ComboBox`, first item "Segoe UI (по умолчанию)" | 240 |
| Font row | size 120 + 8 + weight `ComboBox` 160 + 8 + italic `CheckBox` in 48 | 344 |

The colour flyout has three tabs: spectrum, palette, and components with the hex field. A colour
picked on any tab reaches the draft when the flyout closes, by a click outside or by Escape.

Weight items: "Светлый" (300), "Обычный" (400), "Средний" (500), "Полужирный" (600), "Жирный"
(700), "Сверхжирный" (800), "Чёрный" (900); a seeded weight outside the list shows its number.

Pages (ru label, YAML key):

- Шрифты (176 px): "Шрифт" `fonts.family`; header row "Размер", "Насыщенность", "Курсив";
  "Заголовки таблицы" `fonts.header_*`; "Ячейки таблицы" `fonts.cell_*`; "Строка состояния"
  `status_bar.font_size`, `weight`, `italic`.
- Таблица (408 px): header "Строки и ячейки": "Высота строки" `layout.row_height` 12..400,
  "Отступ слева", "Отступ сверху", "Отступ справа", "Отступ снизу" `layout.cell_padding_*` 0..100;
  header "Цвета": "Фон таблицы" `chrome.grid_background`, "Рамка таблицы" `chrome.grid_border`,
  "Линии сетки" `colors.grid_line`, "Текст заголовков" `chrome.header_foreground`.
- Цвета ячеек (248 px): header "Основной цвет": "Изменённые ячейки" `colors.cells.changed`,
  "Ячейки только для чтения" `readonly.depth_0`, "Неприменимые ячейки" `disabled.depth_0`; header
  "Выделение": "Цвет" `colors.selection.accent`, "Непрозрачность, %" `colors.selection.weight`.
- Подсветка (352 px): subtitle "Проценты задают непрозрачность слоя поверх цвета ячейки."; header
  "Выполнение": "Цвет" `execution.running`, "Текущий шаг, %" `running_weight`, "Выполненные шаги, %"
  `past_weight`; header "Циклы": "Цвет" `loop.color`, "Глубина 1, %", "Глубина 2, %",
  "Глубина 3, %" `loop.depth_N_weight`.
- Строка состояния (368 px): header "Размеры": "Отступ сверху и снизу" `status_bar.padding`;
  header "Цвета": "Фон", "Текст" `status_bar.background`, `foreground`; "Вторичный текст"
  `chrome.secondary_foreground` (tooltip "Также подсказка в пустой таблице"); "Подключено",
  "Нет связи" `chrome.connected`, `disconnected`; "Разделители" `chrome.separator`; "Рамки панелей"
  `chrome.subtle_border` (tooltip "Также рамка панели уведомлений").
- Панель уведомлений (288 px): header "Размеры": "Наибольшая высота" `validation_panel.max_height`
  20..2000 step 10; header "Цвета": "Фон", "Фон заголовка" `chrome.panel_header_background`,
  "Текст", "Ошибки", "Предупреждения" `validation_panel.*`.

Footer: 1 px top border `SubtleBorderBrush`, window background. Row 1 is the message line, full
width, two 20 px lines reserved, wraps and trims. Row 2: "Стандартные значения" on the left;
"Сохранить" (primary, default) and "Закрыть" (cancel) on the right, 8 apart.

English labels follow review section 2.

## Acceptance

- A1 `dotnet build SemiStep.slnx` → exit 0, 0 warnings.
- A2 `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "Area=GridStyleEditor"` →
  0 failed; covers: window 756 x 560 with `CanResize` false, no `ScrollViewer`, label column 152,
  number 120, colour picker 344 x 32, family 240, weight 160, every page fits the body, every
  editable key on exactly one page and the two dropped keys on none.
- A3 same filter: an emptied number marks its field with the error border, puts a glyph on its
  section, names that field in the message and disables Save; the first broken field in page order
  wins. A pick through each colour picker writes only its own key and the picker then shows that
  colour; a hex value typed in the flyout is saved as upper-case `#RRGGBB`; an opened and dismissed
  flyout writes nothing.
- A4 same filter: Save keeps the window open and sets the restart notice; a no-change save leaves
  the file bytes unchanged; a weight typed as 12.5 % is written as `0.125`.
- A5 same filter: "Стандартные значения" yields `GridStyleOptions.Default` except the three kept
  keys; Escape on an open drop-down or colour flyout keeps the window.
- A6 full suite `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --blame-hang-timeout 5m`
  → 0 failed; `dotnet format SemiStep.slnx --verify-no-changes` → exit 0.
- Manual smoke (`--config-dir ConfigFiles/MBE`, window from "Вид"):
  1. each of the six pages at the default size → no scroll bar, no clipped label;
  2. empty "Высота строки" → red border, glyph, message, Save disabled;
  3. drag the window border → the size does not change;
  4. change one colour, Save → the YAML on disk changed, the window stays open with the restart
     notice.
- Not visible to headless tests: real text widths, 125 % and 150 % scaling.

---

## Context

- `SemiStep/SemiStep.UI/StyleEditor/`: `GridStyleEditorWindow.axaml(.cs)`,
  `GridStyleEditorViewModel.cs`, the ten drafts, `RestartPromptDialog.*`.
- `SemiStep/SemiStep.UI/Localization/Resources.resx` and `Resources.ru.resx`.
- `SemiStep/SemiStep.UI/MainWindow/RecipeMenuBar.axaml` (menu item).
- Tests: `SemiStep/SemiStep.Tests/UI/StyleEditor/`.
- Docs: `docs/architecture/grid-style-configuration.md` "Editor write-back",
  `dialogs-and-interactions.md`, `exit-flow.md` "Style-editor restart path", `docs/readme.md`
  section 6 (style editor), `docs/img/visual_style_window.png`.
- Reference pattern: SemiPlot `Styles/Forms.axaml` (invalid border, glyph, `form-message`) and
  `Input/FormattedFieldInput.cs` (normalising text input).

## Tasks

### Task 1: View model: colour fields, validation list, save flow

**Goal:** the view model validates every field in page order, keeps the window open on Save,
skips a no-change save, loads defaults, and no longer needs the restart dialog.
**Entry:** `GridStyleEditorViewModel`, the drafts.
**Likely touches:** drafts, a colour field type, a validation list type, a section item type,
the window code-behind, `RestartPromptDialog.*`, the owner-routing and restart dialog tests.

- [x] colour field object (colour plus hex text, valid flag) replaces each colour leaf
- [x] field list with page, label and rule; first invalid field drives the message, per-page
      error flags and `CanSave`
- [x] Save keeps the window open, sets the restart notice, writes nothing when the record equals
      the last saved one; "Стандартные значения" command
- [x] delete `RestartPromptDialog`, its strings and the exit routing in the window
- [x] tests: first invalid field wins; a bad hex keeps the last colour; no-change save leaves the
      file unchanged; defaults keep the three seeded keys; the `NumericUpDown` probe
- [x] run build, full suite, format

### Task 2: Window layout and controls

**Goal:** the window matches the UI design section.
**Entry:** `GridStyleEditorWindow.axaml`.
**Likely touches:** the window, a hex input behaviour, a percent converter, resources, the menu.

- [x] fixed window, section list, six pages, footer, styles for 32 px controls, invalid border and
      glyph, message line
- [x] hex input behaviour; percent converter; Russian and English strings; menu item rename
- [x] tests: layout read-back (sizes, columns, no scroll, every key on one page, dropped keys gone),
      field error border, list glyph, Save keeps the window open with the notice, percent maps to
      0..1 in the YAML, no-edit round trip over all pages, Escape on an open drop-down
- [x] run build, full suite, format

### Task 3: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds on the finished branch.
**Entry:** the Acceptance section.

- [x] live run from the worktree build with `--config-dir ConfigFiles/MBE`: capture each page, a
      resize attempt and a validation error; read label bounds from UI Automation
- [x] change one colour, Save, check the YAML diff and the notice, then `git checkout -- ConfigFiles`
- [x] full suite and format

### Task 4: Update documentation

**Goal:** the docs describe the new window.
**Entry:** `docs/architecture/grid-style-configuration.md` "Editor write-back".

- [x] grid-style-configuration editor section; dialogs-and-interactions; exit-flow restart section
- [x] `docs/readme.md` style editor paragraph (Russian) and the window screenshot

### Task 5: Stock colour picker (item 62)

**Goal:** each colour row is one stock `ColorPicker` in SemiPlot's style; the hex box goes.
**Entry:** `ColorFieldView`, `ColorDraft`.

- [x] `ColorFieldView`: one picker with a swatch `Content`, alpha off, write-back on flyout close
- [x] delete `HexColorInput`, the hex validation, the hex box styles and the two hex strings
- [x] tests: wiring by picker, hex typed in the flyout saved as upper case, dismiss writes nothing
- [x] live capture of "Цвета ячеек", "Подсветка" and an open flyout on its hex tab
- [x] run build, full suite, format

## Post-Completion

- The operator's manual test: the smoke steps above, plus 125 % and 150 % Windows scaling.
