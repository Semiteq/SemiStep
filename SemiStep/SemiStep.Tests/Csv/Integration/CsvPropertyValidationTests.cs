using FluentAssertions;

using SemiStep.Tests.Csv.Helpers;

using Xunit;

namespace SemiStep.Tests.Csv.Integration;

[Trait("Category", "Integration")]
[Trait("Component", "Csv")]
[Trait("Area", "PropertyValidation")]
public sealed class CsvPropertyValidationTests(CsvFixture fixture) : IClassFixture<CsvFixture>
{
	[Fact]
	public void Deserialize_OverLengthString_FailsWithRowAndColumnContext()
	{
		var oversizedComment = new string('x', 300);
		var csv = $"action;step_duration;task;comment\n10;5;0;{oversizedComment}\n";

		var result = fixture.FileSerializer.Deserialize(csv);

		result.IsFailed.Should().BeTrue();
		var aggregated = string.Join(" | ", result.Errors.Select(FlattenMessage));
		aggregated.Should().Contain("Row 2");
		aggregated.Should().Contain("comment");
		aggregated.Should().Contain("exceeds maximum");
	}

	[Fact]
	public void Deserialize_OutOfRangeNumeric_FailsWithRowAndColumnContext()
	{
		var csv = "action;step_duration;task;comment\n10;100000;0;ok\n";

		var result = fixture.FileSerializer.Deserialize(csv);

		result.IsFailed.Should().BeTrue();
		var aggregated = string.Join(" | ", result.Errors.Select(FlattenMessage));
		aggregated.Should().Contain("Row 2");
		aggregated.Should().Contain("step_duration");
		aggregated.Should().Contain("exceeds maximum");
		aggregated.Should().Contain("86400", "the configured upper bound must appear in the diagnostic");
		aggregated.Should().Contain("100000", "the offending value must appear in the diagnostic");
	}

	[Fact]
	public void Deserialize_RowWithMultipleViolations_ReportsBothColumns()
	{
		var oversizedComment = new string('x', 300);
		var csv = $"action;step_duration;task;comment\n10;99999999;0;{oversizedComment}\n";

		var result = fixture.FileSerializer.Deserialize(csv);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().HaveCountGreaterThanOrEqualTo(2,
			"a single CSV row with two distinct column violations must surface as two top-level error chains");

		var flattened = result.Errors.Select(FlattenMessage).ToList();
		flattened.Should().Contain(message => message.Contains("step_duration"));
		flattened.Should().Contain(message => message.Contains("comment"));
	}

	[Fact]
	public void Deserialize_ValidValues_ImportsSuccessfully()
	{
		var csv = "action;step_duration;task;comment\n10;5;0;hello world\n";

		var result = fixture.FileSerializer.Deserialize(csv);

		result.IsSuccess.Should().BeTrue();
		result.Value.StepCount.Should().Be(1);
	}

	private static string FlattenMessage(FluentResults.IError error)
	{
		var parts = new List<string> { error.Message };
		foreach (var reason in error.Reasons)
		{
			parts.Add(FlattenMessage(reason));
		}

		return string.Join(" -> ", parts);
	}
}
