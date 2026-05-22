using System.Collections.Immutable;
using System.Collections.ObjectModel;

using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

/// <summary>
/// Verifies that the production LoadingRow stamping wires <see cref="RecipeRowViewModel.IsForDepth1"/>,
/// <see cref="RecipeRowViewModel.IsForDepth2"/> and <see cref="RecipeRowViewModel.IsForDepth3"/> to the
/// row-level pseudo-classes consumed by <c>DataGridStyles.axaml</c>.
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Integration")]
public sealed class RowExecutionClassesStampingTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();

	public ValueTask InitializeAsync()
	{
		return _fixture.InitializeAsync();
	}

	public ValueTask DisposeAsync()
	{
		return _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void DataGridRows_ReceiveForDepthClasses_AccordingToRowViewModel()
	{
		// Assumption: 4 rows is small enough that the DataGrid materialises every container
		// without virtualisation, so GetVisualDescendants() enumerates all of them.
		var rows = new ObservableCollection<RecipeRowViewModel>
		{
			CreateRow(stepNumber: 1, forDepth: 0),
			CreateRow(stepNumber: 2, forDepth: 1),
			CreateRow(stepNumber: 3, forDepth: 2),
			CreateRow(stepNumber: 4, forDepth: 3),
		};

		var dataGrid = BuildDataGridWithStamping(rows);
		var window = new Window
		{
			Width = 800,
			Height = 600,
			Content = dataGrid,
		};

		window.Show();
		Dispatcher.UIThread.RunJobs();

		var dataGridRows = dataGrid.GetVisualDescendants().OfType<DataGridRow>().ToList();
		dataGridRows.Should().HaveCount(4, "all 4 row containers must materialise without virtualisation at this size");

		var rowByDataContext = dataGridRows.ToDictionary(r => (RecipeRowViewModel)r.DataContext!);

		AssertClasses(rowByDataContext[rows[0]], expectedDepth: 0);
		AssertClasses(rowByDataContext[rows[1]], expectedDepth: 1);
		AssertClasses(rowByDataContext[rows[2]], expectedDepth: 2);
		AssertClasses(rowByDataContext[rows[3]], expectedDepth: 3);

		window.Close();
	}

	private RecipeRowViewModel CreateRow(int stepNumber, int forDepth)
	{
		var action = _fixture.RecipeMetadataRegistry.GetAction(RecipeTestDriver.WaitActionId).Value;
		var step = new Step(RecipeTestDriver.WaitActionId, ImmutableDictionary<PropertyId, PropertyValue>.Empty);
		var row = new RecipeRowViewModel(stepNumber, step, action, _fixture.RecipeMetadataRegistry, new HashSet<string>())
		{
			ForDepth = forDepth,
		};
		return row;
	}

	private static DataGrid BuildDataGridWithStamping(ObservableCollection<RecipeRowViewModel> rows)
	{
		var grid = new DataGrid
		{
			ItemsSource = rows,
			Columns =
			{
				new DataGridTextColumn
				{
					Header = "Step",
					Binding = new Binding(nameof(RecipeRowViewModel.StepNumber)),
				},
			},
		};

		grid.LoadingRow += (_, e) => RecipeRowExecutionClassBinder.BindAll(e.Row);

		return grid;
	}

	private static void AssertClasses(DataGridRow dataGridRow, int expectedDepth)
	{
		var classes = dataGridRow.Classes;
		classes.Contains(RowExecutionClasses.ForDepth1Class).Should().Be(
			expectedDepth == 1,
			$"row at depth {expectedDepth} must {(expectedDepth == 1 ? "have" : "not have")} '{RowExecutionClasses.ForDepth1Class}'");
		classes.Contains(RowExecutionClasses.ForDepth2Class).Should().Be(
			expectedDepth == 2,
			$"row at depth {expectedDepth} must {(expectedDepth == 2 ? "have" : "not have")} '{RowExecutionClasses.ForDepth2Class}'");
		classes.Contains(RowExecutionClasses.ForDepth3Class).Should().Be(
			expectedDepth >= 3,
			$"row at depth {expectedDepth} must {(expectedDepth >= 3 ? "have" : "not have")} '{RowExecutionClasses.ForDepth3Class}'");
	}
}
