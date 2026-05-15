using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Styling;

namespace SemiStep.UI.RecipeGrid;

internal static class InapplicableCellTheme
{
	public static readonly AttachedProperty<bool> IsInapplicableProperty =
		AvaloniaProperty.RegisterAttached<DataGridCell, bool>(
			"IsInapplicable",
			typeof(InapplicableCellTheme),
			defaultValue: false);

	public static bool GetIsInapplicable(DataGridCell cell)
	{
		return cell.GetValue(IsInapplicableProperty);
	}

	public static void SetIsInapplicable(DataGridCell cell, bool value)
	{
		cell.SetValue(IsInapplicableProperty, value);
	}

	public static ControlTheme Create(string columnKey)
	{
		var theme = new ControlTheme(typeof(DataGridCell))
		{
			BasedOn = ResolveDefaultDataGridCellTheme(),
		};

		theme.Add(new Setter(IsInapplicableProperty, CellApplicabilityBinding.CreateInapplicableBinding(columnKey)));

		return theme;
	}

	private static ControlTheme? ResolveDefaultDataGridCellTheme()
	{
		return Application.Current?.FindResource(typeof(DataGridCell)) as ControlTheme;
	}
}
