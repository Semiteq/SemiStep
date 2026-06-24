using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// Per-column <see cref="ControlTheme"/> for <see cref="DataGridCell"/> that carries two disjoint
/// cell-state signals via attached properties: <see cref="IsInapplicableProperty"/> (the cell does
/// not apply to the row's action) and <see cref="IsChangedProperty"/> (the cell was seeded with a
/// default value by an action / selector change). A <see cref="DataGridColumn"/> has a single
/// <c>CellTheme</c>, so both setters share the one theme built in <see cref="Create"/>.
/// </summary>
internal static class InapplicableCellTheme
{
	public static readonly AttachedProperty<bool> IsInapplicableProperty =
		AvaloniaProperty.RegisterAttached<DataGridCell, bool>(
			"IsInapplicable",
			typeof(InapplicableCellTheme),
			defaultValue: false);

	public static readonly AttachedProperty<bool> IsChangedProperty =
		AvaloniaProperty.RegisterAttached<DataGridCell, bool>(
			"IsChanged",
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

	public static bool GetIsChanged(DataGridCell cell)
	{
		return cell.GetValue(IsChangedProperty);
	}

	public static void SetIsChanged(DataGridCell cell, bool value)
	{
		cell.SetValue(IsChangedProperty, value);
	}

	public static ControlTheme Create(string columnKey)
	{
		var theme = new ControlTheme(typeof(DataGridCell))
		{
			BasedOn = ResolveDefaultDataGridCellTheme(),
		};

		theme.Add(new Setter(IsInapplicableProperty, CellApplicabilityBinding.CreateInapplicableBinding(columnKey)));
		theme.Add(new Setter(IsChangedProperty, CellApplicabilityBinding.CreateChangedBinding(columnKey)));

		return theme;
	}

	private static ControlTheme? ResolveDefaultDataGridCellTheme()
	{
		return Application.Current?.FindResource(typeof(DataGridCell)) as ControlTheme;
	}
}
