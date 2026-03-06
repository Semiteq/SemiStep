using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

using Shared.Entities;

using UI.Controls;
using UI.ViewModels;

namespace UI.Helpers;

public sealed class TextCellFactory
{
	public DataGridColumn CreateReadOnlyColumn(GridColumnDefinition columnDef, DataGridLength width)
	{
		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Width = width,
			IsReadOnly = true,
			CanUserSort = false,
			CellTemplate = CreateTemplate(columnDef.Key, isEditing: false, isColumnReadOnly: true)
		};
	}

	public DataGridColumn CreateEditableColumn(GridColumnDefinition columnDef, DataGridLength width)
	{
		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Width = width,
			IsReadOnly = false,
			CanUserSort = false,
			CellTemplate = CreateTemplate(columnDef.Key, isEditing: false, isColumnReadOnly: false),
			CellEditingTemplate = CreateTemplate(columnDef.Key, isEditing: true, isColumnReadOnly: false)
		};
	}

	private static FuncDataTemplate<RecipeRowViewModel> CreateTemplate(
		string columnKey, bool isEditing, bool isColumnReadOnly)
	{
		return new FuncDataTemplate<RecipeRowViewModel>(
			(row, _) => BuildCell(row, columnKey, isEditing, isColumnReadOnly),
			supportsRecycling: false);
	}

	private static Control BuildCell(
		RecipeRowViewModel? row, string columnKey, bool isEditing, bool isColumnReadOnly)
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

		if (!isEditing || isColumnReadOnly || cellState == CellState.Readonly)
		{
			var value = row.GetPropertyValue(columnKey);
			var displayText = value?.ToString() ?? string.Empty;
			var units = row.GetUnitsForColumn(columnKey);
			if (!string.IsNullOrEmpty(displayText) && !string.IsNullOrEmpty(units))
			{
				displayText = $"{displayText} {units}";
			}

			presenter.Content = new TextBlock
			{
				Text = displayText,
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(4, 2),
				TextAlignment = TextAlignment.Center,
			};
			return presenter;
		}

		var textBox = new TextBox
		{
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			BorderThickness = new Thickness(0),
			Padding = new Thickness(4, 2),
			TextAlignment = TextAlignment.Center,
		};

		textBox.Bind(TextBox.TextProperty,
			new Binding($"[{columnKey}]")
			{
				Mode = BindingMode.TwoWay,
				UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
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
}
