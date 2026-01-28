using Avalonia.Controls;

using UI.Helpers;
using UI.ViewModels;

namespace UI.Views;

public partial class MainWindow : Window
{
	private ColumnBuilder? _columnBuilder;
	private GridStyleApplier? _styleApplier;

	public MainWindow()
	{
		InitializeComponent();
		DataContextChanged += OnDataContextChanged;
	}

	private void OnDataContextChanged(object? sender, EventArgs e)
	{
		if (DataContext is not MainWindowViewModel viewModel || viewModel.Configuration is null)
		{
			return;
		}

		var gridStyles = viewModel.GridStyleProvider.Current;

		_columnBuilder = new ColumnBuilder(
			viewModel.ActionRegistry,
			gridStyles);

		_styleApplier = new GridStyleApplier();

		viewModel.GridStyleProvider.StylesChanged += OnStylesChanged;

		BuildGrid();
	}

	private void OnStylesChanged(object? sender, Shared.Entities.GridStyleOptions styles)
	{
		if (_columnBuilder is null || _styleApplier is null)
		{
			return;
		}

		_columnBuilder.UpdateStyles(styles);
		BuildGrid();
	}

	private void BuildGrid()
	{
		if (DataContext is not MainWindowViewModel viewModel || viewModel.Configuration is null)
		{
			return;
		}

		if (_columnBuilder is null || _styleApplier is null)
		{
			return;
		}

		var gridStyles = viewModel.GridStyleProvider.Current;

		_columnBuilder.BuildColumnsFromConfiguration(RecipeGrid, viewModel.Configuration);
		_styleApplier.ApplyGridStyles(RecipeGrid, gridStyles);
		_styleApplier.ApplyControlStyles(ValidationPanel, StatusBar, gridStyles);
	}

	private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (DataContext is not MainWindowViewModel viewModel)
		{
			return;
		}

		foreach (var row in viewModel.RecipeRows)
		{
			row.IsSelected = RecipeGrid.SelectedItems?.Contains(row) ?? false;
		}
	}
}
