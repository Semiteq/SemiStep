using Avalonia.Media;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.UI.Styles;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="ChangedCellColors"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class ChangedCellColorsDraft(ChangedCellColors source) : ReactiveObject
{
	// Hides ReactiveObject.Changed (the get-only change observable). The leaf name mirrors the record
	// component ChangedCellColors.Changed by design; the AXAML path ChangedCells.Changed and the guard
	// reflection both resolve to this get/set property, not the base observable.
	public new Color Changed
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Changed.ToMediaColor();

	public Color ChangedSelected
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.ChangedSelected.ToMediaColor();

	public ChangedCellColors Build()
	{
		return new ChangedCellColors(
			Changed: Changed.ToStyleColor(),
			ChangedSelected: ChangedSelected.ToStyleColor());
	}
}
