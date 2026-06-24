using System.Globalization;

using Avalonia.Data.Converters;

using FluentAssertions;

using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.RecipeGrid;

/// <summary>
/// Exercises the pure <see cref="IValueConverter"/> behind
/// <see cref="CellApplicabilityBinding.CreateChangedBinding"/>: a column key present in the
/// changed set converts to <c>true</c>; an absent key or a <c>null</c> set converts to <c>false</c>.
/// No headless harness is required, mirroring the inapplicable converter contract.
/// </summary>
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
[Trait("Category", "Unit")]
public sealed class CellApplicabilityBindingChangedTests
{
	private const string ColumnKey = "Temperature";

	[Fact]
	public void ChangedConverter_ColumnPresent_ReturnsTrue()
	{
		var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ColumnKey };

		Convert(set).Should().Be(true);
	}

	[Fact]
	public void ChangedConverter_ColumnAbsent_ReturnsFalse()
	{
		var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Pressure" };

		Convert(set).Should().Be(false);
	}

	[Fact]
	public void ChangedConverter_NullSet_ReturnsFalse()
	{
		Convert(null).Should().Be(false);
	}

	[Fact]
	public void ChangedConverter_CaseMismatch_ReturnsTrue_WhenSetIsOrdinalIgnoreCase()
	{
		// The changed set is built OrdinalIgnoreCase (see RecipeRowViewModel mutators), so a column
		// key that differs only in case still matches. Locks the case-insensitive lookup contract.
		var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ColumnKey.ToUpperInvariant() };

		Convert(set).Should().Be(true);
	}

	private static object? Convert(IReadOnlySet<string>? set)
	{
		var converter = CellApplicabilityBinding.CreateChangedBinding(ColumnKey).Converter!;

		return converter.Convert(set, typeof(bool), null, CultureInfo.InvariantCulture);
	}
}
