using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

internal sealed class TextCellFactory(GridStyleOptions gridStyle)
{
	public DataGridColumn CreateReadOnlyColumn(GridColumnDefinition columnDef, DataGridLength width)
	{
		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Tag = columnDef.Key,
			Width = width,
			IsReadOnly = true,
			CanUserSort = false,
			CellTemplate = ColumnTypes.IsStepStartTimeColumn(columnDef.ColumnType)
				? CreateStepStartTimeTemplate()
				: CreateMultiBindingTemplate(columnDef.Key),
		};
	}

	public DataGridColumn CreateEditableColumn(GridColumnDefinition columnDef, DataGridLength width, int? maxLength)
	{
		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Tag = columnDef.Key,
			Width = width,
			IsReadOnly = false,
			CanUserSort = false,
			CellTemplate = ColumnTypes.IsStepStartTimeColumn(columnDef.ColumnType)
				? CreateStepStartTimeTemplate()
				: CreateMultiBindingTemplate(columnDef.Key),
			CellEditingTemplate = CreateEditingTemplate(columnDef.Key, maxLength),
		};
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateStepStartTimeTemplate()
	{
		return new FuncDataTemplate<RecipeRowViewModel>((_, _) =>
		{
			var textBlock = new TextBlock
			{
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(
					gridStyle.Layout.CellPaddingLeft,
					gridStyle.Layout.CellPaddingTop,
					gridStyle.Layout.CellPaddingRight,
					gridStyle.Layout.CellPaddingBottom),
				TextAlignment = TextAlignment.Center,
			};
			GridFontApplier.ApplyCellFont(textBlock, gridStyle);

			textBlock.Bind(TextBlock.TextProperty, new Binding(nameof(RecipeRowViewModel.StepStartTime))
			{
				Mode = BindingMode.OneWay,
			});

			return textBlock;
		}, supportsRecycling: true);
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateMultiBindingTemplate(string columnKey)
	{
		var bindingPath = ResolveBindingPath(columnKey);
		var unitsConverter = new DictionaryEntryConverter<string?>(columnKey, null);
		var formatKindConverter = new DictionaryEntryConverter<string>(columnKey, TimeFormatHelper.DefaultFormatKind);
		var multiConverter = new PropertyTimeMultiConverter();

		return new FuncDataTemplate<RecipeRowViewModel>((_, _) =>
		{
			var textBlock = new TextBlock
			{
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(
					gridStyle.Layout.CellPaddingLeft,
					gridStyle.Layout.CellPaddingTop,
					gridStyle.Layout.CellPaddingRight,
					gridStyle.Layout.CellPaddingBottom),
				TextAlignment = TextAlignment.Center,
			};
			GridFontApplier.ApplyCellFont(textBlock, gridStyle);

			textBlock.Bind(TextBlock.TextProperty, new MultiBinding
			{
				Converter = multiConverter,
				Bindings =
				{
					new Binding(bindingPath) { Mode = BindingMode.OneWay },
					new Binding(nameof(RecipeRowViewModel.ColumnUnits))
					{
						Mode = BindingMode.OneWay,
						Converter = unitsConverter,
					},
					new Binding(nameof(RecipeRowViewModel.ColumnFormatKinds))
					{
						Mode = BindingMode.OneWay,
						Converter = formatKindConverter,
					},
				},
			});

			return textBlock;
		}, supportsRecycling: true);
	}

	public FuncDataTemplate<RecipeRowViewModel> CreateEditingTemplate(string columnKey, int? maxLength)
	{
		var bindingPath = ResolveBindingPath(columnKey);

		return new FuncDataTemplate<RecipeRowViewModel>((row, _) =>
		{
			var formatKind = row?.ColumnFormatKinds.GetValueOrDefault(columnKey)
				?? TimeFormatHelper.DefaultFormatKind;

			var editingConverter = new PropertyTimeEditingConverter(formatKind, allowsEmpty: maxLength.HasValue);

			var textBox = new TextBox
			{
				VerticalAlignment = VerticalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				// The Semi TextBox theme floors MinHeight at 32; on a shorter configured row that
				// inflates the editing cell and pushes the text above the display position. Size to the row.
				MinHeight = 0,
				BorderThickness = new Thickness(0),
				Padding = new Thickness(
					gridStyle.Layout.CellPaddingLeft,
					gridStyle.Layout.CellPaddingTop,
					gridStyle.Layout.CellPaddingRight,
					gridStyle.Layout.CellPaddingBottom),
				TextAlignment = TextAlignment.Center,
			};
			GridFontApplier.ApplyCellFont(textBox, gridStyle);

			if (maxLength.HasValue)
			{
				textBox.MaxLength = maxLength.Value;
			}

			textBox.Bind(TextBox.TextProperty, new Binding(bindingPath)
			{
				Mode = BindingMode.TwoWay,
				UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
				Converter = editingConverter,
			});

			return textBox;
		}, supportsRecycling: false);
	}

	private static string ResolveBindingPath(string columnKey)
	{
		return columnKey == TimeFormatHelper.StepStartTimeColumnKey
			? nameof(RecipeRowViewModel.StepStartTime)
			: ColumnTypes.IndexerPath(columnKey);
	}
}
