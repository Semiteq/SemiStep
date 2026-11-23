using System.Globalization;
using System.Net;
using System.Net.Sockets;

using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using AwesomeAssertions;

using FluentResults;

using Microsoft.Extensions.Time.Testing;

using SemiStep.Tests.Helpers;
using SemiStep.Tools.PlcEmulator;

using Xunit;

namespace SemiStep.Tests.Emulator;

[Trait("Component", "S7")]
[Trait("Area", "Emulator")]
[Trait("Category", "Integration")]
public sealed class EmulatorStartupTests
{
	private static readonly TimeSpan _waitLimit = TimeSpan.FromSeconds(10);

	[Fact]
	public async Task Start_MissingConfigDir_Fails()
	{
		var result = await StartAsync("--config-dir", Path.Combine(Path.GetTempPath(), "missing-emulator-config"));

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public async Task Start_BusyPort_Fails()
	{
		var port = EmulatorTestSupport.FreePort();
		var occupant = new TcpListener(IPAddress.Loopback, port);
		occupant.Start();
		try
		{
			var configDir = ShippedConfigLocator.GetConfigDirectory("MBE");
			var result = await StartAsync("--config-dir", configDir, "--port", Text(port));

			result.IsFailed.Should().BeTrue();
		}
		finally
		{
			occupant.Stop();
		}
	}

	[Theory]
	[InlineData]
	[InlineData("--config-dir")]
	[InlineData("--config-dir", "ConfigFiles/MBE", "--port", "0")]
	public async Task Start_UnparsableOptions_Fails(params string[] arguments)
	{
		var result = await StartAsync(arguments);

		result.IsFailed.Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task Dispose_AfterTicking_StopsTheTickAndTheRefreshAndFreesThePort()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		var port = EmulatorTestSupport.FreePort();
		var time = new FakeTimeProvider();
		var configDir = ShippedConfigLocator.GetConfigDirectory("MBE");
		var started = await EmulatorStartup.StartAsync(["--config-dir", configDir, "--port", Text(port)], time);
		started.IsSuccess.Should().BeTrue();
		using (var host = started.Value)
		{
			var viewModel = host.CreateViewModel();
			viewModel.SelectedBlock = viewModel.Blocks[0];
			viewModel.Edit(viewModel.SelectedBlock.Rows.Single(row => row.Name == "committed"), "TRUE");
			var edited = viewModel.Message;
			host.StartTicking();

			await TestHelpers.WaitUntilAsync(
				() =>
				{
					time.Advance(RunEngine.TickInterval);
					Dispatcher.UIThread.RunJobs();
					return viewModel.Message != edited;
				},
				_waitLimit,
				TestHelpers.DefaultPollInterval,
				cancellationToken);
		}

		time.Advance(RunEngine.TickInterval);
		Dispatcher.UIThread.RunJobs();
		var configuration = await ShippedConfigLocator.LoadAsync("MBE");
		using var second = EmulatorTestSupport.StartOrFail(configuration, port);
	}

	private static Task<Result<EmulatorHost>> StartAsync(params string[] arguments)
	{
		return EmulatorStartup.StartAsync(arguments, TimeProvider.System);
	}

	private static string Text(int number)
	{
		return number.ToString(CultureInfo.InvariantCulture);
	}
}
