using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using FluentResults;

using SemiStep.Core.Plc.Sync.Ownership;
using SemiStep.Core.Recipes.Formulas.Errors;

using SemiStep.UI.Localization;

using Xunit;

namespace SemiStep.Tests.UI.Localization;

[Trait("Component", "UI")]
[Trait("Area", "Localization")]
[Trait("Category", "Unit")]
public sealed class CoreErrorLocalizationCoverageTests
{
	private static readonly IReadOnlyDictionary<Type, Error> _typeData = new Dictionary<Type, Error>
	{
		[typeof(OwnedByAnotherInstanceError)] =
			new OwnedByAnotherInstanceError(new OwnerInfo(1, "MACHINE", "alice", DateTimeOffset.UnixEpoch)),
		[typeof(FormulaComputationFailedError)] =
			new FormulaComputationFailedError("temp", "min > max")
	};

	[Fact]
	public void EveryPublicCoreErrorType_HasSampleAndLocalizesUnderRussianCulture()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			foreach (var errorType in PublicCoreErrorTypes())
			{
				_typeData.Should().ContainKey(
					errorType,
					"every public Core Error subclass needs a sample in _typeData");

				var sample = _typeData[errorType];
				var localized = ReasonLocalizer.Localize(sample);

				localized.Should().NotBeNullOrEmpty(
					"{0} must localize to a non-empty sentence",
					errorType.Name);
				localized.Should().NotBe(
					sample.Message,
					"{0} needs a ReasonLocalizer switch case producing localized text under ru",
					errorType.Name);
			}
		}
	}

	private static IEnumerable<Type> PublicCoreErrorTypes()
	{
		return typeof(OwnedByAnotherInstanceError).Assembly
			.GetTypes()
			.Where(type => type.IsVisible
				&& !type.IsAbstract
				&& typeof(Error).IsAssignableFrom(type)
				&& type != typeof(Error));
	}
}
