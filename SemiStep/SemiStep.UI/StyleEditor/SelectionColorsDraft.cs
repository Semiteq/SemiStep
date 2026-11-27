using SemiStep.Core.Configuration;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="SelectionColors"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class SelectionColorsDraft(SelectionColors source)
{
	public ColorDraft Accent { get; } = new(source.Accent);

	public NumberDraft Weight { get; } =
		new((decimal)source.Weight, DraftRanges.MinWeight, DraftRanges.MaxWeight);

	public SelectionColors Build()
	{
		return new SelectionColors(
			Accent: Accent.ToStyleColor(),
			Weight: Weight.ToDouble());
	}
}
