using ReactiveUI;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="StatusBarStyle"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class StatusBarStyleDraft(StatusBarStyle source) : ReactiveObject
{
	public ColorDraft Background { get; } = new(source.Background);

	public ColorDraft Foreground { get; } = new(source.Foreground);

	public NumberDraft Padding { get; } =
		new((decimal)source.Padding, DraftRanges.MinPadding, DraftRanges.MaxPadding);

	public NumberDraft FontSize { get; } =
		new(source.FontSize, DraftRanges.MinFontSize, DraftRanges.MaxFontSize);

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

	public StatusBarStyle Build()
	{
		return new StatusBarStyle(
			Background: Background.ToStyleColor(),
			Foreground: Foreground.ToStyleColor(),
			Padding: Padding.ToDouble(),
			FontSize: FontSize.ToInt(),
			Weight: Weight,
			Italic: Italic);
	}
}
