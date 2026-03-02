using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

using Shared.Entities;

namespace UI.Controls;

[PseudoClasses(PseudoClassEnabled, PseudoClassReadonly, PseudoClassDisabled)]
public sealed class CellPresenter : ContentControl
{
	private const string PseudoClassEnabled = ":cell-enabled";
	private const string PseudoClassReadonly = ":cell-readonly";
	private const string PseudoClassDisabled = ":cell-disabled";

	public static readonly StyledProperty<CellState> CellStateProperty =
		AvaloniaProperty.Register<CellPresenter, CellState>(nameof(CellState), defaultValue: CellState.Enabled);

	static CellPresenter()
	{
		CellStateProperty.Changed.AddClassHandler<CellPresenter>(OnCellStateChanged);
	}

	public CellState CellState
	{
		get => GetValue(CellStateProperty);
		set => SetValue(CellStateProperty, value);
	}

	private static void OnCellStateChanged(CellPresenter sender, AvaloniaPropertyChangedEventArgs e)
	{
		var state = sender.CellState;
		sender.PseudoClasses.Set(PseudoClassEnabled, state == CellState.Enabled);
		sender.PseudoClasses.Set(PseudoClassReadonly, state == CellState.Readonly);
		sender.PseudoClasses.Set(PseudoClassDisabled, state == CellState.Disabled);
	}
}
