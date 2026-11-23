# Generate the resx accessor

## Behavior
- Today `SemiStep.UI/Localization/Resources.Designer.cs` (478 lines) is written by hand, one property
  per key, because MSBuild's strongly-typed resource generation runs after the Avalonia XAML compiler
  and a cold build then fails with `AVLN2000` on every `{x:Static l:Resources.*}`. A key added to
  `Resources.resx` without its property is invisible until someone tries to use it, and a property
  left behind after a key is removed compiles and fails at run time. One of the four
  `ResourceSyncTests` checks catches that drift. The other three check that `Resources.ru.resx` has
  every neutral key and no orphan key, that no value is blank, and that the `{n}` placeholders match
  between English and Russian.
- After this change `Microsoft.CodeAnalysis.ResxSourceGenerator`, a Roslyn source generator, writes
  the accessor inside the compiler, before XamlIl runs. The hand-written file is deleted. The
  key-to-accessor check is deleted, and the three translation checks move unchanged to
  `ResourceTranslationTests`. Adding a key to `Resources.resx` makes `Resources.<Key>` available;
  removing it removes the property, and every remaining use fails the build.
- The UI looks and behaves the same: same keys, same Russian and English text, same culture
  switching through `Resources.Culture` and the thread UI culture set in `SemiStep.UI/Program.cs`.
- Scope guard: no resource key, value or language changes. `SemiStep.Core` is untouched.

## Flow branches
| Situation | The app does | Decided by | Checked by |
|---|---|---|---|
| Cold build (no `obj/`, no `bin/`) | Builds with 0 warnings; every `{x:Static l:Resources.*}` resolves on the first pass | issue #186 | A1 |
| `locale: ru` | UI text from `Resources.ru.resx` | existing behavior | A2 (`CoreErrorLocalizationCoverageTests` ru case), A5 smoke |
| `locale: en` | UI text from the neutral `Resources.resx` (English) | existing behavior | A2 (`CoreErrorLocalizationCoverageTests` en case), A5 smoke |
| A key used in AXAML or C# is missing from `Resources.resx` | Build fails (`AVLN2000` in AXAML, `CS0117` in C#) | issue #186 | A4 |
| Code or tests that read `Resources.Culture`, `Resources.ResourceManager` or a key property | Compile and behave as today | agent: probed generated surface, see Caveats | A1, A2 |
| A neutral key is missing from `Resources.ru.resx`, a value is blank, or the `{n}` placeholders differ | `ResourceTranslationTests` fails | existing behavior (`docs/plans/completed/20261118-test-suite-cleanup.md`) | A2 |

## Decisions
- Use `Microsoft.CodeAnalysis.ResxSourceGenerator`, version pinned exactly as in SemiPlot
  (`3.12.0-beta1.25218.8`, `SemiPlot/Directory.Packages.props`), with `PrivateAssets="all"`, and
  `<EmbeddedResource Update="Localization/Resources.resx" GenerateSource="true"/>`. Reason: it runs
  inside the compiler, so XamlIl sees the class on a cold build; SemiPlot measured the same
  `AVLN2000` failure and fixed it this way. Rejected: MSBuild `StronglyTypedResourceBuilder` metadata
  (the race this file exists to avoid), keeping the hand-written file (the drift the issue is about).
- The generated class stays `internal` (the generator default). XamlIl rewrites the same assembly, so
  AXAML reads an internal class, and `SemiStep.UI.csproj` already has
  `<InternalsVisibleTo Include="SemiStep.Tests"/>`. No other project uses `Resources`. Rejected:
  `Public="true"`, which has no consumer.
- `ResourceSyncTests.EveryNeutralKey_HasDesignerAccessor_AndNothingElse` is deleted: it pinned the
  hand sync between keys and properties, which the generator now guarantees by construction. The
  other three checks (Russian key set, non-empty values, placeholder sets) move unchanged to
  `ResourceTranslationTests`. Reason: they read the raw resource sets with `tryParents: false`
  through `Resources.ResourceManager`, which the internal class still exposes to the test project,
  and nothing else catches a missing Russian key (the UI silently shows English) or a placeholder
  mismatch (`FormatException` at run time). `docs/plans/completed/20261118-test-suite-cleanup.md`
  kept the same three checks. The file gets a new name so that Acceptance 3 still holds.
- `NeutralLanguage` is `en`. Reason: without it the `AnalysisMode=Recommended` baseline under
  `TreatWarningsAsErrors` fails the build with CA1824 once the accessor carries the generator's
  standard `GeneratedCode` attribute (SemiPlot records the same). Rejected: suppressing CA1824.
- `EmitFormatMethods` stays off: SemiStep formats composite text in view models.
- No new culture test: `CoreErrorLocalizationCoverageTests` already resolves every Core reason
  through `Resources.*` under `ru` (text differs from English) and under `en` (text equals the
  message), switching culture with `Resources.Culture`.

## Caveats
- Generated surface. probed: template strings read from the pinned package DLL
  (`microsoft.codeanalysis.resxsourcegenerator/3.12.0-beta1.25218.8/analyzers/dotnet/cs/`) and
  `dotnet msbuild SemiStep/SemiStep.UI/SemiStep.UI.csproj -getProperty:RootNamespace` (`SemiStep.UI`),
  2026-11-23. The class is `static partial Resources` in `SemiStep.UI.Localization` (RootNamespace plus
  folder), with `Culture { get; set; }`, `ResourceManager` built from `typeof(Resources)` (base name
  `SemiStep.UI.Localization.Resources`, which matches the embedded resources), and one
  `public static string` property per key reading `ResourceManager.GetString(key, Culture)`. Nothing
  uses `Resources` as an instance or generic type, so the static class is harmless.
- ASSUMPTION: `Culture` is `CultureInfo?`; `ResourcesCultureScope` stores and restores a nullable
  value and `ReasonLocalizer` uses `Resources.Culture ?? ...`; fallback: adjust `ResourcesCultureScope`
  to the generated type; checked by: A1 (CS8601 would fail the build).
- SemiPlot proves the generator on net10. A1 is the check on the .NET 11 SDK with C# 15.

## Constraints and non-goals
- No new key, no changed value, no new language.
- No change to `SemiStep.Core` or to the config files.
- Tests stay simple.

## Gates
- none

## Acceptance
1. `rm -rf SemiStep/Artifacts/bin SemiStep/Artifacts/obj && dotnet build SemiStep.slnx` → 0 warnings,
   0 errors on that first, cold build.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj` → 0 failed (localization and headless UI
   tests included).
3. `test ! -e SemiStep/SemiStep.UI/Localization/Resources.Designer.cs && test ! -e SemiStep/SemiStep.Tests/UI/Localization/ResourceSyncTests.cs`
   → exit 0.
4. Scratch edit: add `{x:Static l:Resources.NoSuchKey}` to one AXAML file, run
   `dotnet build SemiStep/SemiStep.UI/SemiStep.UI.csproj` → fails with `AVLN2000`; revert. Same with
   `Resources.NoSuchKey` in a C# file → `CS0117`; revert.
5. Manual smoke: start SemiStep with MBE (Russian UI), then with `locale: en` in a scratch copy
   (English UI); menus and the status bar show the expected language.

---

## Context
- `SemiStep/SemiStep.UI/SemiStep.UI.csproj`, `SemiStep/Directory.Packages.props`,
  `SemiStep.UI/Localization/{Resources.resx,Resources.ru.resx,Resources.Designer.cs}`.
- Consumers of the accessor surface: `SemiStep.UI/Program.cs` (sets `Resources.Culture`),
  `Localization/LocalizedText.cs` and `ReasonLocalizer.cs`, tests `ResourcesCultureScope`,
  `MainWindowViewModelSyncStateTests`, `ResourceSyncTests` (renamed to `ResourceTranslationTests`).
- SemiPlot reference wiring: `SemiPlot/SemiPlot/SemiPlot.UI/SemiPlot.UI.csproj`,
  `SemiPlot/docs/architecture/ui-text.md` ("The generated accessor").
- Docs: `docs/architecture/ui-localization.md` (section "Gotcha: the accessor is a committed source
  file", the "adding a key" steps, and the "designer accessor" wording further down).

## Tasks

### Task 1: Replace the hand-written accessor with the generator

**Goal:** the generator writes `Resources`, the hand-written file and its key-to-accessor check are gone, a cold build passes and both cultures still resolve
**Entry:** `SemiStep.UI.csproj`
**Likely touches:** `SemiStep.UI.csproj`, `Directory.Packages.props`, `Resources.Designer.cs` and `ResourceSyncTests.cs` (both deleted with `git rm`), `ResourceTranslationTests.cs` (new), `ResourcesCultureScope.cs` if the Caveat fallback applies

- [x] add the pinned generator reference, the `EmbeddedResource` item with `GenerateSource="true"`, and `NeutralLanguage` `en`
- [x] `git rm` `Resources.Designer.cs` and `ResourceSyncTests.cs`; move the three translation checks unchanged to `ResourceTranslationTests.cs` and drop only the key-to-accessor check
- [x] cold build: remove `SemiStep/Artifacts/bin` and `SemiStep/Artifacts/obj`, build once, 0 warnings
- [x] tests: the existing `CoreErrorLocalizationCoverageTests` ru and en cases pass (no new test)
- [x] run tests - must pass before the next task

### Task 2: Verify acceptance

**Goal:** every Behavior line, Flow branch and Acceptance item holds
**Entry:** the plan's `## Acceptance` section

- [x] every Behavior line and Flow branch is implemented
- [x] Acceptance 1 to 4 pass (Acceptance 4 edits are reverted)
- [x] full test suite: `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj`

### Task 3: [Final] Update documentation

**Goal:** the docs describe the generated accessor
**Entry:** `docs/architecture/ui-localization.md`

- [x] rewrite the "committed source file" section: the generator writes an internal accessor; the `AVLN2000` explanation stays as the reason MSBuild generation is not used; `NeutralLanguage` is there for CA1824
- [x] update the "adding a key" steps (no accessor step; `ResourceTranslationTests` catches a missing Russian value) and remove the remaining "designer accessor" wording
- [x] CLAUDE.md only if a convention changed (no change needed: it holds no resx convention)

## Post-Completion
- the operator's manual test: Acceptance 5
- closes #186

**Executed by exec:**
- branch: resx-source-generator

## Verify it yourself
1. `rm -rf SemiStep/Artifacts/bin SemiStep/Artifacts/obj && dotnet build SemiStep.slnx` → 0 warnings on the
   first, cold build. Before this change the same works only because `Resources.Designer.cs` is committed.
2. `dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~ResourceTranslationTests"`
   → 3 passed. Remove one key from `Resources.ru.resx` in a scratch edit → `RussianSatellite_ContainsEveryNeutralKey_AndNoOrphans`
   fails; restore the file.
3. Add a key to `Resources.resx` and `Resources.ru.resx`, use `Resources.<Key>` in C# → it compiles with no
   third file to edit. Remove it again.
4. Start SemiStep with MBE (Russian UI), then with `locale: en` in a scratch copy (English UI).
