using FluentAssertions;

using SemiStep.Core.Recipes.Formulas;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Recipes.Formulas;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "Formulas")]
public sealed class FormulaIdentifierExtractorTests
{
	[Fact]
	public void Parse_SimpleSum_ReturnsBothOperands()
	{
		var result = FormulaIdentifierExtractor.Parse("a + b");

		result.IsSuccess.Should().BeTrue();
		result.Value.Identifiers.Should().BeEquivalentTo(new[] { "a", "b" });
	}

	[Fact]
	public void Parse_NestedExpression_ReturnsAllIdentifiers()
	{
		var result = FormulaIdentifierExtractor.Parse("(x - y) / z * 60");

		result.IsSuccess.Should().BeTrue();
		result.Value.Identifiers.Should().BeEquivalentTo(new[] { "x", "y", "z" });
	}

	[Fact]
	public void Parse_LiteralsOnly_ReturnsEmptySet()
	{
		var result = FormulaIdentifierExtractor.Parse("3.14 * 2");

		result.IsSuccess.Should().BeTrue();
		result.Value.Identifiers.Should().BeEmpty();
	}

	[Fact]
	public void Parse_SqrtBuiltIn_ExcludesFunctionName()
	{
		var result = FormulaIdentifierExtractor.Parse("Sqrt(a*a + b*b)");

		result.IsSuccess.Should().BeTrue();
		result.Value.Identifiers.Should().BeEquivalentTo(new[] { "a", "b" });
	}

	[Fact]
	public void Parse_PowAbsBuiltIns_ExcludeFunctionNames()
	{
		var result = FormulaIdentifierExtractor.Parse("Pow(x, 2) + Abs(y)");

		result.IsSuccess.Should().BeTrue();
		result.Value.Identifiers.Should().BeEquivalentTo(new[] { "x", "y" });
	}

	[Fact]
	public void Parse_UnparseableExpression_ReturnsFailure()
	{
		var result = FormulaIdentifierExtractor.Parse("a +");

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void Parse_DistinctCasings_PreservesBothIdentifiers()
	{
		var result = FormulaIdentifierExtractor.Parse("A + a");

		result.IsSuccess.Should().BeTrue();
		result.Value.Identifiers.Should().BeEquivalentTo(new[] { "A", "a" });
	}

	[Fact]
	public void Parse_EmptySource_ReturnsFailure()
	{
		var result = FormulaIdentifierExtractor.Parse("");

		result.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void Parse_ValidExpression_PopulatesLogicalExpression()
	{
		var result = FormulaIdentifierExtractor.Parse("(a - b) / c * 60");

		result.IsSuccess.Should().BeTrue();
		result.Value.LogicalExpression.Should().NotBeNull();
	}
}
