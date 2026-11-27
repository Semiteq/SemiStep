using SemiStep.Core.Configuration;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="GridStyleLayout"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class GridStyleLayoutDraft(GridStyleLayout source)
{
	public NumberDraft CellPaddingLeft { get; } =
		new((decimal)source.CellPaddingLeft, DraftRanges.MinPadding, DraftRanges.MaxPadding);

	public NumberDraft CellPaddingTop { get; } =
		new((decimal)source.CellPaddingTop, DraftRanges.MinPadding, DraftRanges.MaxPadding);

	public NumberDraft CellPaddingRight { get; } =
		new((decimal)source.CellPaddingRight, DraftRanges.MinPadding, DraftRanges.MaxPadding);

	public NumberDraft CellPaddingBottom { get; } =
		new((decimal)source.CellPaddingBottom, DraftRanges.MinPadding, DraftRanges.MaxPadding);

	public NumberDraft RowHeight { get; } =
		new((decimal)source.RowHeight, DraftRanges.MinRowHeight, DraftRanges.MaxRowHeight);

	public GridStyleLayout Build()
	{
		return new GridStyleLayout(
			CellPaddingLeft: CellPaddingLeft.ToDouble(),
			CellPaddingTop: CellPaddingTop.ToDouble(),
			CellPaddingRight: CellPaddingRight.ToDouble(),
			CellPaddingBottom: CellPaddingBottom.ToDouble(),
			RowHeight: RowHeight.ToDouble());
	}
}
