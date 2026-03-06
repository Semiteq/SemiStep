using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

using Shared.Entities;
using Shared.Registries;

using UI.Controls;
using UI.Converters;
using UI.ViewModels;

namespace UI.Helpers;

public sealed class TextCellFactory(IPropertyRegistry propertyRegistry, IColumnRegistry columnRegistry)
{
	private const string TimeHmsFormat = "time_hms";
	private const string NumericFormat = "numeric";
	private const string StepStartTimeColumnKey = "step_start_time";
	private const string StepStartTimeFieldType = "step_start_time_field";
	private const string TimeUnits = "с";

	public DataGridColumn CreateReadOnlyColumn(GridColumnDefinition columnDef, DataGridLength width)
	{
		var formatKind = ResolveFormatKind(columnDef.Key);

		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Width = width,
			IsReadOnly = true,
			CanUserSort = false,
			CellTemplate = CreateTemplate(columnDef.Key, isEditing: false, isColumnReadOnly: true, formatKind)
		};
	}

	public DataGridColumn CreateEditableColumn(GridColumnDefinition columnDef, DataGridLength width)
	{
		var formatKind = ResolveFormatKind(columnDef.Key);

		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Width = width,
			IsReadOnly = false,
			CanUserSort = false,
			CellTemplate = CreateTemplate(columnDef.Key, isEditing: false, isColumnReadOnly: false, formatKind),
			CellEditingTemplate = CreateTemplate(columnDef.Key, isEditing: true, isColumnReadOnly: false, formatKind)
		};
	}

	private static FuncDataTemplate<RecipeRowViewModel> CreateTemplate(
		string columnKey, bool isEditing, bool isColumnReadOnly, string formatKind)
	{
		return new FuncDataTemplate<RecipeRowViewModel>(
			(row, _) => BuildCell(row, columnKey, isEditing, isColumnReadOnly, formatKind),
			supportsRecycling: false);
	}

	private static Control BuildCell(
		RecipeRowViewModel? row, string columnKey, bool isEditing, bool isColumnReadOnly,
		string formatKind)
	{
		if (row is null)
		{
			return CreateEmptyTextBlock();
		}

		var cellState = row.CellStates.TryGetValue(columnKey, out var state) ? state : CellState.Enabled;
		var presenter = CreatePresenter(row, columnKey, cellState);

		if (cellState == CellState.Disabled)
		{
			presenter.Content = CreateEmptyTextBlock();
			return presenter;
		}

		var isStepStartTime = columnKey == StepStartTimeColumnKey;
		var bindingPath = isStepStartTime ? nameof(RecipeRowViewModel.StepStartTime) : $"[{columnKey}]";
		var units = isStepStartTime ? TimeUnits : row.GetUnitsForColumn(columnKey);

		if (!isEditing || isColumnReadOnly || cellState == CellState.Readonly)
		{
			var displayConverter = new PropertyValueConverter(formatKind, units, appendUnits: true);
			var textBlock = new TextBlock
			{
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(4, 2),
				TextAlignment = TextAlignment.Center,
			};

			textBlock.Bind(TextBlock.TextProperty,
				new Binding(bindingPath)
				{
					Mode = BindingMode.OneWay,
					Converter = displayConverter
				});

			presenter.Content = textBlock;
			return presenter;
		}

		var editingConverter = new PropertyValueConverter(formatKind, units, appendUnits: false);
		var textBox = new TextBox
		{
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			BorderThickness = new Thickness(0),
			Padding = new Thickness(4, 2),
			TextAlignment = TextAlignment.Center,
		};

		textBox.Bind(TextBox.TextProperty,
			new Binding(bindingPath)
			{
				Mode = BindingMode.TwoWay,
				UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
				Converter = editingConverter
			});

		presenter.Content = textBox;
		return presenter;
	}

	private static CellPresenter CreatePresenter(
		RecipeRowViewModel row, string columnKey, CellState cellState)
	{
		var presenter = new CellPresenter
		{
			CellState = cellState,
			HorizontalContentAlignment = HorizontalAlignment.Stretch,
			VerticalContentAlignment = VerticalAlignment.Stretch
		};

		SubscribeWithLifecycle(presenter, row, columnKey);
		return presenter;
	}

	private static void SubscribeWithLifecycle(
		CellPresenter presenter, RecipeRowViewModel row, string columnKey)
	{
		PropertyChangedEventHandler handler = (_, e) =>
		{
			if (e.PropertyName is nameof(RecipeRowViewModel.CellStates))
			{
				var newState = row.CellStates.TryGetValue(columnKey, out var s) ? s : CellState.Enabled;
				presenter.CellState = newState;
			}
		};

		row.PropertyChanged += handler;

		presenter.DetachedFromVisualTree += OnDetached;
		presenter.AttachedToVisualTree += OnAttached;
		return;

		void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
		{
			row.PropertyChanged -= handler;
		}

		void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
		{
			row.PropertyChanged -= handler;
			row.PropertyChanged += handler;
		}
	}

	private static TextBlock CreateEmptyTextBlock()
	{
		return new TextBlock { Text = string.Empty };
	}

	private string ResolveFormatKind(string columnKey)
	{
		if (!columnRegistry.ColumnExists(columnKey))
		{
			return NumericFormat;
		}

		var columnDef = columnRegistry.GetColumn(columnKey);

		if (columnDef.ColumnType == StepStartTimeFieldType)
		{
			return TimeHmsFormat;
		}

		if (propertyRegistry.PropertyExists(columnDef.PropertyTypeId))
		{
			var propDef = propertyRegistry.GetProperty(columnDef.PropertyTypeId);
			return propDef.FormatKind;
		}

		return NumericFormat;
	}
}
