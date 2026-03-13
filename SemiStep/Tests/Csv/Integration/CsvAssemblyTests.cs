using System.Collections.Immutable;

using FluentAssertions;

using FluentResults;

using Tests.Csv.Helpers;

using Xunit;

namespace Tests.Csv.Integration;

[Trait("Category", "Integration")]
[Trait("Component", "Csv")]
[Trait("Area", "CsvAssembly")]
public sealed class CsvAssemblyTests(CsvFixture fixture) : IClassFixture<CsvFixture>
{
	[Fact]
	public void Deserialize_FullyApplicableRow_NoErrors()
	{
		var csv = "action;step_duration;task;comment\n10;5;0;hello\n";
		var result = fixture.FileSerializer.Deserialize(csv);

		result.IsSuccess.Should().BeTrue();
		result.Value.StepCount.Should().Be(1);
	}

	[Fact]
	public void Deserialize_NonApplicableColumnEmpty_NoErrors()
	{
		var csv = "action;step_duration;task;comment\n30;;;test comment\n";
		var result = fixture.FileSerializer.Deserialize(csv);

		result.IsSuccess.Should().BeTrue();
		result.Value.StepCount.Should().Be(1);
		result.Value.Steps[0].ActionKey.Should().Be(30);
	}

	[Fact]
	public void Deserialize_MixedRows_AssemblesAllSteps()
	{
		var csv = "action;step_duration;task;comment\n" +
				  "10;5;0;wait step\n" +
				  "20;0;3;for loop\n" +
				  "10;10;0;inner wait\n" +
				  "30;;;end for\n" +
				  "40;;;pause\n";

		var result = fixture.FileSerializer.Deserialize(csv);

		result.IsSuccess.Should().BeTrue();
		result.Value.StepCount.Should().Be(5);
	}
}
