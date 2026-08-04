using System;
using System.Reactive;
using System.Reactive.Linq;
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

using Xunit;

namespace SemiStep.Tests.UI.StyleEditor;

[Trait("Component", "UI")]
[Trait("Category", "Unit")]
public sealed class GridStyleEditorViewModelTests
{
	private const string ConfigDir = @"C:\does-not-exist";

	[AvaloniaFact]
	public void Seed_PopulatesColorAndNumericProps_FromRecord()
	{
		var source = GridStyleOptions.Default with
		{
			StatusBarFontSize = 15,
			StatusBarTimerValueFontSize = 30
		};
		var viewModel = CreateViewModel(source);

		viewModel.CellFontSize.Should().Be(source.CellFontSize);
		viewModel.RowHeight.Should().Be((decimal)source.RowHeight);
		viewModel.ValidationPanelMaxHeight.Should().Be((decimal)source.ValidationPanelMaxHeight);
		viewModel.StatusBarFontSize.Should().Be(15);
		viewModel.StatusBarTimerValueFontSize.Should().Be(30);

		viewModel.SelectionBackground.Should().Be(Color.Parse(source.SelectionBackgroundColor));
		viewModel.HeaderForeground.Should().Be(Color.Parse(source.HeaderForegroundColor));
	}

	[AvaloniaFact]
	public void Seed_RoundTripsShippedHexValues_Losslessly()
	{
		var source = GridStyleOptions.Default;
		var viewModel = CreateViewModel(source);

		var record = viewModel.BuildRecord();

		record.Should().Be(source);
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
			CellFontSize = 18,
			SelectionBackgroundColor = "#123456"
		};
		record.Should().Be(expected);
	}

	[AvaloniaFact]
	public void BuildRecord_RoundsFractionalStatusBarFontSize_ToNearestInt()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.StatusBarFontSize = 14.6m;

		viewModel.BuildRecord().StatusBarFontSize.Should().Be(15);
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
		var source = GridStyleOptions.Default with
		{
			FontFamily = "Cascadia Code",
			HeaderFontWeight = 600,
			HeaderItalic = true,
			CellFontWeight = 500,
			StatusBarTimerLabelFontSize = 18,
			StatusBarTimerLabelItalic = true
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

		record.FontFamily.Should().Be("Consolas");
		record.HeaderFontWeight.Should().Be(900);
		record.CellItalic.Should().BeTrue();
		record.StatusBarTimerValueFontWeight.Should().Be(300);
	}

	[AvaloniaFact]
	public void AvailableFontWeights_IncludeSeededWeights_NotInCuratedList()
	{
		var source = GridStyleOptions.Default with { HeaderFontWeight = 333 };
		var viewModel = CreateViewModel(source);

		viewModel.AvailableFontWeights.Should().Contain(option => option.Value == 333);
	}

	[AvaloniaFact]
	public void AvailableFontFamilies_StartWithDefaultSentinel_AndIncludeSeededFamily()
	{
		var source = GridStyleOptions.Default with { FontFamily = "No Such Installed Font 12345" };
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

		viewModel.BuildRecord().SelectionBackgroundColor.Should().Be("#A1B2C3");
	}

	[AvaloniaFact]
	public void ToHex_EmitsEightDigitForm_ForNonOpaqueColors()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.SelectionBackground = Color.FromArgb(0x80, 0x11, 0x22, 0x33);

		viewModel.BuildRecord().SelectionBackgroundColor.Should().Be("#80112233");
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

		public Result Save(string configDir, GridStyleOptions options)
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

		public Result Save(string configDir, GridStyleOptions options)
		{
			return saveResult ?? Result.Ok();
		}
	}
}
