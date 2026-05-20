# §2.4 Keyboard Shortcut Completion

## Overview

`Docs/02-ui-requirements.md` §2.4 lists a baseline set of keyboard shortcuts that the editor must
support. The current `MainWindow.axaml` `KeyBindings` section only wires a subset (`Ctrl+O`,
`Ctrl+S`, `Ctrl+Z`, `Ctrl+Shift+Z`, `Ctrl+N`). This stub captures the remaining bindings flagged
out-of-scope by `Docs/plans/completed/20260520-per-window-edit-connect-mode.md`.

Missing bindings to add:

- `Ctrl+Y` — Redo (spec uses `Ctrl+Y`; current code uses only `Ctrl+Shift+Z`).
- `Ctrl+C` — Copy step.
- `Ctrl+V` — Paste step.
- `Ctrl+X` — Cut step.
- `Del` — Delete step.
- `Ctrl+Shift+T` — Toggle transposed grid orientation (spec §2.3.2).

All commands already exist on the relevant view-models and are gated correctly by
`RecipeCoordinator.CanEditRecipe` where needed; this work is purely XAML `KeyBinding` wiring plus
a confirmation test that each binding routes to the expected command.

## Files involved

- `SemiStep/SemiStep.UI/MainWindow/MainWindow.axaml` — extend the `Window.KeyBindings` section with
  the missing entries.

## Implementation Steps

### Task 1: Wire missing keyboard shortcuts

- [ ] Add `KeyBinding` for `Ctrl+Y` → `RecipeCommands.RedoCommand`.
- [ ] Add `KeyBinding` for `Ctrl+C` → `Clipboard.CopyStepCommand`.
- [ ] Add `KeyBinding` for `Ctrl+V` → `Clipboard.PasteStepCommand`.
- [ ] Add `KeyBinding` for `Ctrl+X` → `Clipboard.CutStepCommand`.
- [ ] Add `KeyBinding` for `Del` → `RecipeCommands.DeleteStepCommand`.
- [ ] Add `KeyBinding` for `Ctrl+Shift+T` → orientation-toggle command (verify the command
      exists; if not, this becomes a prerequisite sub-task).
