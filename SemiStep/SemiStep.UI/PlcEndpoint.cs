using System.Globalization;

using FluentResults;

using SemiStep.Core.Configuration;

namespace SemiStep.UI;

public sealed record PlcEndpoint(string Host, int Port)
{
	private const int MaximumPort = 65535;

	public static Result<PlcEndpoint> Parse(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return Result.Fail("no value");
		}

		var separator = value.LastIndexOf(':');
		if (separator < 0)
		{
			return Result.Fail($"'{value}' has no ':port'");
		}

		var host = value[..separator].Trim();
		if (host.Length == 0)
		{
			return Result.Fail($"'{value}' has no host before ':'");
		}

		var portText = value[(separator + 1)..];
		var isPortValid = int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
			&& port is >= 1 and <= MaximumPort;

		return isPortValid
			? Result.Ok(new PlcEndpoint(host, port))
			: Result.Fail($"'{value}' has port '{portText}', not a whole number from 1 to {MaximumPort}");
	}

	public AppConfiguration ApplyTo(AppConfiguration configuration)
	{
		var plc = configuration.PlcConfiguration;

		return configuration with
		{
			PlcConfiguration = plc with
			{
				Connection = plc.Connection with { IpAddress = Host, Port = Port }
			}
		};
	}
}
