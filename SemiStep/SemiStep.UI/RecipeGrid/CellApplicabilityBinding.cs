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
