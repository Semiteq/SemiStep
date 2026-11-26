using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SemiStep.Tests.UI.Helpers;

/// <summary>Headless typing that drops the character of a handled KeyDown, as Avalonia.Win32 drops its WM_CHAR.</summary>
public static class HeadlessTyping
{
	public static void TypeKey(this TopLevel topLevel, PhysicalKey physicalKey, string text)
	{
		var keyDownHandled = false;

		void OnKeyDown(object? sender, KeyEventArgs e)
		{
			keyDownHandled = e.Handled;
		}

		topLevel.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
		try
		{
			topLevel.KeyPressQwerty(physicalKey, RawInputModifiers.None);
		}
		finally
		{
			topLevel.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
		}

		if (!keyDownHandled)
		{
			topLevel.KeyTextInput(text);
		}
	}
}
