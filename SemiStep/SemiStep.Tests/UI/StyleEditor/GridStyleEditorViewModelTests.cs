using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using Avalonia.Headless.XUnit;
using Avalonia.Media;

using FluentAssertions;

using FluentResults;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.Tests.Config.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Localization;
using SemiStep.UI.Localization;
using SemiStep.UI.StyleEditor;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.StyleEditor;

[Trait("Component", "UI")]
[Trait("Category", "Unit")]
public sealed class GridStyleEditorViewModelTests
{
	private const string ConfigDir = @"C:\does-not-exist";
	private const int SurfacedEditablePropertyCount = 77;

	[AvaloniaFact]
	public void Seed_ThenBuildRecord_PreservesEveryFieldDistinctly()
	{
		var viewModel = CreateViewModel(GridStyleOptionsTestData.Distinct());

		viewModel.BuildRecord().Should().Be(GridStyleOptionsTestData.Distinct());
	}

	[AvaloniaFact]
	public void BuildRecord_PerturbingEachLeaf_ChangesOnlyThatMappedField()
	{
		var viewModel = CreateViewModel(GridStyleOptionsTestData.Distinct());

		var leaves = SurfacedLeaves(viewModel);
		leaves.Should().HaveCount(
			SurfacedEditablePropertyCount,
			"the perturbation guard must cover every surfaced editable leaf");

		foreach (var (groupName, draft, leaf) in leaves)
		{
			var seededValue = leaf.GetValue(draft);
			var baseline = viewModel.BuildRecord();

			leaf.SetValue(draft, Perturb(leaf, seededValue));
			var built = viewModel.BuildRecord();

			// Derived, not mapped: the draft leaf name mirrors the record component name by design, so
			// the record path is {GroupProperty}.{LeafProperty}. A name that diverges cannot hide — it
			// makes this walk report a different path or the per-draft round-trip guard go red.
			var mappedField = $"{groupName}.{leaf.Name}";
			var changedFields = ChangedRecordFields(baseline, built);
			changedFields.Should().BeEquivalentTo(
				new[] { mappedField },
				$"editing {mappedField} must change exactly that record field");

			leaf.SetValue(draft, seededValue);
		}
	}

	[AvaloniaFact]
	public void BuildRecord_AfterEditingColorAndFontSize_ChangesOnlyThoseFields()
	{
		var source = GridStyleOptions.Default;
		var viewModel = CreateViewModel(source);

		viewModel.Fonts.CellFontSize = 18;
		viewModel.Selection.Background = Color.Parse("#123456");

		var record = viewModel.BuildRecord();

		var expected = source with
		{
			Fonts = source.Fonts with { CellFontSize = 18 },
			Selection = source.Selection with { Background = StyleColor.Parse("#123456") }
		};
		record.Should().Be(expected);
	}

	[AvaloniaFact]
	public void BuildRecord_RoundsFractionalStatusBarFontSize_ToNearestInt()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.StatusBar.FontSize = 14.6m;

		viewModel.BuildRecord().StatusBar.FontSize.Should().Be(15);
	}

	[AvaloniaTheory]
	[InlineData(5)]
	[InlineData(73)]
	public void CanSave_IsFalse_WhenStatusBarFontSizeOutOfRange(int fontSize)
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.StatusBar.FontSize = fontSize;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaTheory]
	[InlineData(5)]
	[InlineData(73)]
	public void CanSave_IsFalse_WhenStatusBarTimerValueFontSizeOutOfRange(int fontSize)
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.StatusBar.TimerValueFontSize = fontSize;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaTheory]
	[InlineData(5)]
	[InlineData(73)]
	public void CanSave_IsFalse_WhenStatusBarTimerLabelFontSizeOutOfRange(int fontSize)
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.StatusBar.TimerLabelFontSize = fontSize;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaFact]
	public void Seed_PopulatesFontFamilyWeightAndItalic_FromRecord()
	{
		var defaults = GridStyleOptions.Default;
		var source = defaults with
		{
			Fonts = defaults.Fonts with
			{
				FontFamily = "Cascadia Code",
				HeaderFontWeight = 600,
				HeaderItalic = true,
				CellFontWeight = 500
			},
			StatusBar = defaults.StatusBar with
			{
				TimerLabelFontSize = 18,
				TimerLabelItalic = true
			}
		};
		var viewModel = CreateViewModel(source);

		viewModel.Fonts.FontFamily.Should().Be("Cascadia Code");
		viewModel.Fonts.HeaderFontWeight.Should().Be(600);
		viewModel.Fonts.HeaderItalic.Should().BeTrue();
		viewModel.Fonts.CellFontWeight.Should().Be(500);
		viewModel.StatusBar.TimerLabelFontSize.Should().Be(18);
		viewModel.StatusBar.TimerLabelItalic.Should().BeTrue();
	}

	[AvaloniaFact]
	public void BuildRecord_CarriesEditedFontFamilyWeightAndItalic()
	{
		var source = GridStyleOptions.Default;
		var viewModel = CreateViewModel(source);

		viewModel.Fonts.FontFamily = "Consolas";
		viewModel.Fonts.HeaderFontWeight = 900;
		viewModel.Fonts.CellItalic = true;
		viewModel.StatusBar.TimerValueWeight = 300;

		var record = viewModel.BuildRecord();

		record.Fonts.FontFamily.Should().Be("Consolas");
		record.Fonts.HeaderFontWeight.Should().Be(900);
		record.Fonts.CellItalic.Should().BeTrue();
		record.StatusBar.TimerValueWeight.Should().Be(300);
	}

	[AvaloniaFact]
	public void AvailableFontWeights_IncludeSeededWeights_NotInCuratedList()
	{
		var defaults = GridStyleOptions.Default;
		var source = defaults with { Fonts = defaults.Fonts with { HeaderFontWeight = 333 } };
		var viewModel = CreateViewModel(source);

		viewModel.AvailableFontWeights.Should().Contain(option => option.Value == 333);
	}

	[AvaloniaFact]
	public void AvailableFontFamilies_StartWithDefaultSentinel_AndIncludeSeededFamily()
	{
		var defaults = GridStyleOptions.Default;
		var source = defaults with { Fonts = defaults.Fonts with { FontFamily = "No Such Installed Font 12345" } };
		var viewModel = CreateViewModel(source);

		viewModel.AvailableFontFamilies[0].Value.Should().Be("");
		viewModel.AvailableFontFamilies[0].Name.Should().Be(Resources.EditorDefaultFont);
		viewModel.AvailableFontFamilies.Should().Contain(option => option.Value == "No Such Installed Font 12345");
	}

	[AvaloniaFact]
	public void CanSave_IsTrue_ForSeededValidDraft()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.CanSave.Should().BeTrue();
	}

	[AvaloniaTheory]
	[InlineData(0)]
	[InlineData(5)]
	[InlineData(73)]
	public void CanSave_IsFalse_WhenFontSizeOutOfRange(int fontSize)
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.Fonts.CellFontSize = fontSize;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaFact]
	public void CanSave_IsFalse_WhenNumericIsNull()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.Layout.RowHeight = null;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaFact]
	public void CanSave_IsFalse_WhenRowHeightOutOfRange()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.Layout.RowHeight = GridStyleEditorViewModel.MaxRowHeight + 1;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaFact]
	public void ToHex_PreservesSixDigitForm_ForOpaqueColors()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.Selection.Background = Color.Parse("#A1B2C3");

		viewModel.BuildRecord().Selection.Background.ToString().Should().Be("#A1B2C3");
	}

	[AvaloniaFact]
	public void ToHex_EmitsEightDigitForm_ForNonOpaqueColors()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.Selection.Background = Color.FromArgb(0x80, 0x11, 0x22, 0x33);

		viewModel.BuildRecord().Selection.Background.ToString().Should().Be("#80112233");
	}

	[AvaloniaFact]
	public async Task LoadAsync_MissingConfigDir_SetsErrorMessageAndDoesNotReseed()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);
		viewModel.Fonts.CellFontSize = 18;

		await viewModel.LoadAsync();

		viewModel.ErrorMessage.Should().NotBeNullOrWhiteSpace();
		viewModel.Fonts.CellFontSize.Should().Be(18);
	}

	[AvaloniaFact]
	public async Task LoadAsync_Success_ReplacesDraftsAndRewiresCanSave()
	{
		var loaded = GridStyleOptionsTestData.Distinct();
		var viewModel = new GridStyleEditorViewModel(
			new CausedByFailingFacade(loadResult: Result.Ok(loaded)),
			ConfigDir,
			GridStyleOptions.Default,
			NullLogger<GridStyleEditorViewModel>.Instance);

		await viewModel.LoadAsync();

		viewModel.Fonts.HeaderFontSize.Should().Be(loaded.Fonts.HeaderFontSize);

		// The out-of-range edit lands on the NEW draft; CanSave flipping proves the re-seed rewired the
		// subscriptions to it, not to the discarded default draft.
		viewModel.Fonts.HeaderFontSize = 999;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaFact]
	public async Task SaveCommand_WhenSaveThrows_SurfacesErrorMessageAndLogs_WithoutCrashing()
	{
		var logger = new RecordingLogger<GridStyleEditorViewModel>();
		var failure = new InvalidOperationException("disk gone");
		var viewModel = new GridStyleEditorViewModel(
			new ThrowingFacade(failure),
			ConfigDir,
			GridStyleOptions.Default,
			logger);

		viewModel.CanSave.Should().BeTrue("the seeded default draft is valid, so SaveCommand can execute");

		await ExecuteSwallowing(viewModel.SaveCommand);

		viewModel.ErrorMessage.Should().Be(Resources.SaveFailed);
		var logged = logger.Entries.Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Error);
		logged.Exception.Should().BeSameAs(failure);
	}

	[AvaloniaFact]
	public void ReportSaveException_SurfacesOnEditorErrorMessage_AndLogsWithException()
	{
		var logger = new RecordingLogger<GridStyleEditorViewModel>();
		var viewModel = new GridStyleEditorViewModel(
			new GridStyleEditorFacade(),
			ConfigDir,
			GridStyleOptions.Default,
			logger);
		var failure = new InvalidOperationException("boom");

		viewModel.ReportSaveException(failure);

		viewModel.ErrorMessage.Should().Be(Resources.SaveFailed);
		var logged = logger.Entries.Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Error);
		logged.Exception.Should().BeSameAs(failure);
	}

	[AvaloniaFact]
	public async Task LoadAsync_WhenLoaderFailsWithCausedByException_LogsItAtWarning()
	{
		var logger = new RecordingLogger<GridStyleEditorViewModel>();
		var failure = new InvalidOperationException("yaml parse failed");
		var viewModel = new GridStyleEditorViewModel(
			new CausedByFailingFacade(loadResult: Result.Fail(
				new GridStyleLoadFailedError("grid_style.yaml").CausedBy(failure))),
			ConfigDir,
			GridStyleOptions.Default,
			logger);

		await viewModel.LoadAsync();

		var logged = logger.Entries.Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Warning);
		logged.Exception.Should().BeSameAs(failure);
	}

	[AvaloniaFact]
	public async Task SaveCommand_WhenWriterFailsWithCausedByException_LogsItAtWarning()
	{
		var logger = new RecordingLogger<GridStyleEditorViewModel>();
		var failure = new IOException("disk full");
		var viewModel = new GridStyleEditorViewModel(
			new CausedByFailingFacade(saveResult: Result.Fail(
				new GridStyleSaveFailedError("grid_style.yaml").CausedBy(failure))),
			ConfigDir,
			GridStyleOptions.Default,
			logger);

		viewModel.CanSave.Should().BeTrue("the seeded default draft is valid, so SaveCommand can execute");

		await viewModel.SaveCommand.Execute();

		var logged = logger.Entries.Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Warning);
		logged.Exception.Should().BeSameAs(failure);
	}

	[AvaloniaFact]
	public async Task LoadAsync_MalformedHexColor_UnderRussianCulture_RendersRussianErrorMessage()
	{
		using var tempDir = CopyShippedConfigWithInvalidColor();
		var viewModel = new GridStyleEditorViewModel(
			new GridStyleEditorFacade(),
			tempDir.Path,
			GridStyleOptions.Default,
			NullLogger<GridStyleEditorViewModel>.Instance);

		using (ResourcesCultureScope.Use("ru"))
		{
			await viewModel.LoadAsync();
		}

		viewModel.ErrorMessage.Should().Be(
			"Недопустимый цвет в «colors.cells.changed_selected»: «zzz». "
			+ "Ожидается формат «#RRGGBB» или «#AARRGGBB».");
	}

	private static TempDirectory CopyShippedConfigWithInvalidColor()
	{
		var source = ShippedConfigLocator.GetConfigDirectory("MBE");
		var tempDir = new TempDirectory();
		var uiDir = Path.Combine(tempDir.Path, "ui");
		Directory.CreateDirectory(uiDir);

		var original = File.ReadAllText(Path.Combine(source, "ui", "grid_style.yaml"));
		var corrupted = Regex.Replace(original, "changed_selected:\\s*\"#[0-9A-Fa-f]+\"", "changed_selected: \"zzz\"");
		File.WriteAllText(Path.Combine(uiDir, "grid_style.yaml"), corrupted);

		return tempDir;
	}

	private static GridStyleEditorViewModel CreateViewModel(GridStyleOptions source)
	{
		return new GridStyleEditorViewModel(
			new GridStyleEditorFacade(),
			ConfigDir,
			source,
			NullLogger<GridStyleEditorViewModel>.Instance);
	}

	// Every surfaced leaf as (group property name on the VM, the live draft instance, the leaf property on
	// that draft). The ten group properties are exactly the public VM properties whose type derives from
	// ReactiveObject; each draft's leaves are its own declared public get/set properties. DeclaredOnly keeps
	// the base ReactiveObject members out, yet keeps ChangedCellColorsDraft.Changed — the get/set color leaf
	// that hides the base observable with `new` — because it is declared on the draft type.
	private static IReadOnlyList<(string GroupName, object Draft, PropertyInfo Leaf)> SurfacedLeaves(
		GridStyleEditorViewModel viewModel)
	{
		var groups = typeof(GridStyleEditorViewModel)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(property => typeof(ReactiveObject).IsAssignableFrom(property.PropertyType))
			.ToList();

		var leaves = new List<(string, object, PropertyInfo)>();
		foreach (var group in groups)
		{
			var draft = group.GetValue(viewModel)
				?? throw new InvalidOperationException($"Draft property {group.Name} was null");
			foreach (var leaf in DraftLeaves(group.PropertyType))
			{
				leaves.Add((group.Name, draft, leaf));
			}
		}

		return leaves;
	}

	private static IReadOnlyList<PropertyInfo> DraftLeaves(Type draftType)
	{
		return draftType
			.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(property => property.GetMethod?.IsPublic == true && property.SetMethod?.IsPublic == true)
			.ToList();
	}

	// Color perturbation stays opaque so ToHex changes; weights are unvalidated ints so any value round-trips.
	private static object? Perturb(PropertyInfo property, object? current)
	{
		if (property.PropertyType == typeof(Color))
		{
			var color = (Color)current!;
			return Color.FromArgb(255, (byte)(color.R ^ 1), color.G, color.B);
		}

		if (property.PropertyType == typeof(decimal?))
		{
			return (decimal?)current + 1;
		}

		if (property.PropertyType == typeof(int))
		{
			return (int)current! + 100;
		}

		if (property.PropertyType == typeof(bool))
		{
			return !(bool)current!;
		}

		if (property.PropertyType == typeof(string))
		{
			return (string)current! + " Perturbed";
		}

		throw new InvalidOperationException($"No perturbation defined for {property.Name} ({property.PropertyType})");
	}

	// Typed recursive leaf walk: a string/primitive/enum property is a LEAF compared at its dotted path;
	// a record group RECURSES. Keeps root-level Orientation (an enum) a first-class leaf, so a BuildRecord
	// bug that also flips Orientation cannot slip the exact-one-leaf perturbation assertion.
	private static IReadOnlyList<string> ChangedRecordFields(GridStyleOptions baseline, GridStyleOptions candidate)
	{
		var changed = new List<string>();
		CollectChangedLeaves(baseline, candidate, prefix: null, changed);
		return changed;
	}

	private static void CollectChangedLeaves(object baseline, object candidate, string? prefix, List<string> changed)
	{
		// Instance-only: the static GridStyleOptions.Default property would otherwise recurse into itself.
		foreach (var property in baseline.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			var baselineValue = property.GetValue(baseline);
			var candidateValue = property.GetValue(candidate);
			var path = prefix is null ? property.Name : $"{prefix}.{property.Name}";

			if (IsLeaf(property.PropertyType))
			{
				if (!Equals(baselineValue, candidateValue))
				{
					changed.Add(path);
				}
			}
			else
			{
				CollectChangedLeaves(baselineValue!, candidateValue!, path, changed);
			}
		}
	}

	private static bool IsLeaf(Type type)
	{
		return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(StyleColor);
	}

	private static async Task ExecuteSwallowing(ReactiveCommand<Unit, bool> command)
	{
		try
		{
			await command.Execute();
		}
		catch (InvalidOperationException)
		{
			// The command routes the throw to ThrownExceptions; Execute also rethrows to the awaiter.
		}
	}

	private sealed class ThrowingFacade(Exception failure) : IGridStyleEditorFacade
	{
		public Task<Result<GridStyleOptions>> Load(string configDir)
		{
			return Task.FromResult(Result.Ok(GridStyleOptions.Default));
		}

		public Task<Result> Save(string configDir, GridStyleOptions options)
		{
			throw failure;
		}
	}

	private sealed class CausedByFailingFacade(
		Result<GridStyleOptions>? loadResult = null,
		Result? saveResult = null) : IGridStyleEditorFacade
	{
		public Task<Result<GridStyleOptions>> Load(string configDir)
		{
			return Task.FromResult(loadResult ?? Result.Ok(GridStyleOptions.Default));
		}

		public Task<Result> Save(string configDir, GridStyleOptions options)
		{
			return Task.FromResult(saveResult ?? Result.Ok());
		}
	}
}
