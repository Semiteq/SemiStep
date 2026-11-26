using Avalonia.Media;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.UI.Styles;

namespace SemiStep.UI.StyleEditor;

/// <summary>Editable draft of the read-only and inapplicable cell bases (<see cref="GridStyleOptions.ReadOnlyCell"/>, <see cref="GridStyleOptions.DisabledCell"/>).</summary>
public sealed class GreyCellColorsDraft(StyleColor readOnly, StyleColor disabled) : ReactiveObject
{
	public Color ReadOnly
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = readOnly.ToMediaColor();

	public Color Disabled
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = disabled.ToMediaColor();
}
