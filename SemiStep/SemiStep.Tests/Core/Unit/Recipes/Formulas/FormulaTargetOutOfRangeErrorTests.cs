using System.Globalization;

using FluentAssertions;

using SemiStep.Core.Recipes.Formulas.Errors;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Recipes.Formulas;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Area", "Formulas")]
public sealed class FormulaTargetOutOfRangeErrorTests
{
	[Fact]
	public void Constructor_BothBounds_MessageHasClosedInterval()
	{
		var error = new FormulaTargetOutOfRangeError("temp", 150d, 0d, 100d);

		error.Message.Should().Contain("[0; 100]");
		error.Min.Should().Be(0d);
		error.Max.Should().Be(100d);
	}

	[Fact]
	public void Constructor_OnlyMin_MessageHasUpperInfinity()
	{
		var error = new FormulaTargetOutOfRangeError("temp", -5d, 0d, null);

		error.Message.Should().Contain("[0; +∞)");
		error.Min.Should().Be(0d);
		error.Max.Should().BeNull();
	}

	[Fact]
	public void Constructor_OnlyMax_MessageHasLowerInfinity()
	{
		var error = new FormulaTargetOutOfRangeError("temp", 200d, null, 100d);

		error.Message.Should().Contain("(-∞; 100]");
		error.Max.Should().Be(100d);
		error.Min.Should().BeNull();
	}

	[Fact]
	public void Constructor_NoBounds_MessageMarksUnbounded()
	{
		var error = new FormulaTargetOutOfRangeError("temp", 42d, null, null);

		error.Message.Should().Contain("(unbounded)");
		error.Min.Should().BeNull();
		error.Max.Should().BeNull();
	}

	[Fact]
	public void Constructor_NonIntegerValueUnderRussianCulture_UsesInvariantDecimalSeparator()
	{
		var previousCulture = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = new CultureInfo("ru-RU");

			var error = new FormulaTargetOutOfRangeError("temp", 0.5d, 100.25d, 200.75d);

			error.Message.Should().Contain("0.5");
			error.Message.Should().Contain("100.25");
			error.Message.Should().Contain("200.75");
			error.Message.Should().NotContain("0,5");
			error.Message.Should().NotContain("100,25");
		}
		finally
		{
			CultureInfo.CurrentCulture = previousCulture;
		}
	}
}
