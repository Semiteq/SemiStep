using System.Collections.Immutable;

using AwesomeAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard.Errors;
using SemiStep.Core.Recipes.Errors;
using SemiStep.Core.Recipes.Import.Errors;
using SemiStep.Tests.Csv.Helpers;

using Xunit;

namespace SemiStep.Tests.Csv.Integration;

[Trait("Component", "Csv")]
[Trait("Category", "Integration")]
[Trait("Area", "Deserialization")]
public sealed class CsvDeserializationTests(CsvFixture fixture) : IClassFixture<CsvFixture>
{
	[Fact]
	public void Deserialize_RoundTrip_PreservesRecipe()
	{
		var step = new Step(10, ImmutableDictionary<PropertyId, PropertyValue>.Empty
			.Add(new PropertyId("step_duration"), PropertyValue.FromFloat(5.0f))
			.Add(new PropertyId("comment"), PropertyValue.FromString("test comment")));

		var original = new Recipe([step]);
		var csv = fixture.FileSerializer.Serialize(original);
		var result = fixture.FileSerializer.Deserialize(csv);

		result.IsSuccess.Should().BeTrue();
		result.Value.StepCount.Should().Be(1);
		var deserializedStep = result.Value.Steps[0];
		deserializedStep.ActionKey.Should().Be(10);
		deserializedStep.Properties[new PropertyId("step_duration")]
			.Should().Be(PropertyValue.FromFloat(5.0f));
		deserializedStep.Properties[new PropertyId("comment")]
			.Should().Be(PropertyValue.FromString("test comment"));
	}

	[Fact]
	public void Deserialize_InvalidActionId_FailsWithActionNotFoundAtRow()
	{
		var csv = "action;step_duration;task;comment\n99999;10;0;test\n";
		var result = fixture.FileSerializer.Deserialize(csv);

		var rowError = result.Errors.Should().ContainSingle().Which.Should().BeOfType<AtRowError>().Subject;
		rowError.RowNumber.Should().Be(2);
		rowError.Inner.Should().BeOfType<ActionByIdNotFoundError>().Which.Id.Should().Be(99999);
	}

	[Fact]
	public void Deserialize_EmptyActionColumn_FailsWithActionColumnEmptyAtRow()
	{
		var csv = "action;step_duration;task;comment\n;10;0;test\n";
		var result = fixture.FileSerializer.Deserialize(csv);

		var rowError = result.Errors.Should().ContainSingle().Which.Should().BeOfType<AtRowError>().Subject;
		rowError.RowNumber.Should().Be(2);
		rowError.Inner.Should().BeOfType<ActionColumnEmptyError>();
	}

	[Fact]
	public void Deserialize_HeaderMismatch_FailsWithHeaderMismatch()
	{
		var csv = "wrong_header;bad_column\n10;5\n";
		var result = fixture.FileSerializer.Deserialize(csv);

		result.Errors.Should().ContainSingle().Which.Should().BeOfType<CsvHeaderMismatchError>();
	}

	[Fact]
	public void ClipboardRoundTrip_SerializeAndDeserializeWithoutHeaders()
	{
		var step1 = new Step(10, ImmutableDictionary<PropertyId, PropertyValue>.Empty
			.Add(new PropertyId("step_duration"), PropertyValue.FromFloat(5.0f))
			.Add(new PropertyId("comment"), PropertyValue.FromString("first")));

		var step2 = new Step(10, ImmutableDictionary<PropertyId, PropertyValue>.Empty
			.Add(new PropertyId("step_duration"), PropertyValue.FromFloat(15.0f))
			.Add(new PropertyId("comment"), PropertyValue.FromString("second")));

		var steps = new List<Step> { step1, step2 };
		var recipe = new Recipe([.. steps]);

		var csv = fixture.ClipboardSerializer.SerializeSteps(recipe);
		csv.Should().Contain("\t");
		csv.Should().NotContain(";");

		var result = fixture.ClipboardSerializer.DeserializeSteps(csv);

		result.IsSuccess.Should().BeTrue();
		result.Value.Steps.Should().BeEquivalentTo(steps, options => options.WithStrictOrdering());
	}

	[Fact]
	public void ClipboardDeserialize_EmptyString_FailsWithNoValidSteps()
	{
		var result = fixture.ClipboardSerializer.DeserializeSteps("");

		result.IsFailed.Should().BeTrue();
		result.Errors[0].Should().BeOfType<NoValidStepsError>();
	}

	[Fact]
	public void ClipboardDeserialize_NonIntegerAction_FailsWithActionValueNotIntegerAtRow()
	{
		var result = fixture.ClipboardSerializer.DeserializeSteps("not\tvalid\tcsv\tdata");

		var rowError = result.Errors.Should().ContainSingle().Which.Should().BeOfType<AtRowError>().Subject;
		rowError.RowNumber.Should().Be(1);
		rowError.Inner.Should().BeOfType<ActionValueNotIntegerError>();
	}

	[Fact]
	public void ClipboardDeserialize_MalformedQuotedData_FailsWithColumnCountMismatch()
	{
		var malformed = "\"1\"\t\"\"\t\"\"\t\"\"\t\"\"\t\"\"\t\"\"\t\"\"\t\"\"\t\"\"\t\"\"\t\"\"";
		var result = fixture.ClipboardSerializer.DeserializeSteps(malformed);

		result.IsFailed.Should().BeTrue();
		result.Errors[0].Should().BeOfType<ColumnCountMismatchError>();
	}

	[Fact]
	public void ClipboardDeserialize_UnescapedQuote_FailsWithParseFailure()
	{
		var unescapedQuote = "1\"0\t5.0";
		var result = fixture.ClipboardSerializer.DeserializeSteps(unescapedQuote);

		result.IsFailed.Should().BeTrue();
		result.Errors[0].Should().BeOfType<ClipboardParseFailedError>();
	}

	[Fact]
	public void ClipboardDeserialize_FewerColumnsThanExpected_SucceedsWithMissingFieldsTreatedAsEmpty()
	{
		var fewerColumns = "10\t5.0";
		var result = fixture.ClipboardSerializer.DeserializeSteps(fewerColumns);

		result.IsSuccess.Should().BeTrue();
		result.Value.Steps.Should().HaveCount(1);
		result.Value.Steps[0].ActionKey.Should().Be(10);
	}

	[Fact]
	public void ClipboardDeserialize_MoreColumnsThanExpected_ReturnsColumnCountMismatch()
	{
		var tooManyColumns = "10\t5.0\t0\ttest\textra";
		var result = fixture.ClipboardSerializer.DeserializeSteps(tooManyColumns);

		var mismatch = result.Errors.Should().ContainSingle().Which.Should().BeOfType<ColumnCountMismatchError>().Subject;
		mismatch.RowNumber.Should().Be(1);
		mismatch.Actual.Should().Be(5);
	}

	[Fact]
	public void ClipboardDeserialize_NonParseableValue_FailsWithRowColumnValueChain()
	{
		// Field order: action, step_duration, task (float), comment. Action 20 (For) declares the float
		// "task" property.
		var body = "20\t5.0\tabc\tc";
		var result = fixture.ClipboardSerializer.DeserializeSteps(body);

		var rowError = result.Errors.Should().ContainSingle().Which.Should().BeOfType<AtRowError>().Subject;
		rowError.RowNumber.Should().Be(1);
		var columnError = rowError.Inner.Should().BeOfType<AtColumnError>().Subject;
		columnError.ColumnKey.Should().Be("task");
		columnError.Inner.Should().BeOfType<PropertyValueParseError>().Which.RawValue.Should().Be("abc");
	}
}
