using System.Reactive.Linq;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;

using FluentAssertions;

using FluentResults;

using SemiStep.UI.MessageService;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "MessagePanel")]
[Trait("Category", "Unit")]
public sealed class MessagePanelCloseButtonTests : IAsyncLifetime
{
	private MessagePanelViewModel _viewModel = null!;
	private Window? _window;

	public ValueTask InitializeAsync()
	{
		_viewModel = new MessagePanelViewModel();
		return ValueTask.CompletedTask;
	}

	public ValueTask DisposeAsync()
	{
		_window?.Close();
		_viewModel.Dispose();
		return ValueTask.CompletedTask;
	}

	[AvaloniaFact]
	public void CloseButton_Exists_AndBindsToToggleCommand()
	{
		var panel = ShowMessagePanel();

		var closeButton = panel.FindControl<Button>("CloseButton");

		closeButton.Should().NotBeNull("the header must expose a close button");
		closeButton!.Command.Should().BeSameAs(_viewModel.ToggleCommand);
	}

	[AvaloniaFact]
	public async Task ToggleCommand_ClosesThenReopensThePanel()
	{
		_viewModel.RefreshReasons([new Error("e")]);
		_viewModel.IsVisible = true;

		await _viewModel.ToggleCommand.Execute();
		_viewModel.ShowPanel.Should().BeFalse("closing the panel hides it");

		await _viewModel.ToggleCommand.Execute();
		_viewModel.ShowPanel.Should().BeTrue("re-opening restores the panel");
	}

	private MessagePanel ShowMessagePanel()
	{
		var panel = new MessagePanel { DataContext = _viewModel };
		_window = new Window { Content = panel };
		_window.Show();

		return panel;
	}
}
