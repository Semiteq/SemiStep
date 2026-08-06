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

	public SelectionColors Build()
	{
		return new SelectionColors(
			Background: Background.ToStyleColor(),
			Foreground: Foreground.ToStyleColor());
	}
}
