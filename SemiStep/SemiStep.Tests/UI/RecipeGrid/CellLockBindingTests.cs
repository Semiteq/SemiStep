using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using AwesomeAssertions;

using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

// The canonical combo cell's lock leg: a cell outside a DataGrid is locked (fail closed) and logs no binding error.
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class CellLockBindingTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(3);
	}

	public async ValueTask DisposeAsync()
	{
		_window?.Close();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void ComboCellOutsideAGrid_IsLocked_AndLogsNoBindingError()
	{
		var surface = _fixture.CreateCanonicalSurface();
		surface.Initialize();
		surface.IsReadOnly.Should().BeFalse("the check needs an unlocked surface");
		var presenter = ActionCell();
		presenter.DataContext = surface.RecipeRows[1];
		var panel = new StackPanel();
		_window = new Window { Content = panel };
		_window.Show();
		using var guard = new BindingErrorGuard();

		panel.Children.Add(presenter);
		Dispatcher.UIThread.RunJobs();
		var enabledWhileAttached = presenter.IsEnabled;
		panel.Children.Remove(presenter);
		Dispatcher.UIThread.RunJobs();

		enabledWhileAttached.Should().BeFalse("a cell outside a DataGrid takes no input");
		presenter.IsEnabled.Should().BeFalse("a detached cell takes no input");
		guard.AssertNoBindingErrors();
	}

	private LazyComboCellPresenter ActionCell()
	{
		var factory = new ComboBoxCellFactory(_fixture.ComboBoxItemsProvider, _fixture.AppConfiguration.GridStyle);
		var column = (DataGridTemplateColumn)factory.CreateActionColumn(
			_fixture.RecipeMetadataRegistry.GetColumn("action").Value,
			new DataGridLength(100),
			new CellEditCoordinator());

		return (LazyComboCellPresenter)column.CellTemplate!.Build(null)!;
	}
}
