using Avalonia.Media;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.UI.Styles;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="DepthPalette"/>, used for both the read-only-cell and disabled-cell
/// palettes. Property initializers seed from <paramref name="source"/>; <see cref="Build"/> reconstructs
/// the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class DepthPaletteDraft(DepthPalette source) : ReactiveObject
{
	public Color Depth0
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Depth0.ToMediaColor();

	public Color Depth1
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Depth1.ToMediaColor();

	public Color Depth2
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Depth2.ToMediaColor();

	public Color Depth3
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Depth3.ToMediaColor();

	public Color Depth0Past
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Depth0Past.ToMediaColor();

	public Color Depth1Past
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Depth1Past.ToMediaColor();

	public Color Depth2Past
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Depth2Past.ToMediaColor();

	public Color Depth3Past
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Depth3Past.ToMediaColor();

	public Color Selected
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Selected.ToMediaColor();

	public Color Foreground
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.Foreground.ToMediaColor();

	public DepthPalette Build()
	{
		return new DepthPalette(
			Depth0: Depth0.ToStyleColor(),
			Depth1: Depth1.ToStyleColor(),
			Depth2: Depth2.ToStyleColor(),
			Depth3: Depth3.ToStyleColor(),
			Depth0Past: Depth0Past.ToStyleColor(),
			Depth1Past: Depth1Past.ToStyleColor(),
			Depth2Past: Depth2Past.ToStyleColor(),
			Depth3Past: Depth3Past.ToStyleColor(),
			Selected: Selected.ToStyleColor(),
			Foreground: Foreground.ToStyleColor());
	}
}
