using ReactiveUI;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="GridStyleFonts"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class GridStyleFontsDraft(GridStyleFonts source) : ReactiveObject
{
	public string? FontFamily
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.FontFamily;

	public decimal? HeaderFontSize
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.HeaderFontSize;

	public int HeaderFontWeight
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.HeaderFontWeight;

	public bool HeaderItalic
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.HeaderItalic;

	public decimal? CellFontSize
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.CellFontSize;

	public int CellFontWeight
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.CellFontWeight;

	public bool CellItalic
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.CellItalic;

	public GridStyleFonts Build()
	{
		return new GridStyleFonts(
			FontFamily: FontFamily ?? "",
			HeaderFontSize: DraftNumbers.ToInt(HeaderFontSize),
			HeaderFontWeight: HeaderFontWeight,
			HeaderItalic: HeaderItalic,
			CellFontSize: DraftNumbers.ToInt(CellFontSize),
			CellFontWeight: CellFontWeight,
			CellItalic: CellItalic);
	}
}
