using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SemiStep.UI.StyleEditor;

/// <summary>A colour field: one stock picker over a <see cref="ColorDraft"/> that writes the pick back on close.</summary>
internal partial class ColorFieldView : UserControl
{
	private FlyoutBase? _flyout;
	private ColorDraft? _draftAtOpen;
	private Color _colorAtOpen;

	public ColorFieldView()
	{
		InitializeComponent();
	}

	protected override void OnLoaded(RoutedEventArgs e)
	{
		base.OnLoaded(e);

		Picker.TemplateApplied += OnPickerTemplateApplied;
		AttachFlyout();
	}

	protected override void OnUnloaded(RoutedEventArgs e)
	{
		Picker.TemplateApplied -= OnPickerTemplateApplied;
		DetachFlyout();

		base.OnUnloaded(e);
	}

	// A picker on a hidden page applies its template only when the page first shows.
	private void OnPickerTemplateApplied(object? sender, TemplateAppliedEventArgs e)
	{
		AttachFlyout();
	}

	private void AttachFlyout()
	{
		DetachFlyout();

		_flyout = Picker.GetVisualDescendants().OfType<DropDownButton>().FirstOrDefault()?.Flyout;
		if (_flyout is not null)
		{
			_flyout.Opened += OnFlyoutOpened;
			_flyout.Closed += OnFlyoutClosed;
		}
	}

	private void DetachFlyout()
	{
		if (_flyout is not null)
		{
			_flyout.Opened -= OnFlyoutOpened;
			_flyout.Closed -= OnFlyoutClosed;
			_flyout = null;
		}
	}

	// The picker only reads the draft, so opening and dismissing the flyout writes nothing.
	private void OnFlyoutOpened(object? sender, EventArgs e)
	{
		_draftAtOpen = DataContext as ColorDraft;
		_colorAtOpen = Picker.Color;
	}

	private void OnFlyoutClosed(object? sender, EventArgs e)
	{
		var draft = _draftAtOpen;
		_draftAtOpen = null;

		if (draft is not null && Picker.Color != _colorAtOpen)
		{
			draft.Pick(Picker.Color);
		}
	}
}
