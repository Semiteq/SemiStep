using System.Reactive.Linq;

using Avalonia.Headless.XUnit;

using AwesomeAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.Localization;
using SemiStep.UI.MainWindow;

using Xunit;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "MainWindow")]
[Trait("Category", "Integration")]
public sealed class MainWindowTitleTests : IAsyncLifetime
{
	private const string TitlePrefix = "SemiStep - ";

	private readonly UIFixture _fixture = new();
	private readonly string _openedFilePath =
		Path.Combine(Path.GetTempPath(), $"title-opened-{Guid.NewGuid():N}.csv");
	private readonly string _savedAsFilePath =
		Path.Combine(Path.GetTempPath(), $"title-saved-as-{Guid.NewGuid():N}.csv");
	private MainWindowViewModel _viewModel = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(2);
		(await _fixture.CsvService.SaveAsync(_fixture.Coordinator.CurrentRecipe, _openedFilePath))
			.IsSuccess.Should().BeTrue();
		_fixture.Coordinator.NewRecipe();
		_viewModel = _fixture.CreateMainWindowViewModel();
		_viewModel.RecipeFile.OpenFileInteraction.RegisterHandler(context => context.SetOutput(_openedFilePath));
		_viewModel.RecipeFile.SaveFileInteraction.RegisterHandler(context => context.SetOutput(_savedAsFilePath));
	}

	public async ValueTask DisposeAsync()
	{
		_viewModel.Dispose();
		await _fixture.DisposeAsync();
		File.Delete(_openedFilePath);
		File.Delete(_savedAsFilePath);
	}

	[AvaloniaFact]
	public async Task Open_TitleHoldsTheFileName_WithoutTheDirtyMarker()
	{
		await OpenAsync();

		_viewModel.WindowTitle.Should().Be(TitlePrefix + Path.GetFileNameWithoutExtension(_openedFilePath));
	}

	[AvaloniaFact]
	public async Task SaveAs_TitleFollowsTheNewName()
	{
		await OpenAsync();

		await _viewModel.RecipeFile.SaveAsRecipeCommand.Execute();

		_viewModel.WindowTitle.Should().Be(TitlePrefix + Path.GetFileNameWithoutExtension(_savedAsFilePath));
	}

	[AvaloniaFact]
	public async Task New_TitleShowsTheNewRecipeName()
	{
		await OpenAsync();

		await _viewModel.RecipeFile.NewRecipeCommand.Execute();

		_viewModel.WindowTitle.Should().Be(TitlePrefix + Resources.WindowTitleNewRecipe);
	}

	[AvaloniaFact]
	public async Task EditAfterOpen_AddsTheDirtyMarker()
	{
		await OpenAsync();

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		_viewModel.WindowTitle.Should().Be(TitlePrefix + Path.GetFileNameWithoutExtension(_openedFilePath) + " *");
	}

	private async Task OpenAsync()
	{
		await _viewModel.RecipeFile.LoadRecipeCommand.Execute();
	}
}
