# Transposed Grid: Recycle-In-Place Virtualizing Panel

## Overview
- The transposed recipe grid churns GC hard while scrolling a large recipe (2100 steps, Release). A gc-verbose allocation-by-method trace shows ~41–56% of all scroll-time UI-thread allocation is under our column realization, dominated by per-recycle attach machinery: `Dictionary.Resize` (property-store/style-dict growth) ~18.9%, `Border.CreateCompositionVisual` + `Visual.CreateCompositionVisual` ~9%, `StyleBase.Attach` + `Setter.Instance` ~10%, plus `PublishNext` (property/binding notifications) ~29.5% inclusive. Construction is negligible (`BuildCellSlot` 0.6%) — the pool already prevents slot rebuild.
- Root cause (confirmed from Avalonia 12.0.3 source): `VirtualizingStackPanel.RecycleElement` does `RemoveInternalChild` (full visual+logical detach) + `AddInternalChild` (re-attach) per viewport crossing. Every ~115-element column subtree re-runs style-attach, property-store growth, composition-visual creation, and binding re-source on every scroll recycle.
- Fix: recycle IN-PLACE. Replace the horizontal `VirtualizingStackPanel` with a purpose-built `VirtualizingPanel` that keeps column containers attached (`IsVisible=false` when idle, pushed to an idle stack) and re-points DataContext on reuse — the mechanism Avalonia's own DataGrid uses (`DataGridRowsPresenter` keeps rows in `Children`, sets `IsRecycled=true` once, never removes from the tree). This eliminates the re-attach, killing the three attach-specific allocators outright.
- Rejected alternatives (with reasons in Solution Overview): de-styling the cells (~11% ceiling, not worth it), a full custom control abandoning ListBox, a hybrid always-attached layer.

## Context (from discovery + design review)
- Files/components:
  - `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml` — `ListBox` `StepListBox`, `ItemsPanel` = horizontal `VirtualizingStackPanel`, `ItemsSource=StepColumns`, `SelectionMode=Multiple`; frozen parameter-name `ItemsControl` on the left; outer vertical `ScrollViewer`, ListBox scrolls horizontally. Column width = `{DynamicResource TransposedStepColumnWidth}` (uniform across columns).
  - `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml.cs` — wires `ContainerPrepared`/`ContainerClearing` (class binder), `SelectionChanged`, Tunnel `PointerPressed` (cell selection/edit entry), Tunnel `KeyDown` (navigation), `OnSelectionRequested`/`ScrollIntoView`. Uses `TransposedGridNavigator`, `TransposedGridSelectionController`, `TransposedTextEditCoordinator`, `TransposedStepColumnClassBinder`.
  - `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedColumnCellsHost.cs` — Decorator; on attach acquires a pooled presenter and binds the column; syncs `IsColumnSelected` from the container ListBoxItem. Its comment already anticipates stable container identity.
  - `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedColumnCellsPresenter.cs` — pooled cell subtree (non-virtualizing StackPanel of ~36 cell Borders); `OnDataContextBeginUpdate` (commit backstop) + `CommitActiveEditor`.
  - `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedColumnCellsPool.cs` — per-surface presenter pool.
  - `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedTextEditCoordinator.cs`, `TransposedGridNavigator.cs`, `TransposedGridSelectionController.cs`, `TransposedStepColumnClassBinder.cs`, `TransposedGridCellLocator.cs` (walks `e.Source` ancestors to the ListBox).
- Avalonia 12.0.3 integration facts (source-verified during design):
  - `ItemContainerGenerator` is stateless; the panel owns realization. Its remarks state recycled containers should be hidden (`IsVisible=false`), NOT removed from the panel — a custom `VirtualizingPanel` honoring this is blessed by the contract; VSP violates it.
  - `ItemsPresenter.ApplyTemplate` attaches any `VirtualizingPanel` from the `ItemsPanelTemplate`; `ContainerFromIndex`/`IndexFromContainer`/`GetRealizedContainers`/`ScrollIntoView`/`Refresh` dispatch to the panel's abstract overrides.
  - `SelectingItemsControl.ContainerForItemPreparedOverride` re-syncs `IsSelected` per realize; `ClearContainerForItemOverride` clears it per unrealize; `UpdateContainerSelection` iterating all `Children` marks idle (hidden) containers deselected — benign.
  - VSP recycle sites are `private`/non-virtual (cannot subclass-patch), so a new panel is required, not a VSP subclass.
- Cached reference: Avalonia 12.0.3 source (`VirtualizingPanel`, `VirtualizingStackPanel`, `ItemsControl`, `ItemContainerGenerator`, `ItemsPresenter`, `SelectingItemsControl`) is available under the session scratchpad `avalonia/` for the implementation phase; re-fetch via GitHub MCP (`AvaloniaUI/Avalonia`, tag 12.0.3) if absent.

## Development Approach
- **testing approach**: Regular (code first, then tests).
- Uniform column width is load-bearing: it makes viewport math exact (`firstIndex = floor(viewportX / W)`, extent = `N * W`), removing VSP's size-estimation/anchor complexity. The panel MUST assume a single fixed `ColumnWidth`.
- Each task fully green before the next. Every task adds/updates headless tests. Preserve behavior: multi-select, tunnel pointer/keyboard, class binding, name-column alignment, ScrollIntoView, edit-commit on recycle.
- **Risk gate on Task 1**: if the keep-attached contract does not hold headlessly (containers still get `DetachedFromVisualTree` on scroll, or ItemsControl fights it), STOP and switch to the fallback (fork `VirtualizingStackPanel` + patch the ~4 recycle sites to keep-attached). Do not force the custom-panel path past a failing contract test.

## Testing Strategy
- **headless UI tests** (`[AvaloniaFact]`): container reuse across scroll (same instances, no `DetachedFromVisualTree`, bounded `Children.Count`), `ContainerPrepared`/`Clearing` firing, `IsSelected` re-sync, realized-range vs offset, focus-within survives scroll-out, items add/remove/move index mapping, Reset teardown, ScrollIntoView far-index, edit begin→scroll-away→committed, selection correctness after scroll round-trip, name-column row-alignment (bounds comparison).
- **performance gate (manual, Post-Completion)**: same 2100-step recipe + scroll protocol, gc-verbose allocation-by-method; realization-subtree allocation share must drop from ~41–56% to <20%, with `Dictionary.Resize`/`CreateCompositionVisual`/`StyleBase.Attach`/`Setter.Instance` absent from the scroll path.
- **no e2e**: headless Avalonia is the ceiling.

## Progress Tracking
- Mark `[x]` immediately. `➕` new tasks, `⚠️` blockers.

## Solution Overview
- **Chosen: option (a)** — `TransposedColumnsPanel : VirtualizingPanel`, recycle-in-place, inside the existing `ListBox`/`ItemsControl` (selection, generator, ContainerPrepared/Clearing all keep working). ~400 lines of owned, testable code; uniform width deletes VSP's hard parts.
- **Rejected (b)** full custom control (re-implements SelectionModel/generation/keyboard/ScrollIntoView — weeks, and DataGrid proves the fix lives in the panel, not the control). **Rejected (c)** hybrid always-attached cell layer (breaks `TransposedGridCellLocator` ancestor walk, splits hit-testing, needs per-frame offset sync).
- **Pool**: kept, demoted to per-surface presenter factory. Under in-place recycle the host never detaches on scroll, so it holds one presenter for life; the pool still serves the surface-swap lifecycle (Reset = real teardown → hosts release presenters into the dying pool). `TransposedColumnCellsHost` stays byte-for-byte unchanged.

## Technical Details
`TransposedColumnsPanel : Avalonia.Controls.VirtualizingPanel`:
- **State**: `Dictionary<int, Control> _realized`; `Stack<Control> _idle`; `ColumnWidth` StyledProperty, marked `AffectsMeasure` (bound in the `ItemsPanelTemplate` to the `TransposedStepColumnWidth` resource; runtime width changes currently arrive only with a new surface = Reset, so no live re-flow requirement — record that assumption); viewport `Rect` from `EffectiveViewportChanged` (same trigger VSP uses).
- **MeasureOverride**: desired = `(Items.Count * ColumnWidth, maxRealizedChildHeight)`; compute exact realize window from viewport (+ small fixed buffer, e.g. 1–2 columns each side); realize that range, unrealize the rest; measure realized children only. Also measure the deferred (TabOnceActiveElement) container if it sits outside the window (VSP does this — the deferred element still gets laid out).
- **Realize(index)**: pop `_idle` (else `generator.NeedsContainer`/`CreateContainer`, then `PrepareItemContainer`, then `AddInternalChild`, then `ItemContainerPrepared` — the generator contract order, matching `VSP.CreateElement`; `AddInternalChild` happens once per physical slot, ever); on reuse: `IsVisible=true`; `generator.PrepareItemContainer(container, item, index)` (re-points DataContext, re-syncs IsSelected); `generator.ItemContainerPrepared(container, item, index)` (fires `ContainerPrepared`). NO `AddInternalChild` on reuse.
- **Unrealize(index)**: `generator.ClearItemContainer(container)` (fires `ContainerClearing`, clears IsSelected); `IsVisible=false`; push `_idle`. NO `RemoveInternalChild`. **Focus/edit deferral (mirror VSP exactly, do NOT simplify to `IsKeyboardFocusWithin`)**: defer unrealizing the container equal to `KeyboardNavigation.GetTabOnceActiveElement(ItemsControl)` (the selection-anchor container — public API, verified 12.0.3), keep measuring/arranging it while deferred, and add a release listener on `TabOnceActiveElementProperty` that unrealizes it once the anchor moves. This is what lets a *selected* open editor scroll offscreen and commit on focus loss, while an *unselected* editor is unrealized (→ `ContainerClearing` → commit hook). Getting this wrong makes the commit-on-scroll-out and focus-survives-scroll-out tests mutually unsatisfiable.
- **ArrangeOverride**: realized (and the deferred anchor) at `(index * W, 0, W, height)`; idle skipped (hidden). Exact uniform-width extent means scroll anchoring is not needed for offset stability (positions never drift from estimation); do not add anchor registration unless a test proves an insert-before-viewport offset jump.
- **ScrollIntoView(index)**: realized → `BringIntoView()`; else realize eagerly, arrange at the exact rect, `BringIntoView()` + one guarded `UpdateLayout()` (public `Layoutable.UpdateLayout`, already the codebase idiom in `TransposedGridNavigator.MoveToNeighborColumn`; `LayoutManager.ExecuteLayoutPass`/`GetLayoutRoot` are INTERNAL in 12.0.3 and unreachable). Guard reentrancy with `_isInLayout`. Exact extent means no triple-pass compensation.
- **OnItemsChanged**: Add → shift `_realized` keys up, invalidate measure. Remove/Replace → **unrealize the removed items' containers** (VSP `RecycleElementOnItemRemoved`), then shift remaining `_realized` keys, `generator.ItemContainerIndexChanged` for shifted realized containers. Move → re-key. Reset → teardown: `ClearItemContainer` on realized containers, `RemoveInternalChild` on ALL children (realized + idle), clear `_idle` (idle were already cleared at unrealize, so do not double-`ClearItemContainer` them) — deliberate, drives pool release on surface swap.
- **Abstract overrides**: `GetRealizedContainers`, `ContainerFromIndex`, `IndexFromContainer`, and `GetControl(NavigationDirection, from, wrap)` — implement `GetControl` like VSP (resolve the direction to a target index and call our own `ScrollIntoView` to realize it), NOT realized-only index±1. The tunnel handler only owns plain arrows (it bails on any modifier); Shift+Arrow / Home/End / Page fall through to ListBox → `GetControl`, so a realized-only implementation would dead-end multi-select keyboard nav at the realization boundary.
- Peak `Children.Count` ≈ viewport columns + buffer (~20–25), never 2100.
- **Eliminated per recycle**: `StyleBase.Attach`+`Setter.Instance` (~10%), property-store `Dictionary.Resize` (~18.9%), `CreateCompositionVisual` (~9%) — built once per container, live forever. **Remaining**: the DataContext re-point notification wave (a subset of the 29.5% `PublishNext`), inherent to showing different data and already paid today via `BindColumn`.

## What Goes Where
- **Implementation Steps** (checkboxes): the panel, its integration, tests.
- **Post-Completion** (no checkboxes): the manual gc-verbose before/after gate, and the fallback (VSP fork) if Task 1's contract fails.

## Implementation Steps

### Task 1: Panel skeleton — keep-attached recycle, contract proven (RISK GATE)

**Files:**
- Create: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedColumnsPanel.cs`
- Create: `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/TransposedColumnsPanelContractTests.cs`

- [x] Create `TransposedColumnsPanel : VirtualizingPanel` with `ColumnWidth` StyledProperty (`AffectsMeasure`), `_realized`/`_idle`, minimal `MeasureOverride`/`ArrangeOverride` realizing a viewport-derived index range and hiding (`IsVisible=false`, no `RemoveInternalChild`) the rest; realize via generator contract order (`CreateContainer`→`PrepareItemContainer`→`AddInternalChild`→`ItemContainerPrepared`) with `AddInternalChild` only on first realize, unrealize via `generator.ClearItemContainer`. Implement `GetRealizedContainers`/`ContainerFromIndex`/`IndexFromContainer`, and **stub the two remaining abstract members so it compiles and is null-safe**: `ScrollIntoView` (realized-path `BringIntoView`, else no-op for now — `AutoScrollToSelectedItem` defaults true and WILL call this) and `GetControl` (return null for now). No `NotImplementedException`.
- [x] Inject the panel into the REAL `TransposedRecipeGridView` for tests via a test-only `ItemsPanelTemplate` override on `StepListBox` (a two-line helper), NOT a scratch view — so Task 4's view-wiring tests and the existing transposed suite can run against the panel from here on. (Task 5 then becomes just the production `.axaml` template line-swap.)
- [x] Write test: scrolling reuses the SAME container instances (no new instances beyond peak) and NO container raises `DetachedFromVisualTree` during scroll.
- [x] Write test: `Children.Count` stays bounded (≈ viewport + buffer) at large N.
- [x] Write test: `ContainerPrepared`/`ContainerClearing` fire per realize/unrealize; `IsSelected` re-syncs from the selection model on realize.
- [x] Write **focus contract** test (gate-critical): a container that is the `TabOnceActiveElement` (selection anchor) is NOT unrealized when scrolled out of the window (deferred), and IS unrealized once the anchor moves. Verify hiding a focused editor headlessly does not corrupt focus/LostFocus.
- [x] Write **selection-model contract** test (gate-critical): multi-select two columns → scroll them out (idle) → touch the selection model (or change selection) → scroll back → both columns still selected in BOTH the model and the container. (If this fails, `container.ClearValue(ListBoxItem.IsSelectedProperty)` before `PrepareItemContainer` in `Realize` restores VSP-equivalent state — apply only if the test demands it.)
- [x] **GATE**: if any contract test (reuse, focus, or selection-model) cannot pass (ItemsControl forces detach, generator/selection misbehaves under keep-attached), STOP, mark this task `⚠️`, and record the fallback decision (VSP fork per Post-Completion) — do not proceed to Task 2.
- [x] Run tests — must pass before Task 2.

### Task 2: Panel core — exact viewport math, measure/arrange, focus deferral

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedColumnsPanel.cs`
- Modify: `SemiStep/SemiStep.Tests/UI/RecipeGrid/Transposed/TransposedColumnsPanelContractTests.cs` (or a new `TransposedColumnsPanelLayoutTests.cs`)

- [x] Implement exact math: `firstIndex = floor(viewportX / ColumnWidth)`, extent width = `Items.Count * ColumnWidth`, per-index arrange rect `(index*W, 0, W, height)`; viewport from `EffectiveViewportChanged`; fixed realize buffer.
- [x] Implement the `TabOnceActiveElement` deferral fully: defer unrealizing that container, keep measuring/arranging it while deferred, and release (unrealize) it via a `TabOnceActiveElementProperty` listener when the anchor moves.
- [x] Write test: realized index range matches the scroll offset (+buffer); idle children are hidden and not arranged.
- [x] Write test: the `TabOnceActiveElement` container survives scroll-out (deferred, still laid out) and is unrealized once the anchor moves.
- [x] Write test: desired size width = `N * ColumnWidth`; arrange positions match `index * W`.
- [x] Run tests — must pass before Task 3.

### Task 3: Items-changed handling — add/remove/move + Reset teardown

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedColumnsPanel.cs`
- Modify: tests file

- [x] Implement `OnItemsChanged`: Add → shift `_realized` keys up + invalidate measure; Remove/Replace → unrealize the removed items' containers first, then shift remaining keys + `generator.ItemContainerIndexChanged` for shifted realized containers; Move → re-key; Reset → teardown (`ClearItemContainer` realized, `RemoveInternalChild` all children, clear `_idle`; do not double-clear already-idle containers).
- [x] Write test: insert/remove a step mid-scroll keeps the DataContext↔index mapping correct for realized containers (and the removed item's container is unrealized, not left mapped).
- [x] Write test: append step at end grows the extent (desired width = `(N+1)*W`); scrolling to max horizontal offset realizes the new last column (offset-based, no dependency on Task 4's `ScrollIntoView`).
- [x] Write test: surface swap (RecipeReplaced / new pool) triggers Reset teardown → containers physically detach → presenters released (no stale-descriptor reuse).
- [x] Write test: run the EXISTING transposed suite (virtualization, selection, editing, navigation, viewport-jump) against the injected panel — it must stay green (catches regressions early, before the production swap).
- [x] Run tests — must pass before Task 4.

### Task 4: ScrollIntoView — exact positioning

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedColumnsPanel.cs`
- Modify: tests file

- [x] Implement `ScrollIntoView(index)`: realized → `BringIntoView()`; else realize eagerly, arrange at the exact rect, `BringIntoView()` + one guarded (`_isInLayout`) `UpdateLayout()` (public API; NOT the internal `ExecuteLayoutPass`).
- [x] Implement `GetControl(direction, from, wrap)` VSP-style: resolve the direction to a target index and call our `ScrollIntoView` to realize it, returning that container (so ListBox keyboard nav past the realization boundary works).
- [x] Write test: a far-index selection request (`OnSelectionRequested`) realizes and positions the target column.
- [x] Write test: add-step auto-scroll path (append + RequestSelection) brings the new column into view.
- [x] Write test: navigator `MoveTo`/neighbor-column across the realization boundary resolves the right container.
- [x] Write test: Shift+Right (range-extend) across the realization boundary extends the selection (exercises `GetControl` → realize).
- [x] Run tests — must pass before Task 5.

### Task 5: Integrate into the view + commit-on-clearing

**Files:**
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml` (ItemsPanel → `TransposedColumnsPanel`, bind `ColumnWidth` to the `TransposedStepColumnWidth` resource)
- Modify: `SemiStep/SemiStep.UI/RecipeGrid/Transposed/TransposedRecipeGridView.axaml.cs` (commit-active-editor in `OnContainerClearing`)
- Modify: tests file(s)

- [ ] Swap the production `ItemsPanelTemplate` in `.axaml` from `VirtualizingStackPanel` to `TransposedColumnsPanel` with the `ColumnWidth` binding (a template line-swap; the panel was already injected for tests since Task 1, so this only flips production wiring).
- [ ] In `OnContainerClearing`, additionally walk the container and call `TransposedColumnCellsPresenter.CommitActiveEditor()` (the unrealize path replaces the old detach-driven commit for the UNSELECTED-editor case).
- [ ] Write test (unselected-editor branch): begin editing a cell in an UNSELECTED column → scroll the column out → container is unrealized → value committed and `IsEditing` false (this is what `TransposedVirtualizationTests` pins today via detach).
- [ ] Write test (selected-editor branch): begin editing a cell in the SELECTED (anchor) column → scroll out → container is DEFERRED (still editing offscreen) → move focus/anchor away → value committed. Confirm both branches, since an editing cell holds focus and the two go through different paths.
- [ ] Write test: selection is correct after a scroll round-trip (select, scroll away, scroll back, still selected in model + container).
- [ ] Write test: frozen name-column rows stay row-aligned with the scrolling columns (bounds comparison).
- [ ] Run tests — must pass before Task 6 (the existing suite already runs against the panel from Task 3).

### Task 6: Verify acceptance criteria
- [ ] Confirm the panel replaces VSP and all must-keep behaviors are covered by passing tests.
- [ ] Run full suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`
- [ ] Run formatter: `dotnet format "SemiStep/SemiStep.slnx" --verify-no-changes`
- [ ] The gc-verbose before/after allocation gate is a MANUAL step (needs the live app) — see Post-Completion; mark `[x]` with that note.

### Task 7: Documentation and cleanup
- [ ] Update `Docs/architecture/recipe-grid-surface.md`: document the recycle-in-place `TransposedColumnsPanel`, why it replaced `VirtualizingStackPanel` (per-recycle detach/re-attach allocation), and that the pool is now a per-surface factory. Record the allocation-gate result.
- [ ] Retire dead pool-cycling comments in the host/template that no longer describe scroll-time behavior.
- [ ] No `CLAUDE.md` change expected (project footer forbids specifics).
- [ ] Move this plan to `Docs/plans/completed/`.

*(The class-binder "bind once" optimization is deliberately NOT in this plan — no existing test pins idle-container class state, so the suite could stay green while hidden containers silently carry stale-row class bindings. It is recorded as a Post-Completion follow-up.)*

## Post-Completion
*Manual / external — no checkboxes*

**Performance gate (manual, the acceptance metric):**
- Reproduce on the RELEASE `RIE` config, ~2100-step recipe, same scroll protocol. Capture before/after:
  ```
  dotnet-trace collect --name SemiStep.UI --profile gc-verbose --duration 00:00:20
  ```
  Convert (`dotnet-trace convert --format speedscope`) and compare allocation-by-method on the UI thread. Acceptance: realization-subtree allocation share drops from ~41–56% to <20%; `Dictionary.Resize`, `CreateCompositionVisual`, `StyleBase.Attach`/`Setter.Instance` absent from the scroll path. Residual `PublishNext` from DataContext rebinding is expected and acceptable.

**Fallback (if Task 1 contract fails):**
- Fork `VirtualizingStackPanel` + `RealizedStackElements` (~1,900 lines) and patch the ~4 recycle sites (`RecycleElement`/`GetRecycledElement` and their fully-recycled variants) to keep-attached (`IsVisible` toggle instead of `RemoveInternalChild`/`AddInternalChild`). Caveat verified during review: VSP's `ScrollIntoView` uses `GetLayoutRoot()` + `LayoutManager.ExecuteLayoutPass()`, both INTERNAL in 12.0.3 — the fork MUST substitute the public `Layoutable.UpdateLayout()` there. Accepted maintenance drift against upstream 12.x.

**Behavior tradeoffs recorded under keep-attached (verified, accepted):**
- `ClearItemContainer` does NOT clear `DataContext` (ItemsControl contract), so ~20 hidden idle subtrees keep live bindings to stale columns and receive PLC-driven notifications during a run. Harmless visually; clearing DataContext at unrealize would trade it for rebind churn on re-show, so it is correctly not done.
- Idle containers remain in the visual tree (hidden), so they may appear in the automation/accessibility tree (DataGrid's own keep-attached rows share this exposure). Add a one-line manual accessibility spot-check when validating.

**Follow-up (separate, recorded from earlier rounds, not this plan):**
- Element-count reduction (merge the slot Border with `TransposedLazyCellPresenter`'s Border) trims `CreateCompositionVisual` further — secondary, do only if the gate leaves GC hot.
- Class-binder "bind once" for stable containers (needs a new idle-container class-state test first).
- The stale-after-insert selection gap, `Monitor.Enter` contention residue, and `ExtendSelectionTo` O(K²) batching remain out-of-scope follow-ups.
