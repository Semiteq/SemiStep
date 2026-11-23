using FluentResults;

using SemiStep.Core.Configuration.Facade;

namespace SemiStep.Tools.PlcEmulator;

internal static class EmulatorStartup
{
	public static async Task<Result<EmulatorHost>> StartAsync(
		IReadOnlyList<string> arguments,
		TimeProvider timeProvider)
	{
		var parsed = EmulatorCommand.Parse(arguments);
		if (parsed.IsFailed)
		{
			return parsed.ToResult<EmulatorHost>();
		}

		var options = parsed.Value;
		var loaded = await ConfigFacade.LoadAndValidateAsync(options.ConfigDir);
		if (loaded.IsFailed)
		{
			var reasons = string.Join("; ", loaded.Errors.Select(error => error.Message));
			return Result.Fail<EmulatorHost>($"Cannot load '{options.ConfigDir}': {reasons}");
		}

		var presetName = Path.GetFileName(Path.TrimEndingDirectorySeparator(options.ConfigDir));
		return EmulatorHost.Start(loaded.Value, options, presetName, timeProvider);
	}
}
