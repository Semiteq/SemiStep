using ReactiveUI;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="GridStyleLayout"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class GridStyleLayoutDraft(GridStyleLayout source) : ReactiveObject
{
	public decimal? CellPaddingLeft
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = (decimal)source.CellPaddingLeft;

	public decimal? CellPaddingTop
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = (decimal)source.CellPaddingTop;

	public decimal? CellPaddingRight
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = (decimal)source.CellPaddingRight;

	public decimal? CellPaddingBottom
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = (decimal)source.CellPaddingBottom;

	public decimal? RowHeight
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = (decimal)source.RowHeight;

	public GridStyleLayout Build()
	{
		return new GridStyleLayout(
			CellPaddingLeft: DraftNumbers.ToDouble(CellPaddingLeft),
			CellPaddingTop: DraftNumbers.ToDouble(CellPaddingTop),
			CellPaddingRight: DraftNumbers.ToDouble(CellPaddingRight),
			CellPaddingBottom: DraftNumbers.ToDouble(CellPaddingBottom),
			RowHeight: DraftNumbers.ToDouble(RowHeight));
	}
}
