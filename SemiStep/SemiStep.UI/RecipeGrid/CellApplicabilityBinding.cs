using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace SemiStep.UI.RecipeGrid;

internal static class CellApplicabilityBinding
{
	// Inherited from the DataGrid, which mirrors its own IsReadOnly into it; a cell outside a DataGrid keeps the
	// default and fails closed. The cell reads it from itself, so a detached cell logs no "Ancestor not found".
	public static readonly AttachedProperty<bool> IsGridUnlockedProperty =
		AvaloniaProperty.RegisterAttached<Control, bool>(
			"IsGridUnlocked",
			typeof(CellApplicabilityBinding),
			defaultValue: false,
			inherits: true);

	public static void MirrorGridLock(DataGrid grid)
	{
		grid.Bind(IsGridUnlockedProperty, grid.GetObservable(DataGrid.IsReadOnlyProperty, isReadOnly => !isReadOnly));
	}

	public static Binding CreateApplicableBinding(string columnKey)
	{
		return new Binding(nameof(RecipeRowViewModel.InapplicableColumns))
		{
			Converter = new FuncValueConverter<IReadOnlySet<string>?, bool>(
				set => set is null || !set.Contains(columnKey)),
			Mode = BindingMode.OneWay,
		};
	}

	public static MultiBinding CreateApplicableAndUnlockedBinding(string columnKey, Control cell)
	{
		return new MultiBinding
		{
			Converter = BoolConverters.And,
			FallbackValue = false,
			Bindings =
			{
				CreateApplicableBinding(columnKey),
				cell.GetObservable(IsGridUnlockedProperty).ToBinding(),
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
