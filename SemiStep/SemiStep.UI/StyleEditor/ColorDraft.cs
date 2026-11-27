using Avalonia.Media;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.UI.Styles;

namespace SemiStep.UI.StyleEditor;

/// <summary>One editable colour; the picker reads <see cref="Color"/> and writes back through <see cref="Pick"/>.</summary>
public sealed class ColorDraft(StyleColor source) : ReactiveObject
{
	public Color Color
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.ToMediaColor();

	/// <summary>Takes the picked colour as opaque, so the file gets the <c>#RRGGBB</c> form.</summary>
	public void Pick(Color picked)
	{
		Color = Color.FromRgb(picked.R, picked.G, picked.B);
	}

	public StyleColor ToStyleColor()
	{
		return Color.ToStyleColor();
	}
}
