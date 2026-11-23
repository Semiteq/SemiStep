using System.CommandLine;

using FluentResults;

namespace SemiStep.Tools.PlcEmulator;

internal static class EmulatorCommand
{
	private const int DefaultPort = 102;
	private const int DefaultIntCapacity = 3000;
	private const int DefaultFloatCapacity = 6000;
	private const int DefaultStringCapacity = 900;
	private const int MaximumPort = 65535;

	private static readonly Option<string> _configDir = new("--config-dir")
	{
		Description = "Preset folder the app loads, for example ConfigFiles/MBE.",
		Required = true,
	};

	private static readonly Option<int> _port = WholeNumber(
		"--port", "TCP port to listen on, from 1 to 65535.", DefaultPort, MaximumPort);

	private static readonly Option<int> _intCapacity = WholeNumber(
		"--int-capacity", "Number of ints the integer data DB holds.", DefaultIntCapacity, int.MaxValue);

	private static readonly Option<int> _floatCapacity = WholeNumber(
		"--float-capacity", "Number of floats the float data DB holds.", DefaultFloatCapacity, int.MaxValue);

	private static readonly Option<int> _stringCapacity = WholeNumber(
		"--string-capacity",
		"Number of strings the string data DB holds, each of the configured string length.",
		DefaultStringCapacity,
		int.MaxValue);

	public static Result<EmulatorOptions> Parse(IReadOnlyList<string> arguments)
	{
		var root = new RootCommand("Emulates the PLC SemiStep talks to over the S7 protocol.")
		{
			_configDir, _port, _intCapacity, _floatCapacity, _stringCapacity,
		};
		var parsed = root.Parse(arguments);
		if (parsed.Errors.Count > 0)
		{
			return Result.Fail(parsed.Errors.Select(error => new Error(error.Message)));
		}

		return Result.Ok(From(parsed));
	}

	private static EmulatorOptions From(ParseResult parsed)
	{
		return new EmulatorOptions(
			parsed.GetRequiredValue(_configDir),
			parsed.GetValue(_port),
			parsed.GetValue(_intCapacity),
			parsed.GetValue(_floatCapacity),
			parsed.GetValue(_stringCapacity));
	}

	private static Option<int> WholeNumber(string name, string description, int defaultValue, int maximum)
	{
		var option = new Option<int>(name)
		{
			Description = description,
			DefaultValueFactory = _ => defaultValue,
		};
		option.Validators.Add(result =>
		{
			var value = result.GetValueOrDefault<int>();
			if (value < 1 || value > maximum)
			{
				result.AddError($"Option '{name}' expects a whole number from 1 to {maximum}, got {value}.");
			}
		});
		return option;
	}
}
