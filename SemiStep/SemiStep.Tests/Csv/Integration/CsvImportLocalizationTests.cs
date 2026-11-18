using FluentAssertions;

using SemiStep.Core.Recipes.Errors;
using SemiStep.Tests.Csv.Helpers;

using Xunit;

namespace SemiStep.Tests.Csv.Integration;

[Trait("Category", "Integration")]
[Trait("Component", "Csv")]
[Trait("Area", "Localization")]
public sealed class CsvImportLocalizationTests(CsvFixture fixture) : IClassFixture<CsvFixture>
{
	private const string OutOfRangeCsv = "action;step_duration;task;comment\n10;100000;0;ok\n";

	[Fact]
	public void Deserialize_OutOfRangeCell_FailsWithRowColumnValueChain()
	{
		var result = fixture.FileSerializer.Deserialize(OutOfRangeCsv);

		result.IsFailed.Should().BeTrue();

		var row = result.Errors.OfType<AtRowError>().Should().ContainSingle().Subject;
		row.RowNumber.Should().Be(2);

		var column = row.Inner.Should().BeOfType<AtColumnError>().Subject;
		column.ColumnKey.Should().Be("step_duration");

		var value = column.Inner.Should().BeOfType<ValueAboveMaximumError>().Subject;
		value.Value.Should().Be(100000);
		value.Max.Should().Be(86400);
	}
}
