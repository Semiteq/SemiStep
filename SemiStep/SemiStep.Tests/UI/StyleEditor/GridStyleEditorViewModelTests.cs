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
	public void Seed_PopulatesEverySurfacedProperty_FromDistinctFixture()
	{
		var fixture = GridStyleOptionsTestData.Distinct();
		var viewModel = CreateViewModel(fixture);

		var properties = EditableProperties();
		properties.Should().HaveCount(
			SurfacedEditablePropertyCount,
			"the fixture must exercise every surfaced editable property");

		foreach (var property in properties)
		{
			var recordValue = LeafValue(fixture, RecordPath(property));
			var actual = property.GetValue(viewModel);
			var because = $"property {property.Name} must be seeded from the fixture";

			if (property.PropertyType == typeof(Color))
			{
				actual.Should().Be(((StyleColor)recordValue!).ToMediaColor(), because);
			}
			else if (property.PropertyType == typeof(decimal?))
			{
				// The record's int font sizes and double paddings box under reflection; a direct
				// (decimal) cast on a boxed int throws, so normalize both sides via Convert.ToDecimal.
				Convert.ToDecimal(actual).Should().Be(Convert.ToDecimal(recordValue), because);
			}
			else
			{
				actual.Should().Be(recordValue, because);
			}
		}
	}

	[AvaloniaFact]
	public void Seed_ThenBuildRecord_PreservesEveryFieldDistinctly()
	{
		var viewModel = CreateViewModel(GridStyleOptionsTestData.Distinct());

		viewModel.BuildRecord().Should().Be(GridStyleOptionsTestData.Distinct());
	}

	[AvaloniaFact]
	public void BuildRecord_PerturbingEachProperty_ChangesOnlyThatMappedField()
	{
		var viewModel = CreateViewModel(GridStyleOptionsTestData.Distinct());

		var properties = EditableProperties();
		properties.Should().HaveCount(
			SurfacedEditablePropertyCount,
			"the perturbation guard must cover every surfaced editable property");

		foreach (var property in properties)
		{
			var seededValue = property.GetValue(viewModel);
			var baseline = viewModel.BuildRecord();

			property.SetValue(viewModel, Perturb(property, seededValue));
			var built = viewModel.BuildRecord();

			var mappedField = RecordPath(property);
			var changedFields = ChangedRecordFields(baseline, built);
			changedFields.Should().BeEquivalentTo(
				new[] { mappedField },
				$"editing {property.Name} must change exactly the {mappedField} record field");

			// Restore the seeded value rather than reconstruct the VM: each ctor enumerates system fonts.
			property.SetValue(viewModel, seededValue);
		}
	}

	[AvaloniaFact]
	public void BuildRecord_AfterEditingColorAndFontSize_ChangesOnlyThoseFields()
	{
		var source = GridStyleOptions.Default;
		var viewModel = CreateViewModel(source);

		viewModel.CellFontSize = 18;
		viewModel.SelectionBackground = Color.Parse("#123456");

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

		viewModel.StatusBarFontSize = 14.6m;

		viewModel.BuildRecord().StatusBar.FontSize.Should().Be(15);
	}

	[AvaloniaTheory]
	[InlineData(5)]
	[InlineData(73)]
	public void CanSave_IsFalse_WhenStatusBarFontSizeOutOfRange(int fontSize)
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.StatusBarFontSize = fontSize;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaTheory]
	[InlineData(5)]
	[InlineData(73)]
	public void CanSave_IsFalse_WhenStatusBarTimerValueFontSizeOutOfRange(int fontSize)
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.StatusBarTimerValueFontSize = fontSize;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaTheory]
	[InlineData(5)]
	[InlineData(73)]
	public void CanSave_IsFalse_WhenStatusBarTimerLabelFontSizeOutOfRange(int fontSize)
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.StatusBarTimerLabelFontSize = fontSize;

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

		viewModel.FontFamily.Should().Be("Cascadia Code");
		viewModel.HeaderFontWeight.Should().Be(600);
		viewModel.HeaderItalic.Should().BeTrue();
		viewModel.CellFontWeight.Should().Be(500);
		viewModel.StatusBarTimerLabelFontSize.Should().Be(18);
		viewModel.StatusBarTimerLabelItalic.Should().BeTrue();
	}

	[AvaloniaFact]
	public void BuildRecord_CarriesEditedFontFamilyWeightAndItalic()
	{
		var source = GridStyleOptions.Default;
		var viewModel = CreateViewModel(source);

		viewModel.FontFamily = "Consolas";
		viewModel.HeaderFontWeight = 900;
		viewModel.CellItalic = true;
		viewModel.StatusBarTimerValueFontWeight = 300;

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

		viewModel.CellFontSize = fontSize;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaFact]
	public void CanSave_IsFalse_WhenNumericIsNull()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.RowHeight = null;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaFact]
	public void CanSave_IsFalse_WhenRowHeightOutOfRange()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.RowHeight = GridStyleEditorViewModel.MaxRowHeight + 1;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaFact]
	public void ToHex_PreservesSixDigitForm_ForOpaqueColors()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.SelectionBackground = Color.Parse("#A1B2C3");

		viewModel.BuildRecord().Selection.Background.ToString().Should().Be("#A1B2C3");
	}

	[AvaloniaFact]
	public void ToHex_EmitsEightDigitForm_ForNonOpaqueColors()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.SelectionBackground = Color.FromArgb(0x80, 0x11, 0x22, 0x33);

		viewModel.BuildRecord().Selection.Background.ToString().Should().Be("#80112233");
	}

	[AvaloniaFact]
	public async Task LoadAsync_MissingConfigDir_SetsErrorMessageAndDoesNotReseed()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);
		viewModel.CellFontSize = 18;

		await viewModel.LoadAsync();

		viewModel.ErrorMessage.Should().NotBeNullOrWhiteSpace();
		viewModel.CellFontSize.Should().Be(18);
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

	private static IReadOnlyList<PropertyInfo> EditableProperties()
	{
		return typeof(GridStyleEditorViewModel).GetProperties()
			.Where(property => property.GetMethod?.IsPublic == true && property.SetMethod?.IsPublic == true)
			.ToList();
	}

	// Hand-maintained VM-property -> nested record leaf path. NOT derived: several leaf names diverge
	// from the VM property name (StatusBarFontWeight -> StatusBar.Weight, ValidationPanelError ->
	// ValidationPanel.ErrorColor, every Color drops its trailing "Color"), so a mistyped entry surfaces
	// as a red guard test rather than a silent pass. Every entry is cross-checked against the group records.
	private static readonly IReadOnlyDictionary<string, string> _viewModelToRecordPath =
		new Dictionary<string, string>
		{
			["FontFamily"] = "Fonts.FontFamily",
			["HeaderFontSize"] = "Fonts.HeaderFontSize",
			["HeaderFontWeight"] = "Fonts.HeaderFontWeight",
			["HeaderItalic"] = "Fonts.HeaderItalic",
			["CellFontSize"] = "Fonts.CellFontSize",
			["CellFontWeight"] = "Fonts.CellFontWeight",
			["CellItalic"] = "Fonts.CellItalic",
			["CellPaddingLeft"] = "Layout.CellPaddingLeft",
			["CellPaddingTop"] = "Layout.CellPaddingTop",
			["CellPaddingRight"] = "Layout.CellPaddingRight",
			["CellPaddingBottom"] = "Layout.CellPaddingBottom",
			["RowHeight"] = "Layout.RowHeight",
			["SelectionBackground"] = "Selection.Background",
			["SelectionForeground"] = "Selection.Foreground",
			["CellChanged"] = "ChangedCells.Changed",
			["CellChangedSelected"] = "ChangedCells.ChangedSelected",
			["ReadOnlyCellDepth0"] = "ReadOnlyCells.Depth0",
			["ReadOnlyCellDepth1"] = "ReadOnlyCells.Depth1",
			["ReadOnlyCellDepth2"] = "ReadOnlyCells.Depth2",
			["ReadOnlyCellDepth3"] = "ReadOnlyCells.Depth3",
			["ReadOnlyCellDepth0Past"] = "ReadOnlyCells.Depth0Past",
			["ReadOnlyCellDepth1Past"] = "ReadOnlyCells.Depth1Past",
			["ReadOnlyCellDepth2Past"] = "ReadOnlyCells.Depth2Past",
			["ReadOnlyCellDepth3Past"] = "ReadOnlyCells.Depth3Past",
			["ReadOnlyCellSelected"] = "ReadOnlyCells.Selected",
			["ReadOnlyCellForeground"] = "ReadOnlyCells.Foreground",
			["DisabledCellDepth0"] = "DisabledCells.Depth0",
			["DisabledCellDepth1"] = "DisabledCells.Depth1",
			["DisabledCellDepth2"] = "DisabledCells.Depth2",
			["DisabledCellDepth3"] = "DisabledCells.Depth3",
			["DisabledCellDepth0Past"] = "DisabledCells.Depth0Past",
			["DisabledCellDepth1Past"] = "DisabledCells.Depth1Past",
			["DisabledCellDepth2Past"] = "DisabledCells.Depth2Past",
			["DisabledCellDepth3Past"] = "DisabledCells.Depth3Past",
			["DisabledCellSelected"] = "DisabledCells.Selected",
			["DisabledCellForeground"] = "DisabledCells.Foreground",
			["ExecutionDepth0"] = "Execution.Depth0",
			["ExecutionDepth1"] = "Execution.Depth1",
			["ExecutionDepth2"] = "Execution.Depth2",
			["ExecutionDepth3"] = "Execution.Depth3",
			["ExecutionDepth0Past"] = "Execution.Depth0Past",
			["ExecutionDepth1Past"] = "Execution.Depth1Past",
			["ExecutionDepth2Past"] = "Execution.Depth2Past",
			["ExecutionDepth3Past"] = "Execution.Depth3Past",
			["ExecutionCurrentStepMarker"] = "Execution.CurrentStepMarker",
			["StatusBarBackground"] = "StatusBar.Background",
			["StatusBarForeground"] = "StatusBar.Foreground",
			["StatusBarPadding"] = "StatusBar.Padding",
			["StatusBarItemSpacing"] = "StatusBar.ItemSpacing",
			["StatusBarFontSize"] = "StatusBar.FontSize",
			["StatusBarFontWeight"] = "StatusBar.Weight",
			["StatusBarItalic"] = "StatusBar.Italic",
			["StatusBarTimerLabelFontSize"] = "StatusBar.TimerLabelFontSize",
			["StatusBarTimerLabelFontWeight"] = "StatusBar.TimerLabelWeight",
			["StatusBarTimerLabelItalic"] = "StatusBar.TimerLabelItalic",
			["StatusBarTimerValueFontSize"] = "StatusBar.TimerValueFontSize",
			["StatusBarTimerValueFontWeight"] = "StatusBar.TimerValueWeight",
			["StatusBarTimerValueItalic"] = "StatusBar.TimerValueItalic",
			["ValidationPanelBackground"] = "ValidationPanel.Background",
			["ValidationPanelForeground"] = "ValidationPanel.Foreground",
			["ValidationPanelError"] = "ValidationPanel.ErrorColor",
			["ValidationPanelWarning"] = "ValidationPanel.WarningColor",
			["ValidationPanelMaxHeight"] = "ValidationPanel.MaxHeight",
			["GridLine"] = "Chrome.GridLine",
			["Info"] = "Chrome.Info",
			["Connected"] = "Chrome.Connected",
			["Disconnected"] = "Chrome.Disconnected",
			["LocalMode"] = "Chrome.LocalMode",
			["Connecting"] = "Chrome.Connecting",
			["PanelBackground"] = "Chrome.PanelBackground",
			["PanelHeaderBackground"] = "Chrome.PanelHeaderBackground",
			["SubtleBorder"] = "Chrome.SubtleBorder",
			["Separator"] = "Chrome.Separator",
			["SecondaryForeground"] = "Chrome.SecondaryForeground",
			["GridBorder"] = "Chrome.GridBorder",
			["GridBackground"] = "Chrome.GridBackground",
			["HeaderForeground"] = "Chrome.HeaderForeground",
		};

	private static string RecordPath(PropertyInfo viewModelProperty)
	{
		return _viewModelToRecordPath.TryGetValue(viewModelProperty.Name, out var path)
			? path
			: throw new InvalidOperationException($"No GridStyleOptions path maps to {viewModelProperty.Name}");
	}

	private static object? LeafValue(GridStyleOptions record, string dottedPath)
	{
		object current = record;
		foreach (var segment in dottedPath.Split('.'))
		{
			var property = current.GetType().GetProperty(segment)
				?? throw new InvalidOperationException($"No record property '{segment}' on {current.GetType().Name}");
			current = property.GetValue(current)
				?? throw new InvalidOperationException($"Record property '{segment}' was null");
		}

		return current;
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

		public Result Validate(GridStyleOptions options)
		{
			return Result.Ok();
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

		public Result Validate(GridStyleOptions options)
		{
			return Result.Ok();
		}

		public Task<Result> Save(string configDir, GridStyleOptions options)
		{
			return Task.FromResult(saveResult ?? Result.Ok());
		}
	}
}
