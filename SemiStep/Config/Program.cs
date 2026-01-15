using Config.Core;

using Serilog;
using Serilog.Events;

namespace Config;

class Program
{
	static async Task Main(string[] args)
	{
		var logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.WriteTo.Console()
			.CreateLogger();

		var loader = new ConfigLoaderBuilder()
			.UseYaml()
			.WithLogger(logger)
			.AddExceptionHandling()
			.AddLogging(LogEventLevel.Debug)
			.AddMerge()
			.AddValidation()
			.Build();

		var context = await loader.LoadAsync("config.yaml");

		if (context.HasErrors)
		{
			Console.WriteLine($"\nConfiguration loading failed with {context.Errors.Count} error(s):\n");

			foreach (var error in context.Errors)
			{
				Console.WriteLine($"  [{error.Severity}] {error.Message}");
				if (error.Location != null)
				{
					Console.WriteLine($"    at {error.Location}");
				}
			}
		}
		else
		{
			Console.WriteLine("\nConfiguration loaded successfully!");

			if (context.ParsedConfig != null)
			{
				Console.WriteLine($"   Actions: {context.ParsedConfig.Actions.Count}");

				foreach (var action in context.ParsedConfig.Actions)
				{
					Console.WriteLine($"     - {action.Key} (ID: {action.InternalId}, Properties: {action.Properties.Count})");
				}
			}
		}
	}
}
