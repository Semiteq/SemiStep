using Avalonia.Media;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.UI.Styles;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Editable draft of <see cref="ValidationPanelStyle"/>. Property initializers seed from <paramref name="source"/>;
/// <see cref="Build"/> reconstructs the record positionally so an omitted component is a compile error.
/// </summary>
public sealed class ValidationPanelStyleDraft(ValidationPanelStyle source) : ReactiveObject
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

	public Color ErrorColor
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.ErrorColor.ToMediaColor();

	public Color WarningColor
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = source.WarningColor.ToMediaColor();

	public decimal? MaxHeight
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	} = (decimal)source.MaxHeight;

	public ValidationPanelStyle Build()
	{
		return new ValidationPanelStyle(
			Background: Background.ToStyleColor(),
			Foreground: Foreground.ToStyleColor(),
			ErrorColor: ErrorColor.ToStyleColor(),
			WarningColor: WarningColor.ToStyleColor(),
			MaxHeight: DraftNumbers.ToDouble(MaxHeight));
	}
}
