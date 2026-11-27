using SemiStep.Core.Configuration;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="ValidationPanelStyle"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class ValidationPanelStyleDraft(ValidationPanelStyle source)
{
	public ColorDraft Background { get; } = new(source.Background);

	public ColorDraft Foreground { get; } = new(source.Foreground);

	public ColorDraft ErrorColor { get; } = new(source.ErrorColor);

	public ColorDraft WarningColor { get; } = new(source.WarningColor);

	public NumberDraft MaxHeight { get; } =
		new((decimal)source.MaxHeight, DraftRanges.MinPanelMaxHeight, DraftRanges.MaxPanelMaxHeight);

	public ValidationPanelStyle Build()
	{
		return new ValidationPanelStyle(
			Background: Background.ToStyleColor(),
			Foreground: Foreground.ToStyleColor(),
			ErrorColor: ErrorColor.ToStyleColor(),
			WarningColor: WarningColor.ToStyleColor(),
			MaxHeight: MaxHeight.ToDouble());
	}
}
