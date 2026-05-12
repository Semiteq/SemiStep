using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Data;
using Avalonia.Layout;

using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

[PseudoClasses(PseudoClassEnabled, PseudoClassReadonly, PseudoClassDisabled, PseudoClassCurrentStep, PseudoClassPastStep)]
public sealed class CellPresenter : ContentControl
{
	private const string PseudoClassEnabled = ":cell-enabled";
	private const string PseudoClassReadonly = ":cell-readonly";
	private const string PseudoClassDisabled = ":cell-disabled";
	private const string PseudoClassCurrentStep = ":step-current";
	private const string PseudoClassPastStep = ":step-past";

	public static readonly StyledProperty<CellState> CellStateProperty =
		AvaloniaProperty.Register<CellPresenter, CellState>(nameof(CellState), defaultValue: CellState.Enabled);

	public static readonly StyledProperty<bool> IsCurrentStepProperty =
		AvaloniaProperty.Register<CellPresenter, bool>(nameof(IsCurrentStep), defaultValue: false);

	public static readonly StyledProperty<bool> IsPastStepProperty =
		AvaloniaProperty.Register<CellPresenter, bool>(nameof(IsPastStep), defaultValue: false);

	static CellPresenter()
	{
		CellStateProperty.Changed.AddClassHandler<CellPresenter>(OnCellStateChanged);
		IsCurrentStepProperty.Changed.AddClassHandler<CellPresenter>(OnIsCurrentStepChanged);
		IsPastStepProperty.Changed.AddClassHandler<CellPresenter>(OnIsPastStepChanged);
	}

	public CellState CellState
	{
		get => GetValue(CellStateProperty);
		set => SetValue(CellStateProperty, value);
	}

	public bool IsCurrentStep
	{
		get => GetValue(IsCurrentStepProperty);
		set => SetValue(IsCurrentStepProperty, value);
	}

	public bool IsPastStep
	{
		get => GetValue(IsPastStepProperty);
		set => SetValue(IsPastStepProperty, value);
	}

	public static CellPresenter Wrap(Control content, CellStateConverter cellStateConverter)
	{
		var presenter = new CellPresenter
		{
			Focusable = false,
			HorizontalContentAlignment = HorizontalAlignment.Stretch,
			VerticalContentAlignment = VerticalAlignment.Stretch,
			Content = content
		};
		presenter.Bind(CellStateProperty,
			new Binding(nameof(RecipeRowViewModel.CellStates)) { Converter = cellStateConverter });
		presenter.Bind(IsCurrentStepProperty,
			new Binding(nameof(RecipeRowViewModel.IsCurrentStep)));
		presenter.Bind(IsPastStepProperty,
			new Binding(nameof(RecipeRowViewModel.IsPastStep)));

		return presenter;
	}

	private static void OnCellStateChanged(CellPresenter sender, AvaloniaPropertyChangedEventArgs e)
	{
		var state = sender.CellState;
		sender.PseudoClasses.Set(PseudoClassEnabled, state == CellState.Enabled);
		sender.PseudoClasses.Set(PseudoClassReadonly, state == CellState.Readonly);
		sender.PseudoClasses.Set(PseudoClassDisabled, state == CellState.Disabled);
	}

	private static void OnIsCurrentStepChanged(CellPresenter sender, AvaloniaPropertyChangedEventArgs e)
	{
		sender.PseudoClasses.Set(PseudoClassCurrentStep, sender.IsCurrentStep);
	}

	private static void OnIsPastStepChanged(CellPresenter sender, AvaloniaPropertyChangedEventArgs e)
	{
		sender.PseudoClasses.Set(PseudoClassPastStep, sender.IsPastStep);
	}
}
