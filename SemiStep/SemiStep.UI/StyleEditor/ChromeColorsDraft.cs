using SemiStep.Core.Configuration;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="ChromeColors"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// <see cref="Info"/> and <see cref="PanelBackground"/> have no row in the editor and keep their seeded values.
/// </summary>
public sealed class ChromeColorsDraft(ChromeColors source)
{
	public StyleColor Info { get; } = source.Info;

	public ColorDraft Connected { get; } = new(source.Connected);

	public ColorDraft Disconnected { get; } = new(source.Disconnected);

	public StyleColor PanelBackground { get; } = source.PanelBackground;

	public ColorDraft PanelHeaderBackground { get; } = new(source.PanelHeaderBackground);

	public ColorDraft SubtleBorder { get; } = new(source.SubtleBorder);

	public ColorDraft Separator { get; } = new(source.Separator);

	public ColorDraft SecondaryForeground { get; } = new(source.SecondaryForeground);

	public ColorDraft GridBorder { get; } = new(source.GridBorder);

	public ColorDraft GridBackground { get; } = new(source.GridBackground);

	public ColorDraft HeaderForeground { get; } = new(source.HeaderForeground);

	public ColorDraft GridLine { get; } = new(source.GridLine);

	public ChromeColors Build()
	{
		return new ChromeColors(
			Info: Info,
			Connected: Connected.ToStyleColor(),
			Disconnected: Disconnected.ToStyleColor(),
			PanelBackground: PanelBackground,
			PanelHeaderBackground: PanelHeaderBackground.ToStyleColor(),
			SubtleBorder: SubtleBorder.ToStyleColor(),
			Separator: Separator.ToStyleColor(),
			SecondaryForeground: SecondaryForeground.ToStyleColor(),
			GridBorder: GridBorder.ToStyleColor(),
			GridBackground: GridBackground.ToStyleColor(),
			HeaderForeground: HeaderForeground.ToStyleColor(),
			GridLine: GridLine.ToStyleColor());
	}
}
