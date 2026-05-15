using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Styling;

namespace SemiStep.UI.RecipeGrid;

internal static class DisabledCellTheme
{
	public static readonly AttachedProperty<bool> IsApplicableProperty =
		AvaloniaProperty.RegisterAttached<DataGridCell, bool>(
			"IsApplicable",
			typeof(DisabledCellTheme),
			defaultValue: true);

	public static bool GetIsApplicable(DataGridCell cell)
	{
		return cell.GetValue(IsApplicableProperty);
	}

	public static void SetIsApplicable(DataGridCell cell, bool value)
	{
		cell.SetValue(IsApplicableProperty, value);
	}

	public static ControlTheme Create(string columnKey)
	{
		var theme = new ControlTheme(typeof(DataGridCell))
		{
			BasedOn = ResolveDefaultDataGridCellTheme(),
		};

		var binding = new Binding(nameof(RecipeRowViewModel.InapplicableColumns))
		{
			Converter = new FuncValueConverter<IReadOnlySet<string>?, bool>(
				set => set is null || !set.Contains(columnKey)),
			Mode = BindingMode.OneWay,
		};

		theme.Add(new Setter(IsApplicableProperty, binding));

		return theme;
	}

	private static ControlTheme? ResolveDefaultDataGridCellTheme()
	{
		return Application.Current?.FindResource(typeof(DataGridCell)) as ControlTheme;
	}
}
