using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Plc.Configuration;
using SemiStep.Tests.Helpers;
using SemiStep.UI;

using Xunit;

namespace SemiStep.Tests.UI.Startup;

[Trait("Component", "UI")]
[Trait("Area", "Startup")]
[Trait("Category", "Unit")]
public sealed class StartupOptionsPlcEndpointTests
{
	[Fact]
	public void ValidEndpoint_IsParsed()
	{
		var options = StartupOptions.Parse(["--plc-endpoint", "127.0.0.1:1102"]);

		options.PlcEndpointOverride!.Value.Should().Be(new PlcEndpoint("127.0.0.1", 1102));
	}

	[Fact]
	public void ValidEndpoint_InAssignmentForm_IsParsed()
	{
		var options = StartupOptions.Parse(["--plc-endpoint=127.0.0.1:10102"]);

		options.PlcEndpointOverride!.Value.Should().Be(new PlcEndpoint("127.0.0.1", 10102));
	}

	[Theory]
	[InlineData("--plc-endpoint=garbage")]
	[InlineData("--plc-endpoint=")]
	public void MalformedEndpoint_InAssignmentForm_IsAnError(string argument)
	{
		var options = StartupOptions.Parse([argument]);

		options.PlcEndpointOverride!.IsFailed.Should().BeTrue();
	}

	[Fact]
	public async Task LoadConfiguration_MalformedEndpointInAssignmentForm_FailsBeforeLoading()
	{
		var options = StartupOptions.Parse(["--config-dir", "no-such-config-dir", "--plc-endpoint=garbage"]);

		var result = await Program.LoadConfigurationAsync(options);

		result.Errors.Should().Equal(options.PlcEndpointOverride!.Errors);
	}

	[Fact]
	public void Absent_LeavesNoOverride()
	{
		var options = StartupOptions.Parse(["--config-dir", "x"]);

		options.PlcEndpointOverride.Should().BeNull();
	}

	[Theory]
	[InlineData("127.0.0.1")]
	[InlineData("127.0.0.1:")]
	[InlineData(":102")]
	[InlineData("127.0.0.1:abc")]
	[InlineData("127.0.0.1:0")]
	[InlineData("127.0.0.1:65536")]
	[InlineData("127.0.0.1:-1")]
	[InlineData("")]
	public void MalformedEndpoint_IsAnError(string value)
	{
		var options = StartupOptions.Parse(["--plc-endpoint", value]);

		options.PlcEndpointOverride!.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void MissingValue_IsAnError()
	{
		var options = StartupOptions.Parse(["--plc-endpoint"]);

		options.PlcEndpointOverride!.IsFailed.Should().BeTrue();
	}

	[Fact]
	public async Task LoadConfiguration_MalformedEndpoint_FailsWithTheOptionErrorBeforeLoading()
	{
		var options = StartupOptions.Parse(["--config-dir", "no-such-config-dir", "--plc-endpoint", "10.1.2.3"]);

		var result = await Program.LoadConfigurationAsync(options);

		result.Errors.Should().Equal(options.PlcEndpointOverride!.Errors);
	}

	[Fact]
	public async Task LoadConfiguration_ValidEndpoint_ReachesTheRegisteredPlcConfiguration()
	{
		var loaded = await ShippedConfigLocator.LoadAsync("RIE");
		var original = loaded.PlcConfiguration.Connection;
		var options = StartupOptions.Parse(
			["--config-dir", ShippedConfigLocator.GetConfigDirectory("RIE"), "--plc-endpoint", "10.1.2.3:1102"]);

		var result = await Program.LoadConfigurationAsync(options);
		using var provider = Program.BuildServices(options, result.Value);
		var registered = provider.GetRequiredService<PlcConfiguration>();

		registered.Connection.IpAddress.Should().Be("10.1.2.3");
		registered.Connection.Port.Should().Be(1102);
		registered.Connection.Rack.Should().Be(original.Rack);
		registered.Connection.Slot.Should().Be(original.Slot);
		registered.Layout.Should().Be(loaded.PlcConfiguration.Layout);
	}

	[Fact]
	public async Task LoadConfiguration_WithoutOverride_RegistersTheLoadedPlcConfiguration()
	{
		var loaded = await ShippedConfigLocator.LoadAsync("RIE");
		var options = StartupOptions.Parse(["--config-dir", ShippedConfigLocator.GetConfigDirectory("RIE")]);

		var result = await Program.LoadConfigurationAsync(options);
		using var provider = Program.BuildServices(options, result.Value);

		provider.GetRequiredService<PlcConfiguration>().Should().Be(loaded.PlcConfiguration);
	}

	[Fact]
	[Trait("Area", "StatusBar")]
	public async Task LoadConfiguration_FlagsTheOverride_OnlyWhenItDiffersFromThePreset()
	{
		var preset = (await ShippedConfigLocator.LoadAsync("RIE")).PlcConfiguration.Connection;
		var configDir = ShippedConfigLocator.GetConfigDirectory("RIE");

		var differing = await Program.LoadConfigurationAsync(
			StartupOptions.Parse(["--config-dir", configDir, "--plc-endpoint", "10.1.2.3:1102"]));
		var same = await Program.LoadConfigurationAsync(
			StartupOptions.Parse(["--config-dir", configDir, "--plc-endpoint", $"{preset.IpAddress}:{preset.Port}"]));
		var none = await Program.LoadConfigurationAsync(StartupOptions.Parse(["--config-dir", configDir]));

		differing.Value.LinkAddress.Should().Be(new PlcLinkAddress("10.1.2.3", 1102, IsOverride: true));
		same.Value.LinkAddress.IsOverride.Should().BeFalse();
		none.Value.LinkAddress.Should().Be(new PlcLinkAddress(preset.IpAddress, preset.Port, IsOverride: false));
	}
}
