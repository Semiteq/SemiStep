using System.Reactive.Linq;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using FluentAssertions;

using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.MainWindow;
using SemiStep.UI.MessageService;

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
		_viewModel = _fixture.CreateMainWindowViewModel();
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
		var viewModel = _fixture.CreateMainWindowViewModel(
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
}
