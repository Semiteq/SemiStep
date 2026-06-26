using System.Reactive.Concurrency;
using System.Reactive.Linq;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.Clipboard;
using SemiStep.UI.MainWindow;
using SemiStep.UI.MessageService;
using SemiStep.UI.Plc;
using SemiStep.UI.RecipeFile;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.StyleEditor;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "MainWindow")]
[Trait("Category", "Unit")]
public sealed class MainWindowViewModelReportingTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private MainWindowViewModel _viewModel = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_viewModel = CreateViewModel();
	}

	public ValueTask DisposeAsync()
	{
		_viewModel.Dispose();
		return _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public async Task ToggleSync_WhenEnableSyncFails_ReportsErrorToPanel()
	{
		_fixture.StubS7.ConnectShouldFail = true;

		await _viewModel.ToggleSyncCommand.Execute();

		_fixture.MessagePanel.Entries.Should().ContainSingle(e => e.Severity == MessageSeverity.Error);
		_fixture.MessagePanel.ErrorCount.Should().Be(0);
	}

	[AvaloniaFact]
	public async Task OpenStyleEditor_WhenFactoryThrows_ReportsErrorAndDoesNotEscape()
	{
		var viewModel = CreateViewModel(
			() => throw new InvalidOperationException("factory boom"));

		try
		{
			viewModel.MainWindow = new Window();

			try
			{
				await viewModel.OpenStyleEditorCommand.Execute();
			}
			catch (InvalidOperationException)
			{
				// Exception is routed to ThrownExceptions; awaiting also surfaces it here.
			}

			Dispatcher.UIThread.RunJobs();

			var operationEntry = _fixture.MessagePanel.Entries
				.Should().ContainSingle(e => e.Severity == MessageSeverity.Error).Subject;
			operationEntry.Message.Should().StartWith("Style editor failed:");
			operationEntry.Message.Should().Contain("factory boom");
		}
		finally
		{
			viewModel.Dispose();
		}
	}

	private MainWindowViewModel CreateViewModel(
		Func<GridStyleEditorViewModel>? styleEditorFactory = null)
	{
		var grid = new RecipeGridViewModel(
			_fixture.Coordinator,
			_fixture.RecipeMetadataRegistry,
			_fixture.MessagePanel,
			NullLogger<RecipeGridViewModel>.Instance);

		var commands = new RecipeCommandsViewModel(_fixture.Coordinator, grid);

		var clipboardSerializer = new ClipboardSerializer(_fixture.RecipeMetadataRegistry);
		var importedRecipeValidator = new ImportedRecipeValidator(_fixture.RecipeMetadataRegistry);
		var clipboard = new ClipboardViewModel(
			_fixture.Coordinator,
			grid,
			clipboardSerializer,
			importedRecipeValidator,
			_fixture.MessagePanel);

		var recipeFile = new RecipeFileViewModel(_fixture.Coordinator, _fixture.MessagePanel);

		var columnBuilder = new ColumnBuilder(_fixture.AppConfiguration.GridStyle, _fixture.RecipeMetadataRegistry);

		var plcMonitor = new PlcMonitorViewModel(
			_fixture.Coordinator,
			_fixture.RecipeMetadataRegistry,
			new HistoricalScheduler());

		var gridStyleEditorViewModelFactory = styleEditorFactory ?? new Func<GridStyleEditorViewModel>(
			() => new GridStyleEditorViewModel(
				new GridStyleEditorFacade(),
				@"C:\does-not-exist",
				_fixture.AppConfiguration.GridStyle));

		return new MainWindowViewModel(
			_fixture.Coordinator,
			grid,
			commands,
			clipboard,
			recipeFile,
			_fixture.MessagePanel,
			columnBuilder,
			plcMonitor,
			gridStyleEditorViewModelFactory,
			NullLogger<MainWindowViewModel>.Instance);
	}
}
