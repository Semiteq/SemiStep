using Avalonia.Media;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.UI.Styles;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="StatusBarStyle"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class StatusBarStyleDraft(StatusBarStyle source) : ReactiveObject
{
	public Color Background
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Background.ToMediaColor();

	public Color Foreground
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Foreground.ToMediaColor();

	public decimal? Padding
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = (decimal)source.Padding;

	public decimal? ItemSpacing
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = (decimal)source.ItemSpacing;

	public decimal? FontSize
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.FontSize;

	public int Weight
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Weight;

	public bool Italic
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Italic;

	public decimal? TimerLabelFontSize
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.TimerLabelFontSize;

	public int TimerLabelWeight
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.TimerLabelWeight;

	public bool TimerLabelItalic
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.TimerLabelItalic;

	public decimal? TimerValueFontSize
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.TimerValueFontSize;

	public int TimerValueWeight
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.TimerValueWeight;

	public bool TimerValueItalic
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.TimerValueItalic;

	public StatusBarStyle Build()
	{
		return new StatusBarStyle(
			Background: Background.ToStyleColor(),
			Foreground: Foreground.ToStyleColor(),
			Padding: DraftNumbers.ToDouble(Padding),
			ItemSpacing: DraftNumbers.ToDouble(ItemSpacing),
			FontSize: DraftNumbers.ToInt(FontSize),
			Weight: Weight,
			Italic: Italic,
			TimerLabelFontSize: DraftNumbers.ToInt(TimerLabelFontSize),
			TimerLabelWeight: TimerLabelWeight,
			TimerLabelItalic: TimerLabelItalic,
			TimerValueFontSize: DraftNumbers.ToInt(TimerValueFontSize),
			TimerValueWeight: TimerValueWeight,
			TimerValueItalic: TimerValueItalic);
	}
}
