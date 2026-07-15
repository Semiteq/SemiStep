using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Threading;

using FluentAssertions;

using Xunit;
using Xunit.Sdk;

namespace SemiStep.Tests.UI.Helpers;

[Trait("Component", "UI")]
[Trait("Area", "Logging")]
[Trait("Category", "Integration")]
public sealed class BindingErrorGuardTests
{
	[AvaloniaFact]
	public void CapturesBindingError_WhenAncestorLookupFails()
	{
		using var guard = new BindingErrorGuard();

		var window = ShowControlWithMissingAncestorBinding();
		try
		{
			guard.BindingErrorCount.Should().BeGreaterThanOrEqualTo(1);
			guard.BindingErrors.Should().Contain(message => message.Contains("Ancestor not found"));

			var assert = () => guard.AssertNoBindingErrors();
			assert.Should().Throw<XunitException>();
		}
		finally
		{
			window.Close();
		}
	}

	[AvaloniaFact]
	public void CapturesZeroAndRestoresPreviousSink_WhenNoBindingFails()
	{
		var previousSink = Logger.Sink;

		using (var guard = new BindingErrorGuard())
		{
			var window = ShowControlWithoutFailingBinding();
			try
			{
				guard.BindingErrorCount.Should().Be(0);

				var assert = () => guard.AssertNoBindingErrors();
				assert.Should().NotThrow();
			}
			finally
			{
				window.Close();
			}
		}

		Logger.Sink.Should().BeSameAs(previousSink);
	}

	private static Window ShowControlWithMissingAncestorBinding()
	{
		var textBlock = new TextBlock();
		textBlock.Bind(
			TextBlock.TextProperty,
			new Binding(nameof(ListBoxItem.IsSelected))
			{
				RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
				{
					AncestorType = typeof(ListBoxItem),
				},
			});

		return ShowInWindow(new Border { Child = textBlock });
	}

	private static Window ShowControlWithoutFailingBinding()
	{
		var textBlock = new TextBlock { Text = "static" };
		return ShowInWindow(new Border { Child = textBlock });
	}

	private static Window ShowInWindow(Control content)
	{
		var window = new Window
		{
			Width = 200,
			Height = 200,
			Content = content,
		};

		window.Show();
		Dispatcher.UIThread.RunJobs();

		return window;
	}
}
