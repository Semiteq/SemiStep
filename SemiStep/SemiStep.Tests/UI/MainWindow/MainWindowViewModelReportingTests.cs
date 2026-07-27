using System;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.MainWindow;
using SemiStep.UI.MessageService;

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
	[InlineData("PLC state update")]
	[InlineData("PLC conflict handling")]
	[InlineData("Sync time refresh")]
	public void OnSubscriptionError_ReportsToPanelAndLogsException(string context)
	{
		var failure = new InvalidOperationException("boom");

		_viewModel.OnSubscriptionError(context)(failure);

		_fixture.MessagePanel.Entries.Should().ContainSingle(
			entry => entry.Severity == MessageSeverity.Error && entry.Message == $"{context}: boom");

		var logged = _logger.Entries.Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Error);
		logged.Exception.Should().BeSameAs(failure);
	}

	[AvaloniaFact]
	public void Guarded_ThrowInOnNextBody_ReportsToPanelAndLogs_WithoutEscaping()
	{
		var failure = new InvalidOperationException("boom");

		var act = () => _viewModel.Guarded("Sync time refresh", () => throw failure);

		act.Should().NotThrow("a throw in the onNext body must be contained, not propagated to the pipeline");

		_fixture.MessagePanel.Entries.Should().ContainSingle(
			entry => entry.Severity == MessageSeverity.Error && entry.Message == "Sync time refresh: boom");

		var logged = _logger.Entries.Should().ContainSingle().Subject;
		logged.Level.Should().Be(LogLevel.Error);
		logged.Exception.Should().BeSameAs(failure);
	}
}
