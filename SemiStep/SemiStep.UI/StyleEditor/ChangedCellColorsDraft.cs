using SemiStep.Core.Configuration;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="ChangedCellColors"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class ChangedCellColorsDraft(ChangedCellColors source)
{
	public ColorDraft Changed { get; } = new(source.Changed);

	public ChangedCellColors Build()
	{
		return new ChangedCellColors(
			Changed: Changed.ToStyleColor());
	}
}
