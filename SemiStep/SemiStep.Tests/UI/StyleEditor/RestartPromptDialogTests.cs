using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

using FluentAssertions;

using SemiStep.UI.StyleEditor;

using Xunit;

namespace SemiStep.Tests.UI.StyleEditor;

[Trait("Component", "UI")]
[Trait("Area", "GridStyleEditor")]
[Trait("Category", "Unit")]
public sealed class RestartPromptDialogTests
{
	[AvaloniaFact]
	public async Task ExitNow_Click_ResolvesToTrue()
	{
		var owner = new Window();
		owner.Show();
		Dispatcher.UIThread.RunJobs();

		var dialog = new RestartPromptDialog();
		var resultTask = dialog.ShowDialog<bool>(owner);
		Dispatcher.UIThread.RunJobs();

		dialog.FindControl<Button>("ExitNowButton")!
			.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

		var result = await resultTask;

		result.Should().BeTrue("Exit Now must signal the caller to route an exit");

		owner.Close();
	}

	[AvaloniaFact]
	public async Task RestartLater_Click_ResolvesToFalse()
	{
		var owner = new Window();
		owner.Show();
		Dispatcher.UIThread.RunJobs();

		var dialog = new RestartPromptDialog();
		var resultTask = dialog.ShowDialog<bool>(owner);
		Dispatcher.UIThread.RunJobs();

		dialog.FindControl<Button>("RestartLaterButton")!
			.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

		var result = await resultTask;

		result.Should().BeFalse("Restart Later must not route an exit");

		owner.Close();
	}

	[AvaloniaFact]
	public async Task Dismissal_WithNoResult_ResolvesToFalse()
	{
		var owner = new Window();
		owner.Show();
		Dispatcher.UIThread.RunJobs();

		var dialog = new RestartPromptDialog();
		var resultTask = dialog.ShowDialog<bool>(owner);
		Dispatcher.UIThread.RunJobs();

		// Mirrors a title-bar X / programmatic no-result dismissal: closed without a decision.
		// The Escape key path (IsCancel="True" -> Close(false)) is covered by
		// Escape_RoutesThroughRestartLater_ResolvesToFalse.
		dialog.Close();

		var result = await resultTask;

		result.Should().Be(default, "a dismissed restart prompt must never trigger an exit");

		owner.Close();
	}

	[AvaloniaFact]
	public async Task Escape_RoutesThroughRestartLater_ResolvesToFalse()
	{
		var owner = new Window();
		owner.Show();
		Dispatcher.UIThread.RunJobs();

		var dialog = new RestartPromptDialog();
		var resultTask = dialog.ShowDialog<bool>(owner);
		Dispatcher.UIThread.RunJobs();

		// Escape KeyDown alone routes through the IsCancel button to Close(false);
		// the dialog is disposed by the time a release would be delivered.
		dialog.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		var result = await resultTask;

		result.Should().BeFalse("Escape triggers the IsCancel button and must not route an exit");

		owner.Close();
	}
}
