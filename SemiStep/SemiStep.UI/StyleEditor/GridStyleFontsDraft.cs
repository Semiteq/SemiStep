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

	public NumberDraft HeaderFontSize { get; } =
		new(source.HeaderFontSize, DraftRanges.MinFontSize, DraftRanges.MaxFontSize);

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

	public NumberDraft CellFontSize { get; } =
		new(source.CellFontSize, DraftRanges.MinFontSize, DraftRanges.MaxFontSize);

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
			HeaderFontSize: HeaderFontSize.ToInt(),
			HeaderFontWeight: HeaderFontWeight,
			HeaderItalic: HeaderItalic,
			CellFontSize: CellFontSize.ToInt(),
			CellFontWeight: CellFontWeight,
			CellItalic: CellItalic);
	}
}
