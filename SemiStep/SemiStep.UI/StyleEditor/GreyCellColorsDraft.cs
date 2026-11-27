using SemiStep.Core.Configuration;

namespace SemiStep.UI.StyleEditor;

/// <summary>Editable draft of the read-only and inapplicable cell bases (<see cref="GridStyleOptions.ReadOnlyCell"/>, <see cref="GridStyleOptions.DisabledCell"/>).</summary>
public sealed class GreyCellColorsDraft(StyleColor readOnly, StyleColor disabled)
{
	public ColorDraft ReadOnly { get; } = new(readOnly);

	public ColorDraft Disabled { get; } = new(disabled);
}
