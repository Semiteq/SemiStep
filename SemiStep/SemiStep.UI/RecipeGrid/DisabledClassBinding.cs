using Avalonia.Data;
using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid;

internal static class DisabledClassBinding
{
	public static BindingBase Create(IValueConverter converter)
	{
		return new Binding(nameof(RecipeRowViewModel.InapplicableColumns))
		{
			Converter = converter,
			Mode = BindingMode.OneWay,
		};
	}
}
