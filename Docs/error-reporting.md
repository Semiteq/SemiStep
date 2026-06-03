# Error Reporting Contract

How a failed operation reaches the user. Read this before adding a new error-surfacing site.

## The two MessagePanel channels

`MessagePanelViewModel` holds two independent channels that it merges into one `Entries` list:

- **Transient operation channel** (`_operationEntry`). A single slot for the outcome of the most
  recent user-initiated operation. Written through `ReportError` / `ReportWarning` / `ReportSuccess`
  and cleared on the next successful mutation (`ClearOperation`). One operation entry at a time; a
  new outcome overwrites the previous one. Not counted in the panel's error/warning totals.
- **Persistent validation channel** (`_validationEntries`). The structural validity of the *current*
  recipe. Rebuilt wholesale from the recipe snapshot's `IReason` list through `RefreshReasons`.
  These entries drive the status-bar error/warning counts and persist until the recipe changes.

## Ownership

- **Core owns the error text.** Failures are `FluentResults.Result` with typed `IError` reasons; the
  message string lives in Core, never in the UI.
- **The UI only routes.** A view model takes a failed `Result` and hands it to the panel; it does not
  compose error wording beyond an optional operation prefix (e.g. `"Step 3"`).

## The reporting seam

`Result`-to-panel routing goes through `ResultReportingExtensions`
(`SemiStep.UI/MessageService/`):

- `string FormatErrors(this IResultBase)` — joins every error message with `"; "`. The default idiom
  for rendering a failed result, used by both panel sites and log statements.
- `void ReportFailure(this MessagePanelViewModel, IResultBase, string? context = null)` — surfaces
  `FormatErrors()` on the transient operation channel, optionally prefixed with `"{context}: "`.

A failed operation surfaces **all** of its error messages, matching what the log records. Do not
read `Errors[0]` directly.

## Routing rules

- **Transient operation outcomes** (save/load failed, paste rejected, step edit rejected) go to the
  operation channel via `ReportFailure` (or `ReportError` when the message is composed mid-string).
- **Persistent structural state** (the current recipe's validity) goes to the validation channel via
  `RefreshReasons`, and from there to the status-bar counts.
- **PLC connection-loss / sync failures** arrive as a typed `IError` on `IPlcSyncService.Faults`, a
  discrete one-shot channel. `RecipeCoordinator` bridges each fault to the operation channel via
  `ReportError(error.Message)` — the message text is owned in Core. The persistent "disconnected"
  label stays in the status bar.
- **Exception handlers** (`ReportError($"... {ex.Message}")` on `ThrownExceptions`) are not
  `Result`-based and stay as-is.
- **One deliberate exception** to the `"; "` idiom: the clipboard *paste* failure lists each error on
  its own line (`Environment.NewLine`), because a rejected paste can carry many per-step errors that
  read better stacked. It still surfaces every error (never just `Errors[0]`).
