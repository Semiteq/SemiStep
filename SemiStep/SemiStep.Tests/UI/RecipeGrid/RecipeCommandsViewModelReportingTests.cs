using System.Reactive;
using System.Reactive.Linq;

using Avalonia.Headless.XUnit;

using AwesomeAssertions;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Recipes;

using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class RecipeCommandsViewModelReportingTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private readonly RecordingLogger<RecipeCommandsViewModel> _logger = new();
	private readonly StubRecipeGridSurface _surface = new();
	private RecipeCommandsViewModel _commands = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_commands = new RecipeCommandsViewModel(
			_fixture.Coordinator,
			_surface,
			_fixture.MessagePanel,
			_logger);
	}

	public async ValueTask DisposeAsync()
	{
		_commands.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public async Task AddStep_WhenCommandBodyThrows_ReportsErrorAndLogsException()
	{
		var failure = new InvalidOperationException("boom");
		_surface.SelectedStepIndex = -1;
		_surface.RequestSelectionException = failure;

		var execute = async () => await _commands.AddStepCommand.Execute();

		(await execute.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
		_fixture.MessagePanel.Entries.Should().ContainSingle(e => e.IsError);

		var logged = _logger.Snapshot().Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Error);
		logged.Exception.Should().BeSameAs(failure);
	}

	[AvaloniaFact]
	public async Task DeleteStep_WhenCoordinatorRejectsIndex_ReportsFailedResultToPanel()
	{
		_fixture.SeedRecipe(2);

		_surface.SelectedStepIndices = [99];

		await _commands.DeleteStepCommand.Execute();

		_fixture.MessagePanel.Entries.Should().ContainSingle(e => e.IsError);
	}

	[AvaloniaFact]
	public async Task Undo_WhenNoHistory_ReportsFailedResultToPanel()
	{
		await _commands.UndoCommand.Execute();

		_fixture.MessagePanel.Entries.Should().ContainSingle(e => e.IsError);
	}

	[AvaloniaFact]
	public async Task Redo_WhenNoHistory_ReportsFailedResultToPanel()
	{
		await _commands.RedoCommand.Execute();

		_fixture.MessagePanel.Entries.Should().ContainSingle(e => e.IsError);
	}

	[AvaloniaFact]
	public async Task Undo_WhenHistoryExists_ReportsNoError()
	{
		_fixture.SeedRecipe(2);
		_fixture.Coordinator.CanUndo.Should().BeTrue();

		await _commands.UndoCommand.Execute();

		_fixture.MessagePanel.Entries.Should().NotContain(e => e.IsError);
	}

	private sealed class StubRecipeGridSurface : IRecipeGridSurface
	{
		public int SelectedStepIndex { get; set; } = -1;

		public IReadOnlyList<int> SelectedStepIndices { get; set; } = [];

		public Exception? RequestSelectionException { get; set; }

		public int StepCount => 0;

		public bool IsReadOnly => false;

		public IObservable<int?> SelectionRequests => Observable.Never<int?>();

		public IObservable<bool> CanDeleteStep => Observable.Return(true);

		public IObservable<Unit> EditorMustClose => Observable.Never<Unit>();

		public void Initialize()
		{
		}

		public void UpdateSelection(IReadOnlyList<int> stepIndices)
		{
		}

		public void RequestSelection(int? stepIndex)
		{
			if (RequestSelectionException is not null)
			{
				throw RequestSelectionException;
			}
		}

		public IReadOnlyList<Step> CollectSelectedSteps()
		{
			return [];
		}

		public void Dispose()
		{
		}
	}
}
