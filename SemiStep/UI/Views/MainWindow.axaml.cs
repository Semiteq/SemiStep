using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

using UI.Converters;
using UI.ViewModels;

using Shared;
using Shared.Entities;

namespace UI.Views;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		DataContextChanged += OnDataContextChanged;
	}

	private void OnDataContextChanged(object? sender, EventArgs e)
	{
		if (DataContext is MainWindowViewModel viewModel && viewModel.Configuration is not null)
		{
			BuildColumnsFromConfiguration(viewModel.Configuration);
		}
	}

	private void BuildColumnsFromConfiguration(AppConfiguration config)
	{
		var grid = this.FindControl<DataGrid>("RecipeGrid");
		if (grid is null)
		{
			return;
		}

		grid.Columns.Clear();

		grid.Columns.Add(new DataGridTextColumn
		{
			Header = "Step",
			Binding = new Binding("StepNumber"),
			IsReadOnly = true,
			Width = new DataGridLength(60)
		});

		grid.Columns.Add(new DataGridTextColumn
		{
			Header = "Action",
			Binding = new Binding("ActionName"),
			IsReadOnly = true,
			Width = new DataGridLength(150)
		});

		foreach (var columnDef in config.Columns.Values.OrderBy(c => c.Key))
		{
			var propertyDef = config.Properties[columnDef.PropertyTypeId];
			var column = CreateColumnFromDefinition(columnDef, propertyDef);
			grid.Columns.Add(column);
		}
	}

	private DataGridColumn CreateColumnFromDefinition(
		Shared.Entities.ColumnDefinition columnDef,
		PropertyDefinition propertyDef)
	{
		var width = columnDef.Width > 0 ? columnDef.Width : 100;
		
		var column = new DataGridTextColumn
		{
			Header = columnDef.UiName,
			Width = new DataGridLength(width),
			IsReadOnly = columnDef.ReadOnly,
			Binding = new Binding
			{
				Path = ".",
				Converter = new PropertyValueConverter(columnDef.PropertyTypeId),
				Mode = columnDef.ReadOnly ? BindingMode.OneWay : BindingMode.TwoWay
			}
		};

		return column;
	}
}
