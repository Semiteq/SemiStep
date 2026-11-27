using System.Reactive;
using System.Reactive.Linq;

using Avalonia.Headless.XUnit;
using Avalonia.Media;

using AwesomeAssertions;

using FluentResults;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.Tests.Helpers;
using SemiStep.UI.Localization;
using SemiStep.UI.StyleEditor;

using Xunit;

namespace SemiStep.Tests.UI.StyleEditor;

[Trait("Component", "UI")]
[Trait("Category", "Unit")]
[Trait("Area", "GridStyleEditor")]
public sealed class GridStyleEditorViewModelTests
{
	private const string ConfigDir = @"C:\does-not-exist";

	[AvaloniaFact]
	public void Seed_ThenBuildRecord_PreservesEveryFieldDistinctly()
	{
		var viewModel = CreateViewModel(GridStyleOptionsTestData.Distinct());

		viewModel.BuildRecord().Should().Be(GridStyleOptionsTestData.Distinct());
	}

	[AvaloniaFact]
	public void BuildRecord_AfterEditingColorAndFontSize_ChangesOnlyThoseFields()
	{
		var source = GridStyleOptions.Default;
		var viewModel = CreateViewModel(source);

		viewModel.Fonts.CellFontSize.Value = 18;
		viewModel.Selection.Accent.Pick(Color.Parse("#123456"));

		var record = viewModel.BuildRecord();

		var expected = source with
		{
			Fonts = source.Fonts with { CellFontSize = 18 },
			Selection = source.Selection with { Accent = StyleColor.Parse("#123456") }
		};
		record.Should().Be(expected);
	}

	[AvaloniaFact]
	public void BuildRecord_RoundsFractionalStatusBarFontSize_ToNearestInt()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.StatusBar.FontSize.Value = 14.6m;

		viewModel.BuildRecord().StatusBar.FontSize.Should().Be(15);
	}

	[AvaloniaTheory]
	[InlineData(5)]
	[InlineData(73)]
	public void CanSave_IsFalse_WhenStatusBarFontSizeOutOfRange(int fontSize)
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.StatusBar.FontSize.Value = fontSize;

		viewModel.CanSave.Should().BeFalse();
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
	public void SeededValidDraft_CanSave_WithNoValidationMessage()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.CanSave.Should().BeTrue();
		viewModel.ValidationMessage.Should().BeNull();
		viewModel.Sections.All.Should().OnlyContain(section => !section.HasError);
	}

	[AvaloniaTheory]
	[InlineData(0)]
	[InlineData(5)]
	[InlineData(73)]
	public void CanSave_IsFalse_WhenFontSizeOutOfRange(int fontSize)
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.Fonts.CellFontSize.Value = fontSize;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaFact]
	public void EmptyNumber_MarksItsSection_AndNamesTheField()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.Layout.RowHeight.Value = null;

		viewModel.CanSave.Should().BeFalse();
		viewModel.ValidationMessage.Should().Contain(Resources.EditorPageGrid).And.Contain(Resources.EditorRowHeight);
		viewModel.IsMessageError.Should().BeTrue();
		viewModel.Message.Should().Be(viewModel.ValidationMessage);
		viewModel.Sections.Grid.HasError.Should().BeTrue();
		viewModel.Sections.All.Where(section => section != viewModel.Sections.Grid)
			.Should().OnlyContain(section => !section.HasError);
	}

	[AvaloniaFact]
	public void TwoInvalidFields_TheFirstInPageOrderIsReported_ThenTheNext()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.StatusBar.Padding.Value = null;
		viewModel.Fonts.HeaderFontSize.Value = null;

		viewModel.ValidationMessage.Should().Contain(Resources.EditorPageFonts)
			.And.Contain(Resources.EditorGridHeaders);
		viewModel.Sections.Fonts.HasError.Should().BeTrue();
		viewModel.Sections.StatusBar.HasError.Should().BeTrue();

		viewModel.Fonts.HeaderFontSize.Value = 12;

		viewModel.ValidationMessage.Should().Contain(Resources.EditorPageStatusBar)
			.And.Contain(Resources.EditorVerticalPadding);
		viewModel.Sections.Fonts.HasError.Should().BeFalse();

		viewModel.StatusBar.Padding.Value = 6;

		viewModel.ValidationMessage.Should().BeNull();
		viewModel.Message.Should().BeNull();
		viewModel.CanSave.Should().BeTrue();
	}

	[AvaloniaFact]
	public void Weight_ValidationMessage_ShowsThePercentRange()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.Selection.Weight.Value = null;

		viewModel.ValidationMessage.Should().Contain("100").And.Contain(Resources.EditorOpacity);
	}

	[AvaloniaFact]
	public void BuildRecord_OpaqueColor_KeepsChannels()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.Selection.Accent.Pick(Color.Parse("#A1B2C3"));

		viewModel.BuildRecord().Selection.Accent.Should().Be(StyleColor.Parse("#A1B2C3"));
	}

	[AvaloniaFact]
	public void BuildRecord_TranslucentSeedWithoutAPick_KeepsAlphaChannel()
	{
		var source = GridStyleOptions.Default;
		var viewModel = CreateViewModel(source with
		{
			Selection = source.Selection with { Accent = StyleColor.Parse("#80112233") }
		});

		viewModel.BuildRecord().Selection.Accent.Should().Be(StyleColor.Parse("#80112233"));
	}

	[AvaloniaFact]
	public async Task LoadAsync_MissingConfigDir_SetsErrorMessageAndDoesNotReseed()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);
		viewModel.Fonts.CellFontSize.Value = 18;

		await viewModel.LoadAsync();

		viewModel.ErrorMessage.Should().NotBeNullOrWhiteSpace();
		viewModel.Fonts.CellFontSize.Value.Should().Be(18);
	}

	[AvaloniaFact]
	public async Task LoadAsync_Success_ReplacesDraftsAndRewiresValidation()
	{
		var loaded = GridStyleOptionsTestData.Distinct();
		var viewModel = new GridStyleEditorViewModel(
			new RecordingFacade(loadResult: Result.Ok(loaded)),
			ConfigDir,
			GridStyleOptions.Default,
			NullLogger<GridStyleEditorViewModel>.Instance);

		await viewModel.LoadAsync();

		viewModel.Fonts.HeaderFontSize.Value.Should().Be(loaded.Fonts.HeaderFontSize);

		// The out-of-range edit lands on the NEW draft; CanSave flipping proves the re-seed rewired the
		// subscriptions to it, not to the discarded default draft.
		viewModel.Fonts.HeaderFontSize.Value = 999;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaFact]
	public async Task Save_WithAChange_WritesOnce_AndShowsTheRestartNotice()
	{
		var facade = new RecordingFacade();
		var viewModel = CreateViewModel(GridStyleOptions.Default, facade);

		viewModel.Fonts.CellFontSize.Value = 18;
		var saved = await viewModel.SaveCommand.Execute();

		saved.Should().BeTrue();
		facade.Saved.Should().ContainSingle().Which.Fonts.CellFontSize.Should().Be(18);
		viewModel.IsRestartPending.Should().BeTrue();
		viewModel.Message.Should().Be(Resources.EditorSavedRestartNotice);
		viewModel.IsMessageError.Should().BeFalse();
	}

	[AvaloniaFact]
	public async Task Save_WithNoChange_WritesNothing_AndShowsNoNotice()
	{
		var facade = new RecordingFacade();
		var viewModel = CreateViewModel(GridStyleOptions.Default, facade);

		var saved = await viewModel.SaveCommand.Execute();

		saved.Should().BeFalse();
		facade.Saved.Should().BeEmpty();
		viewModel.IsRestartPending.Should().BeFalse();
		viewModel.Message.Should().BeNull();
	}

	[AvaloniaFact]
	public async Task Save_SecondTimeWithoutAnotherChange_WritesNothingMore_AndKeepsTheNotice()
	{
		var facade = new RecordingFacade();
		var viewModel = CreateViewModel(GridStyleOptions.Default, facade);
		viewModel.Fonts.CellFontSize.Value = 18;
		await viewModel.SaveCommand.Execute();

		var savedAgain = await viewModel.SaveCommand.Execute();

		savedAgain.Should().BeFalse();
		facade.Saved.Should().ContainSingle();
		viewModel.Message.Should().Be(Resources.EditorSavedRestartNotice);
	}

	[AvaloniaTheory]
	[InlineData("number")]
	[InlineData("colour")]
	[InlineData("italic")]
	[InlineData("weight")]
	[InlineData("defaults")]
	public async Task AnyEditAfterSave_ClearsTheRestartNotice(string edit)
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default, new RecordingFacade());
		viewModel.Fonts.CellFontSize.Value = 18;
		await viewModel.SaveCommand.Execute();

		switch (edit)
		{
			case "number":
				viewModel.Layout.RowHeight.Value = 30;
				break;
			case "colour":
				viewModel.Chrome.GridLine.Pick(Color.FromRgb(0x12, 0x34, 0x56));
				break;
			case "italic":
				viewModel.StatusBar.Italic = !viewModel.StatusBar.Italic;
				break;
			case "weight":
				viewModel.Fonts.HeaderFontWeight = 300;
				break;
			default:
				viewModel.RestoreDefaultsCommand.Execute().Subscribe();
				break;
		}

		viewModel.IsRestartPending.Should().BeFalse();
		viewModel.Message.Should().BeNull();
	}

	[AvaloniaFact]
	public async Task Save_AfterAFailedLoad_WritesTheFile()
	{
		var facade = new RecordingFacade(loadResult: Result.Fail(new GridStyleLoadFailedError("grid_style.yaml")));
		var viewModel = CreateViewModel(GridStyleOptions.Default, facade);
		await viewModel.LoadAsync();

		var saved = await viewModel.SaveCommand.Execute();

		saved.Should().BeTrue("a failed load leaves no saved baseline to compare with");
		facade.Saved.Should().ContainSingle().Which.Should().Be(GridStyleOptions.Default);
		viewModel.IsRestartPending.Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task ValidationError_ReplacesTheRestartNotice()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default, new RecordingFacade());
		viewModel.Fonts.CellFontSize.Value = 18;
		await viewModel.SaveCommand.Execute();

		viewModel.Layout.RowHeight.Value = null;

		viewModel.IsMessageError.Should().BeTrue();
		viewModel.Message.Should().Be(viewModel.ValidationMessage);
	}

	[AvaloniaFact]
	public void RestoreDefaults_LoadsDefaults_KeepsHiddenKeysAndOrientation()
	{
		var seed = GridStyleOptionsTestData.Distinct();
		var viewModel = CreateViewModel(seed);

		viewModel.RestoreDefaultsCommand.Execute().Subscribe();

		var defaults = GridStyleOptions.Default;
		var expected = defaults with
		{
			Chrome = defaults.Chrome with { Info = seed.Chrome.Info, PanelBackground = seed.Chrome.PanelBackground },
			Orientation = seed.Orientation
		};
		viewModel.BuildRecord().Should().Be(expected);
	}

	[AvaloniaFact]
	public async Task RestoreDefaults_WritesNothingUntilSave()
	{
		var facade = new RecordingFacade();
		var viewModel = CreateViewModel(GridStyleOptionsTestData.Distinct(), facade);

		viewModel.RestoreDefaultsCommand.Execute().Subscribe();

		facade.Saved.Should().BeEmpty();
		(await viewModel.SaveCommand.Execute()).Should().BeTrue();
		facade.Saved.Should().ContainSingle();
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
		viewModel.Fonts.CellFontSize.Value = 18;

		viewModel.CanSave.Should().BeTrue("the edited draft is valid, so SaveCommand can execute");

		await ExecuteSwallowing(viewModel.SaveCommand);

		viewModel.ErrorMessage.Should().Be(Resources.SaveFailed);
		viewModel.Message.Should().Be(Resources.SaveFailed);
		viewModel.IsMessageError.Should().BeTrue();
		var logged = logger.Snapshot().Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Error);
		logged.Exception.Should().BeSameAs(failure);
	}

	[AvaloniaFact]
	public async Task LoadAsync_WhenLoaderFailsWithCausedByException_LogsItAtWarning()
	{
		var logger = new RecordingLogger<GridStyleEditorViewModel>();
		var failure = new InvalidOperationException("yaml parse failed");
		var viewModel = new GridStyleEditorViewModel(
			new RecordingFacade(loadResult: Result.Fail(
				new GridStyleLoadFailedError("grid_style.yaml").CausedBy(failure))),
			ConfigDir,
			GridStyleOptions.Default,
			logger);

		await viewModel.LoadAsync();

		var logged = logger.Snapshot().Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Warning);
		logged.Exception.Should().BeSameAs(failure);
	}

	[AvaloniaFact]
	public async Task SaveCommand_WhenWriterFailsWithCausedByException_LogsItAtWarning()
	{
		var logger = new RecordingLogger<GridStyleEditorViewModel>();
		var failure = new IOException("disk full");
		var viewModel = new GridStyleEditorViewModel(
			new RecordingFacade(saveResult: Result.Fail(
				new GridStyleSaveFailedError("grid_style.yaml").CausedBy(failure))),
			ConfigDir,
			GridStyleOptions.Default,
			logger);
		viewModel.Fonts.CellFontSize.Value = 18;

		await viewModel.SaveCommand.Execute();

		var logged = logger.Snapshot().Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Warning);
		logged.Exception.Should().BeSameAs(failure);
		viewModel.IsRestartPending.Should().BeFalse();
		viewModel.IsMessageError.Should().BeTrue();
	}

	private static GridStyleEditorViewModel CreateViewModel(
		GridStyleOptions source,
		IGridStyleEditorFacade? facade = null)
	{
		return new GridStyleEditorViewModel(
			facade ?? new GridStyleEditorFacade(),
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

		public Task<Result> Save(string configDir, GridStyleOptions options)
		{
			throw failure;
		}
	}

	private sealed class RecordingFacade(
		Result<GridStyleOptions>? loadResult = null,
		Result? saveResult = null) : IGridStyleEditorFacade
	{
		public List<GridStyleOptions> Saved { get; } = [];

		public Task<Result<GridStyleOptions>> Load(string configDir)
		{
			return Task.FromResult(loadResult ?? Result.Ok(GridStyleOptions.Default));
		}

		public Task<Result> Save(string configDir, GridStyleOptions options)
		{
			Saved.Add(options);
			return Task.FromResult(saveResult ?? Result.Ok());
		}
	}
}
