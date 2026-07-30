using System.Reactive;
using System.Reactive.Linq;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.Core.Recipes;

using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.Tests.UI.Localization;

using SemiStep.UI.Localization;
using SemiStep.UI.MessageService;
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
	public async Task AddStep_WhenCommandBodyThrows_ReportsSpecificContextAndLogs()
	{
		var failure = new InvalidOperationException("boom");
		_surface.SelectedStepIndex = -1;
		_surface.RequestSelectionException = failure;

		await ExecuteSwallowing(_commands.AddStepCommand);

		_fixture.MessagePanel.Entries.Should()
			.Contain(e => e.IsError && e.Message.StartsWith($"{Resources.AddStepFailed}:") && e.Message.Contains("boom"));

		var logged = _logger.Entries.Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Error);
		logged.Message.Should().Be("Add step failed");
		logged.Exception.Should().BeSameAs(failure);
	}

	[AvaloniaFact]
	public async Task DeleteStep_WhenCoordinatorRejectsIndex_ReportsFailedResultToPanel()
	{
		_fixture.SeedRecipe(2);

		var expected = _fixture.Coordinator.RemoveStep(99);
		expected.IsFailed.Should().BeTrue();
		var expectedMessage = expected.FormatErrors();

		_surface.SelectedStepIndices = new[] { 99 };

		using (ResourcesCultureScope.Use("en"))
		{
			await ExecuteSwallowing(_commands.DeleteStepCommand);

			_fixture.MessagePanel.Entries.Should()
				.ContainSingle(e => e.IsError).Which.Message.Should().Be(expectedMessage);
		}
	}

	[AvaloniaFact]
	public async Task Undo_WhenNoHistory_ReportsFailedResultToPanel()
	{
		var expected = _fixture.Coordinator.Undo();
		expected.IsFailed.Should().BeTrue();
		var expectedMessage = expected.FormatErrors();

		using (ResourcesCultureScope.Use("en"))
		{
			await ExecuteSwallowing(_commands.UndoCommand);

			_fixture.MessagePanel.Entries.Should()
				.ContainSingle(e => e.IsError).Which.Message.Should().Be(expectedMessage);
		}
	}

	[AvaloniaFact]
	public async Task Redo_WhenNoHistory_ReportsFailedResultToPanel()
	{
		var expected = _fixture.Coordinator.Redo();
		expected.IsFailed.Should().BeTrue();
		var expectedMessage = expected.FormatErrors();

		using (ResourcesCultureScope.Use("en"))
		{
			await ExecuteSwallowing(_commands.RedoCommand);

			_fixture.MessagePanel.Entries.Should()
				.ContainSingle(e => e.IsError).Which.Message.Should().Be(expectedMessage);
		}
	}

	[AvaloniaFact]
	public async Task Undo_WhenHistoryExists_ReportsNoError()
	{
		_fixture.SeedRecipe(2);
		_fixture.Coordinator.CanUndo.Should().BeTrue();

		await ExecuteSwallowing(_commands.UndoCommand);

		_fixture.MessagePanel.Entries.Should().NotContain(e => e.IsError);
	}

	private static async Task ExecuteSwallowing(ReactiveCommand<Unit, Unit> command)
	{
		try
		{
			await command.Execute();
		}
		catch (Exception)
		{
			// The command routes the throw to ThrownExceptions; Execute also rethrows to the awaiter.
		}
	}

	private sealed class StubRecipeGridSurface : IRecipeGridSurface
	{
		public int SelectedStepIndex { get; set; } = -1;

		public IReadOnlyList<int> SelectedStepIndices { get; set; } = Array.Empty<int>();

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
			return Array.Empty<Step>();
		}

		public void Dispose()
		{
		}
	}
}
