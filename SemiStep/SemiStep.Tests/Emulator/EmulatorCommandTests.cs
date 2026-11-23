using AwesomeAssertions;

using SemiStep.Tools.PlcEmulator;

using Xunit;

namespace SemiStep.Tests.Emulator;

[Trait("Component", "S7")]
[Trait("Area", "Emulator")]
[Trait("Category", "Unit")]
public sealed class EmulatorCommandTests
{
	[Fact]
	public void Parse_OnlyConfigDir_UsesTheCapacityRuleDefaults()
	{
		var options = Parse("--config-dir", "ConfigFiles/RIE");

		options.Should().Be(new EmulatorOptions("ConfigFiles/RIE", 102, 3000, 6000, 900));
	}

	[Fact]
	public void Parse_CapacityOptions_OverrideTheDefaults()
	{
		var options = Parse(
			"--config-dir", "ConfigFiles/RIE", "--port", "1102",
			"--int-capacity", "10", "--float-capacity", "20", "--string-capacity", "30");

		options.Should().Be(new EmulatorOptions("ConfigFiles/RIE", 1102, 10, 20, 30));
	}

	[Theory]
	[InlineData("--port", "0")]
	[InlineData("--port", "65536")]
	[InlineData("--int-capacity", "0")]
	[InlineData("--float-capacity", "-1")]
	[InlineData("--string-capacity", "0")]
	public void Parse_NumberOutOfRange_Fails(string option, string value)
	{
		EmulatorCommand.Parse(["--config-dir", "ConfigFiles/RIE", option, value]).IsFailed.Should().BeTrue();
	}

	[Fact]
	public void Parse_NoConfigDir_Fails()
	{
		EmulatorCommand.Parse([]).IsFailed.Should().BeTrue();
	}

	private static EmulatorOptions Parse(params string[] arguments)
	{
		return EmulatorCommand.Parse(arguments).Value;
	}
}
