using System;
using System.Reactive;
using System.Threading.Tasks;
#if DEBUG
using System.Diagnostics;
#endif

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ReactiveUI.Builder;

using SemiStep.UI.Localization;
using SemiStep.UI.MessageService;

using SerilogLog = Serilog.Log;

namespace SemiStep.UI.Logging;

internal static class GlobalExceptionBackstop
{
	internal static string RecoverableUserMessage => Resources.UnexpectedErrorMessage;

	// ReactiveUI 23 dropped the settable RxApp.DefaultExceptionHandler; the pipeline handler is configured
	// once through the builder (RxState.InitializeExceptionHandler), which runs before the first
	// ReactiveCommand is built, so the backstop wins the capture. The two OS hooks are runtime events.
	// Install is called exactly once (App.Run is gated by EnsureSingleStart; RunErrorWindow never installs),
	// so no idempotency guard is needed.
	public static void Install(IReactiveUIBuilder builder, IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(provider);

		var logger = ResolveLogger(provider);

		builder.WithExceptionHandler(CreateRecoverableExceptionHandler(provider, logger));

		TaskScheduler.UnobservedTaskException += (_, args) =>
		{
			LogUnobserved(logger, args.Exception);
			args.SetObserved();
		};

		AppDomain.CurrentDomain.UnhandledException += (_, args) =>
		{
			if (args.ExceptionObject is Exception exception)
			{
				LogFatal(logger, exception);
			}
			else
			{
				logger.LogCritical(
					"Unhandled non-exception object is terminating the process: {ExceptionObject}",
					args.ExceptionObject);
			}

			// Flush so the fatal stack reaches the log file before the process dies.
			SerilogLog.CloseAndFlushAsync().GetAwaiter().GetResult();
		};
	}

	internal static IObserver<Exception> CreateRecoverableExceptionHandler(IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);

		return CreateRecoverableExceptionHandler(provider, ResolveLogger(provider));
	}

	private static IObserver<Exception> CreateRecoverableExceptionHandler(IServiceProvider provider, ILogger logger)
	{
		// Resolve the panel lazily at fire time: resolving it eagerly would construct MessagePanelViewModel
		// and its ToggleCommand before the backstop is in place, defeating the purpose.
		return Observer.Create<Exception>(exception =>
		{
			try
			{
				ReportRecoverable(provider.GetRequiredService<MessagePanelViewModel>(), logger, exception);
#if DEBUG
				if (Debugger.IsAttached)
				{
					Debugger.Break();
				}
#endif
			}
			catch (Exception handlerFailure)
			{
				// Terminal handler: a throw here would crash the app and defeat keep-alive.
				logger.LogError(handlerFailure, "The global backstop handler itself threw");
			}
		});
	}

	// GlobalExceptionBackstop is a static type, so ILogger<T> cannot target it; build the same category
	// name through the factory instead.
	private static ILogger ResolveLogger(IServiceProvider provider)
	{
		return provider.GetRequiredService<ILoggerFactory>()
			.CreateLogger(typeof(GlobalExceptionBackstop).FullName!);
	}

	internal static void ReportRecoverable(MessagePanelViewModel panel, ILogger logger, Exception exception)
	{
		ArgumentNullException.ThrowIfNull(panel);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(exception);

		logger.LogError(exception, "Unhandled exception reached the global backstop");
		panel.ReportError(RecoverableUserMessage);
	}

	internal static void LogUnobserved(ILogger logger, Exception exception)
	{
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(exception);

		logger.LogError(exception, "Unobserved task exception reached the global backstop");
	}

	internal static void LogFatal(ILogger logger, Exception exception)
	{
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(exception);

		logger.LogCritical(exception, "Unhandled exception is terminating the process");
	}
}
