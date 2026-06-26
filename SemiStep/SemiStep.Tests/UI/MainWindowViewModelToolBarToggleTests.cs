using System.Reactive.Linq;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.MainWindow;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "MainWindow")]
[Trait("Category", "Unit")]
public sealed class MainWindowViewModelToolBarToggleTests : IAsyncLifetime
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
	public void IsToolBarVisible_DefaultsToTrue()
	{
		_viewModel.IsToolBarVisible.Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task ToggleToolBarCommand_FlipsVisibility()
	{
		await _viewModel.ToggleToolBarCommand.Execute();
		_viewModel.IsToolBarVisible.Should().BeFalse();

		await _viewModel.ToggleToolBarCommand.Execute();
		_viewModel.IsToolBarVisible.Should().BeTrue();
	}
}
