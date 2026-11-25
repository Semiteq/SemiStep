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
	public void BuildRecord_OpaqueColor_KeepsChannels()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.Selection.Background = Color.Parse("#A1B2C3");

		viewModel.BuildRecord().Selection.Background.Should().Be(StyleColor.Parse("#A1B2C3"));
	}

	[AvaloniaFact]
	public void BuildRecord_TranslucentColor_KeepsAlphaChannel()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.Selection.Background = Color.FromArgb(0x80, 0x11, 0x22, 0x33);

		viewModel.BuildRecord().Selection.Background.Should().Be(StyleColor.Parse("#80112233"));
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
			new CausedByFailingFacade(loadResult: Result.Fail(
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
			new CausedByFailingFacade(saveResult: Result.Fail(
				new GridStyleSaveFailedError("grid_style.yaml").CausedBy(failure))),
			ConfigDir,
			GridStyleOptions.Default,
			logger);

		viewModel.CanSave.Should().BeTrue("the seeded default draft is valid, so SaveCommand can execute");

		await viewModel.SaveCommand.Execute();

		var logged = logger.Snapshot().Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Warning);
		logged.Exception.Should().BeSameAs(failure);
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
