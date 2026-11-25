using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid;

internal static class CellApplicabilityBinding
{
	public static Binding CreateApplicableBinding(string columnKey)
	{
		return new Binding(nameof(RecipeRowViewModel.InapplicableColumns))
		{
			Converter = new FuncValueConverter<IReadOnlySet<string>?, bool>(
				set => set is null || !set.Contains(columnKey)),
			Mode = BindingMode.OneWay,
		};
	}

	// Fails closed: until the cell is inside a DataGrid the lock leg is unresolved and the cell takes no input.
	public static MultiBinding CreateApplicableAndUnlockedBinding(string columnKey)
	{
		return new MultiBinding
		{
			Converter = BoolConverters.And,
			FallbackValue = false,
			Bindings =
			{
				CreateApplicableBinding(columnKey),
				new Binding(nameof(DataGrid.IsReadOnly))
				{
					RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
					{
						AncestorType = typeof(DataGrid),
					},
					Converter = BoolConverters.Not,
				},
			},
		};
	}

	public static Binding CreateInapplicableBinding(string columnKey)
	{
		return new Binding(nameof(RecipeRowViewModel.InapplicableColumns))
		{
			Converter = new FuncValueConverter<IReadOnlySet<string>?, bool>(
				set => set is not null && set.Contains(columnKey)),
			Mode = BindingMode.OneWay,
		};
	}

	public static Binding CreateChangedBinding(string columnKey)
	{
		return new Binding(nameof(RecipeRowViewModel.ChangedColumns))
		{
			Converter = new FuncValueConverter<IReadOnlySet<string>?, bool>(
				set => set is not null && set.Contains(columnKey)),
			Mode = BindingMode.OneWay,
		};
	}
}
