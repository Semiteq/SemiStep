using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.Localization;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.Styles;

using Xunit;

using MainWindowView = SemiStep.UI.MainWindow.MainWindow;

namespace SemiStep.Tests.UI.RecipeGrid;

[Trait("Component", "UI")]
[Trait("Area", "EmptyHint")]
[Trait("Category", "Integration")]
public sealed class EmptyHintTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private Window? _window;

	public ValueTask InitializeAsync()
	{
		return _fixture.InitializeAsync();
	}

	public async ValueTask DisposeAsync()
	{
		_window?.Close();
		await _fixture.DisposeAsync();
	}

	[AvaloniaTheory]
	[InlineData(false)]
	[InlineData(true)]
	public void EmptyRecipe_ShowsTheHint_UntilTheFirstStep(bool transposed)
	{
		_fixture.Coordinator.NewRecipe();
		var surface = _fixture.CreateActiveSurface();
		surface.Initialize();
		if (transposed)
		{
			surface.ToggleOrientation();
		}

		var host = ShowHost(surface);

		Hint(host).IsVisible.Should().BeTrue();
		Hint(host).Text.Should().Be(Resources.EmptyRecipeHint);

		_fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		Dispatcher.UIThread.RunJobs();

		Hint(host).IsVisible.Should().BeFalse();
	}

	[AvaloniaFact]
	public async Task ZeroStepCsvLoad_AfterANonEmptyRecipe_ShowsTheHint()
	{
		var path = Path.Combine(Path.GetTempPath(), $"semistep-empty-{Guid.NewGuid():N}.csv");
		try
		{
			_fixture.Coordinator.NewRecipe();
			(await _fixture.Coordinator.SaveRecipeAsync(path)).IsSuccess.Should().BeTrue();
			_fixture.SeedRecipe(2);
			var surface = _fixture.CreateActiveSurface();
			surface.Initialize();
			var host = ShowHost(surface);
			Hint(host).IsVisible.Should().BeFalse();

			(await _fixture.Coordinator.LoadRecipeAsync(path)).IsSuccess.Should().BeTrue();
			Dispatcher.UIThread.RunJobs();

			Hint(host).IsVisible.Should().BeTrue();
		}
		finally
		{
			File.Delete(path);
		}
	}

	[AvaloniaFact]
	public async Task StepCsvLoad_NeverReportsEmpty_DuringTheLoad()
	{
		_fixture.SeedRecipe(4);
		var surface = _fixture.CreateActiveSurface();
		surface.Initialize();
		var reported = new List<bool>();
		surface.CanonicalSurface.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(CanonicalRecipeGridSurface.IsRecipeEmpty))
			{
				reported.Add(surface.CanonicalSurface.IsRecipeEmpty);
			}
		};

		await _fixture.ReloadFromCsvAsync();
		Dispatcher.UIThread.RunJobs();

		reported.Should().NotContain(true);
		surface.IsRecipeEmpty.Should().BeFalse();
	}

	[AvaloniaFact]
	public void Hint_TakesNoFocusAndNoInput_AndCtrlNAddsAStep()
	{
		_fixture.Coordinator.NewRecipe();
		var viewModel = _fixture.CreateMainWindowViewModel();
		viewModel.RecipeGrid.Initialize();
		var window = new MainWindowView { ViewModel = viewModel };
		_window = window;
		window.Show();
		Dispatcher.UIThread.RunJobs();
		var host = window.GetVisualDescendants().OfType<RecipeGridHost>().Single();
		var hint = Hint(host);
		hint.IsVisible.Should().BeTrue();
		hint.IsHitTestVisible.Should().BeFalse();
		hint.Focusable.Should().BeFalse();
		window.GetVisualDescendants().OfType<DataGrid>().Single().Focus();

		window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.Control);
		Dispatcher.UIThread.RunJobs();

		_fixture.Coordinator.CurrentRecipe.StepCount.Should().Be(1);
		hint.IsVisible.Should().BeFalse();
	}

	private static TextBlock Hint(RecipeGridHost host)
	{
		return host.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "EmptyRecipeHint");
	}

	private RecipeGridHost ShowHost(ActiveRecipeGridSurface surface)
	{
		var host = new RecipeGridHost { DataContext = surface };
		_window = new Window { Width = 1200, Height = 500 };
		CellPaletteInstaller.Install(_window.Resources, _fixture.AppConfiguration.GridStyle);
		_window.Content = host;
		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return host;
	}
}
