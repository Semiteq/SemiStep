using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

internal sealed class TextCellFactory
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
			CellTemplate = columnDef.ColumnType == ColumnTypes.StepStartTimeField
				? CreateStepStartTimeTemplate(columnDef.Key)
				: CreateMultiBindingTemplate(columnDef.Key)
		};
	}

	public DataGridColumn CreateEditableColumn(GridColumnDefinition columnDef, DataGridLength width)
	{
		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Tag = columnDef.Key,
			Width = width,
			IsReadOnly = false,
			CanUserSort = false,
			CellTemplate = columnDef.ColumnType == ColumnTypes.StepStartTimeField
				? CreateStepStartTimeTemplate(columnDef.Key)
				: CreateMultiBindingTemplate(columnDef.Key),
			CellEditingTemplate = CreateEditingTemplate(columnDef.Key)
		};
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateStepStartTimeTemplate(string columnKey)
	{
		var cellStateConverter = new CellStateConverter(columnKey);

		return new FuncDataTemplate<RecipeRowViewModel>((_, _) =>
		{
			var textBlock = new TextBlock
			{
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(4, 2),
				TextAlignment = TextAlignment.Center,
			};

			textBlock.Bind(TextBlock.TextProperty, new Binding(nameof(RecipeRowViewModel.StepStartTime))
			{
				Mode = BindingMode.OneWay
			});

			return CellPresenter.Wrap(textBlock, cellStateConverter);
		}, supportsRecycling: true);
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateMultiBindingTemplate(string columnKey)
	{
		var cellStateConverter = new CellStateConverter(columnKey);
		var bindingPath = ResolveBindingPath(columnKey);
		var unitsConverter = new DictionaryEntryConverter<string?>(columnKey, null);
		var formatKindConverter = new DictionaryEntryConverter<string>(columnKey, TimeFormatHelper.DefaultFormatKind);
		var multiConverter = new PropertyTimeMultiConverter();

		return new FuncDataTemplate<RecipeRowViewModel>((_, _) =>
		{
			var textBlock = new TextBlock
			{
				VerticalAlignment = VerticalAlignment.Center,
				Padding = new Thickness(4, 2),
				TextAlignment = TextAlignment.Center,
			};

			textBlock.Bind(TextBlock.TextProperty, new MultiBinding
			{
				Converter = multiConverter,
				Bindings =
				{
					new Binding(bindingPath) { Mode = BindingMode.OneWay },
					new Binding(nameof(RecipeRowViewModel.ColumnUnits))
					{
						Mode = BindingMode.OneWay,
						Converter = unitsConverter
					},
					new Binding(nameof(RecipeRowViewModel.ColumnFormatKinds))
					{
						Mode = BindingMode.OneWay,
						Converter = formatKindConverter
					}
				}
			});

			return CellPresenter.Wrap(textBlock, cellStateConverter);
		}, supportsRecycling: true);
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateEditingTemplate(string columnKey)
	{
		var cellStateConverter = new CellStateConverter(columnKey);
		var bindingPath = ResolveBindingPath(columnKey);

		return new FuncDataTemplate<RecipeRowViewModel>((row, _) =>
		{
			var formatKind = row?.ColumnFormatKinds
				.GetValueOrDefault(columnKey, TimeFormatHelper.DefaultFormatKind)
				?? TimeFormatHelper.DefaultFormatKind;

			var editingConverter = new PropertyTimeEditingConverter(formatKind);

			var textBox = new TextBox
			{
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				BorderThickness = new Thickness(0),
				Padding = new Thickness(4, 2),
				TextAlignment = TextAlignment.Center,
			};

			textBox.Bind(TextBox.TextProperty, new Binding(bindingPath)
			{
				Mode = BindingMode.TwoWay,
				UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
				Converter = editingConverter
			});

			return CellPresenter.Wrap(textBox, cellStateConverter);
		}, supportsRecycling: false);
	}

	private static string ResolveBindingPath(string columnKey)
	{
		return columnKey == TimeFormatHelper.StepStartTimeColumnKey
			? nameof(RecipeRowViewModel.StepStartTime)
			: ColumnTypes.IndexerPath(columnKey);
	}
}
