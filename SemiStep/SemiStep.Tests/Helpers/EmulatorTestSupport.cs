using System.Net;
using System.Net.Sockets;

using AwesomeAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Tools.PlcEmulator;

namespace SemiStep.Tests.Helpers;

internal static class EmulatorTestSupport
{
	public const int IntCapacity = 100;
	public const int FloatCapacity = 200;
	public const int StringCapacity = 10;

	public static EmulatorOptions OptionsFor(int port)
	{
		return new EmulatorOptions("unused", port, IntCapacity, FloatCapacity, StringCapacity);
	}

	public static EmulatedPlc StartOrFail(AppConfiguration configuration, int port)
	{
		var started = EmulatedPlc.Start(configuration, OptionsFor(port));
		started.IsSuccess.Should().BeTrue();
		return started.Value;
	}

	public static int FreePort()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		try
		{
			return ((IPEndPoint)listener.LocalEndpoint).Port;
		}
		finally
		{
			listener.Stop();
		}
	}
}
