using System;
using System.Reactive;
using System.Reactive.Linq;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using ReactiveUI;

using SemiStep.UI.Reactive;

using Xunit;

namespace SemiStep.Tests.UI.Reactive;

[Trait("Component", "UI")]
[Trait("Area", "CommandInvocation")]
[Trait("Category", "Unit")]
public sealed class CommandInvocationExtensionsTests
{
	[AvaloniaFact]
	public void ExecuteIfPossible_ThrowingCommand_DoesNotRethrowAndFiresThrownExceptionsOnce()
	{
		var failure = new InvalidOperationException("boom");
		var command = ReactiveCommand.Create<Unit, Unit>(_ => throw failure);

		try
		{
			var thrown = 0;
			Exception? captured = null;
			using var subscription = command.ThrownExceptions.Subscribe(ex =>
			{
				thrown++;
				captured = ex;
			});

			var invoke = () => command.ExecuteIfPossible();

			invoke.Should().NotThrow();
			thrown.Should().Be(1);
			captured.Should().BeSameAs(failure);
		}
		finally
		{
			command.Dispose();
		}
	}

	[AvaloniaFact]
	public void ExecuteIfPossible_CanExecuteFalse_DoesNotRunCommandBody()
	{
		var executions = 0;
		var command = ReactiveCommand.Create<Unit, Unit>(
			_ =>
			{
				executions++;
				return Unit.Default;
			},
			Observable.Return(false));

		try
		{
			command.ExecuteIfPossible();

			executions.Should().Be(0);
		}
		finally
		{
			command.Dispose();
		}
	}
}
