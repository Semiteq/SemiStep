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
	public DataGridColumn CreateReadOnlyColumn(
		GridColumnDefinition columnDef,
		DataGridLength width,
		CellAlignment alignment)
	{
		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Tag = columnDef.Key,
			Width = width,
			IsReadOnly = true,
			CanUserSort = false,
			CellTemplate = CreateDisplayTemplate(columnDef, alignment),
		};
	}

	public DataGridColumn CreateEditableColumn(
		GridColumnDefinition columnDef,
		DataGridLength width,
		int? maxLength,
		CellAlignment alignment)
	{
		return new DataGridTemplateColumn
		{
			Header = columnDef.UiName,
			Tag = columnDef.Key,
			Width = width,
			IsReadOnly = false,
			CanUserSort = false,
			CellTemplate = CreateDisplayTemplate(columnDef, alignment),
			CellEditingTemplate = CreateEditingTemplate(columnDef.Key, maxLength, alignment),
		};
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateDisplayTemplate(
		GridColumnDefinition columnDef,
		CellAlignment alignment)
	{
		return ColumnTypes.IsStepStartTimeColumn(columnDef.ColumnType)
			? CreateStepStartTimeTemplate()
			: CreateValueTemplate(columnDef.Key, alignment);
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateStepStartTimeTemplate()
	{
		return new FuncDataTemplate<RecipeRowViewModel>(
			(_, _) => TextCellLayout.Create(
				gridStyle,
				isNumeric: true,
				unitSlotWidth: 0,
				new Binding(nameof(RecipeRowViewModel.StepStartTime)) { Mode = BindingMode.OneWay },
				unitText: null),
			supportsRecycling: true);
	}

	private FuncDataTemplate<RecipeRowViewModel> CreateValueTemplate(string columnKey, CellAlignment alignment)
	{
		var bindingPath = ResolveBindingPath(columnKey);
		var unitsConverter = new DictionaryEntryConverter<string?>(columnKey, null);
		var formatKindConverter = new DictionaryEntryConverter<string>(columnKey, TimeFormatHelper.DefaultFormatKind);
		var multiConverter = new PropertyTimeMultiConverter();

		return new FuncDataTemplate<RecipeRowViewModel>((_, _) =>
		{
			var formatKind = new Binding(nameof(RecipeRowViewModel.ColumnFormatKinds))
			{
				Mode = BindingMode.OneWay,
				Converter = formatKindConverter,
			};
			var display = TextCellLayout.Create(
				gridStyle,
				alignment.IsNumeric,
				alignment.UnitSlotWidth,
				new MultiBinding
				{
					Converter = multiConverter,
					Bindings = { new Binding(bindingPath) { Mode = BindingMode.OneWay }, formatKind },
				},
				new MultiBinding
				{
					Converter = TextCellLayout.UnitTextConverter,
					Bindings =
					{
						new Binding(nameof(RecipeRowViewModel.ColumnUnits))
						{
							Mode = BindingMode.OneWay,
							Converter = unitsConverter,
						},
						formatKind,
					},
				});

			display.Bind(Visual.IsVisibleProperty, CellApplicabilityBinding.CreateApplicableBinding(columnKey));

			return display;
		}, supportsRecycling: true);
	}

	public FuncDataTemplate<RecipeRowViewModel> CreateEditingTemplate(
		string columnKey,
		int? maxLength,
		CellAlignment alignment)
	{
		var bindingPath = ResolveBindingPath(columnKey);

		return new FuncDataTemplate<RecipeRowViewModel>((row, _) =>
		{
			var formatKind = row?.ColumnFormatKinds.GetValueOrDefault(columnKey)
				?? TimeFormatHelper.DefaultFormatKind;

			var editingConverter = new PropertyTimeEditingConverter(formatKind, isTextColumn: maxLength.HasValue);

			var textBox = new TextBox
			{
				VerticalAlignment = VerticalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				// The Semi TextBox theme floors MinHeight at 32; on a shorter configured row that
				// inflates the editing cell and pushes the text above the display position. Size to the row.
				MinHeight = 0,
				BorderThickness = new Thickness(0),
				Padding = TextCellLayout.EditorPadding(gridStyle.Layout, alignment.IsNumeric, alignment.UnitSlotWidth),
				TextAlignment = alignment.IsNumeric ? TextAlignment.Right : TextAlignment.Left,
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

/// <summary>How a value column lays out its cells: a number with a unit slot, or left-aligned text.</summary>
internal readonly record struct CellAlignment(bool IsNumeric, double UnitSlotWidth);
