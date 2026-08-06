using Avalonia.Media;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.UI.Styles;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="ChromeColors"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class ChromeColorsDraft(ChromeColors source) : ReactiveObject
{
	public Color Info
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Info.ToMediaColor();

	public Color Connected
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Connected.ToMediaColor();

	public Color Disconnected
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Disconnected.ToMediaColor();

	public Color LocalMode
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.LocalMode.ToMediaColor();

	public Color Connecting
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Connecting.ToMediaColor();

	public Color PanelBackground
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.PanelBackground.ToMediaColor();

	public Color PanelHeaderBackground
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.PanelHeaderBackground.ToMediaColor();

	public Color SubtleBorder
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.SubtleBorder.ToMediaColor();

	public Color Separator
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Separator.ToMediaColor();

	public Color SecondaryForeground
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.SecondaryForeground.ToMediaColor();

	public Color GridBorder
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.GridBorder.ToMediaColor();

	public Color GridBackground
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.GridBackground.ToMediaColor();

	public Color HeaderForeground
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.HeaderForeground.ToMediaColor();

	public Color GridLine
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.GridLine.ToMediaColor();

	public ChromeColors Build()
	{
		return new ChromeColors(
			Info: Info.ToStyleColor(),
			Connected: Connected.ToStyleColor(),
			Disconnected: Disconnected.ToStyleColor(),
			LocalMode: LocalMode.ToStyleColor(),
			Connecting: Connecting.ToStyleColor(),
			PanelBackground: PanelBackground.ToStyleColor(),
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
