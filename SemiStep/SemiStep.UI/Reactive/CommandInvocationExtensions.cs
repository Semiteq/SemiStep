using System;
using System.Reactive;
using System.Windows.Input;

using ReactiveUI;

namespace SemiStep.UI.Reactive;

public static class CommandInvocationExtensions
{
	// ICommand.Execute on a ReactiveCommand runs Execute().Catch(Empty).Subscribe(), so it never rethrows
	// on the caller thread (the exception still reaches ThrownExceptions); it also honors canExecute (which
	// ReactiveCommand.Execute() ignores); and because ICommand.CanExecute is false while a command executes,
	// it suppresses re-entrant hotkey mashes during an async command.
	public static void ExecuteIfPossible<TResult>(this ReactiveCommand<Unit, TResult> command)
	{
		ArgumentNullException.ThrowIfNull(command);

		if (((ICommand)command).CanExecute(null))
		{
			((ICommand)command).Execute(null);
		}
	}
}
