using Avalonia.Media;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.UI.Styles;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="SelectionColors"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class SelectionColorsDraft(SelectionColors source) : ReactiveObject
{
	public Color Accent
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Accent.ToMediaColor();

	public decimal? Weight
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = (decimal)source.Weight;

	public SelectionColors Build()
	{
		return new SelectionColors(
			Accent: Accent.ToStyleColor(),
			Weight: DraftNumbers.ToDouble(Weight));
	}
}
