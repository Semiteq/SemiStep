using System;
using System.Reactive.Linq;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using FluentResults;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.Localization;
using SemiStep.UI.MainWindow;
using SemiStep.UI.MessageService;
using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "MessageReporting")]
[Trait("Category", "Unit")]
public sealed class MainWindowViewModelReportingTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private readonly RecordingLogger<MainWindowViewModel> _logger = new();
	private MainWindowViewModel _viewModel = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_viewModel = _fixture.CreateMainWindowViewModel(logger: _logger);
	}

	public ValueTask DisposeAsync()
	{
		_viewModel.Dispose();
		return _fixture.DisposeAsync();
	}

	[AvaloniaTheory]
	[InlineData(nameof(Resources.PlcStateUpdateFailed))]
	[InlineData(nameof(Resources.PlcConflictHandlingFailed))]
	[InlineData(nameof(Resources.SyncTimeRefreshFailed))]
	public void OnSubscriptionError_ReportsToPanelAndLogsException(string contextKey)
	{
		var failure = new InvalidOperationException("boom");
		var context = new LocalizedText(contextKey);

		_viewModel.OnSubscriptionError(context)(failure);

		_fixture.MessagePanel.Entries.Should().ContainSingle(
			entry => entry.Severity == MessageSeverity.Error && entry.Message == $"{context.Localized}: boom");

		var logged = _logger.Entries.Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Error);
		logged.Exception.Should().BeSameAs(failure);
	}

	[AvaloniaFact]
	public void Guarded_ThrowInOnNextBody_ReportsToPanelAndLogs_WithoutEscaping()
	{
		var failure = new InvalidOperationException("boom");
		var context = new LocalizedText(nameof(Resources.SyncTimeRefreshFailed));

		var act = () => _viewModel.Guarded(context, () => throw failure);

		act.Should().NotThrow("a throw in the onNext body must be contained, not propagated to the pipeline");

		_fixture.MessagePanel.Entries.Should().ContainSingle(
			entry => entry.Severity == MessageSeverity.Error && entry.Message == $"{context.Localized}: boom");

		var logged = _logger.Entries.Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Error);
		logged.Exception.Should().BeSameAs(failure);
	}

	[AvaloniaFact]
	public async Task ToggleOrientation_WhenFlipThrows_ReportsErrorToPanelAndLogs()
	{
		var failure = new InvalidOperationException("boom");
		var context = new LocalizedText(nameof(Resources.OrientationToggleFailed));
		var grid = (ActiveRecipeGridSurface)_viewModel.RecipeGrid;

		// The flip raises Orientation; a throwing observer on that change makes the command body
		// fault, driving the exception into ToggleOrientationCommand.ThrownExceptions.
		using var poison = grid.WhenAnyValue(x => x.Orientation)
			.Skip(1)
			.Subscribe(_ => throw failure);

		try
		{
			await _viewModel.ToggleOrientationCommand.Execute();
		}
		catch (InvalidOperationException)
		{
			// Routed to ThrownExceptions; awaiting also surfaces it here.
		}

		_fixture.MessagePanel.Entries.Should().ContainSingle(
			entry => entry.Severity == MessageSeverity.Error && entry.Message == $"{context.Localized}: boom");

		var logged = _logger.Entries.Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Error);
		logged.Exception.Should().BeSameAs(failure);
	}

	[AvaloniaFact]
	public void Guarded_ConflictResolutionBody_WhenResultFails_ReportsFailedResultToPanel()
	{
		// Mirrors the HandleConflictAsync call site: a failed resolution Result must land a panel
		// entry rather than being dropped. Guarded's own throw-containment is proven above.
		var result = Result.Fail("resolution rejected");

		_viewModel.Guarded(new LocalizedText(nameof(Resources.PlcConflictResolutionFailed)), () =>
		{
			if (result.IsFailed)
			{
				_viewModel.MessagePanel.ReportFailure(result);
			}
		});

		_fixture.MessagePanel.Entries.Should()
			.ContainSingle(entry => entry.Severity == MessageSeverity.Error)
			.Which.Message.Should().Be("resolution rejected");
	}
}
