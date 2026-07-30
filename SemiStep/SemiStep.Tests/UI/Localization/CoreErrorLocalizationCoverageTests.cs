using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using FluentResults;

using SemiStep.Core.Plc.Sync.Ownership;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Analysis.Warnings;
using SemiStep.Core.Recipes.Errors;
using SemiStep.Core.Recipes.Formulas.Errors;
using SemiStep.Core.Recipes.Import.Errors;
using SemiStep.Core.Recipes.Import.Warnings;
using SemiStep.Core.Shared;

using SemiStep.UI.Localization;

using Xunit;

namespace SemiStep.Tests.UI.Localization;

[Trait("Component", "UI")]
[Trait("Area", "Localization")]
[Trait("Category", "Unit")]
public sealed class CoreErrorLocalizationCoverageTests
{
	private static readonly IReadOnlyDictionary<Type, IReason> _typeData = new Dictionary<Type, IReason>
	{
		[typeof(OwnedByAnotherInstanceError)] =
			new OwnedByAnotherInstanceError(new OwnerInfo(1, "MACHINE", "alice", DateTimeOffset.UnixEpoch)),
		[typeof(FormulaComputationFailedError)] =
			new FormulaComputationFailedError("temp", new Error("min > max")),
		[typeof(AtStepError)] =
			new AtStepError(1, new Error("x")),
		[typeof(AtColumnError)] =
			new AtColumnError("k", new Error("x")),
		[typeof(AtRowError)] =
			new AtRowError(1, new Error("x")),
		[typeof(ActionColumnNotFoundError)] =
			new ActionColumnNotFoundError(),
		[typeof(ActionColumnEmptyError)] =
			new ActionColumnEmptyError(),
		[typeof(ActionValueNotIntegerError)] =
			new ActionValueNotIntegerError("abc"),
		[typeof(CsvBodyEmptyError)] =
			new CsvBodyEmptyError(),
		[typeof(CsvHeaderMismatchError)] =
			new CsvHeaderMismatchError("a; b", "a; c"),
		[typeof(RecipeFileNotFoundError)] =
			new RecipeFileNotFoundError("recipe.csv"),
		[typeof(RecipeLoadFailedError)] =
			new RecipeLoadFailedError("recipe.csv"),
		[typeof(RecipeSaveFailedError)] =
			new RecipeSaveFailedError("recipe.csv"),
		[typeof(PropertyValueTypeMismatchError)] =
			new PropertyValueTypeMismatchError("int", "Int32", "temperature"),
		[typeof(UnsupportedPropertySystemTypeError)] =
			new UnsupportedPropertySystemTypeError("decimal"),
		[typeof(GroupValueNotIntegerError)] =
			new GroupValueNotIntegerError(PropertyType.String),
		[typeof(ValueBelowMinimumError)] =
			new ValueBelowMinimumError(3, 10, "temperature"),
		[typeof(ValueAboveMaximumError)] =
			new ValueAboveMaximumError(500, 100, "temperature"),
		[typeof(StringContainsNulError)] =
			new StringContainsNulError("label"),
		[typeof(StringTooLongError)] =
			new StringTooLongError(20, 8, "label"),
		[typeof(ActionByIdNotFoundError)] =
			new ActionByIdNotFoundError(42),
		[typeof(ActionByNameNotFoundError)] =
			new ActionByNameNotFoundError("Heat"),
		[typeof(PropertyNotFoundError)] =
			new PropertyNotFoundError("temperature"),
		[typeof(ColumnNotFoundError)] =
			new ColumnNotFoundError("temp"),
		[typeof(GroupNotFoundError)] =
			new GroupNotFoundError("valves"),
		[typeof(ValueNotInGroupError)] =
			new ValueNotInGroupError(7, "valves"),
		[typeof(NoStateToUndoError)] =
			new NoStateToUndoError(),
		[typeof(NoStateToRedoError)] =
			new NoStateToRedoError(),
		[typeof(InsertIndexOutOfRangeError)] =
			new InsertIndexOutOfRangeError(5, 3),
		[typeof(StepIndexOutOfRangeError)] =
			new StepIndexOutOfRangeError(5, 3),
		[typeof(PropertyValueParseError)] =
			new PropertyValueParseError("abc", "integer"),
		[typeof(MaxLoopNestingDepthExceededError)] =
			new MaxLoopNestingDepthExceededError(3, 5),
		[typeof(IterationCountUnsupportedTypeError)] =
			new IterationCountUnsupportedTypeError(PropertyType.String, 42),
		[typeof(UnmatchedEndForWarning)] =
			new UnmatchedEndForWarning(1),
		[typeof(UnclosedForLoopWarning)] =
			new UnclosedForLoopWarning(1),
		[typeof(RowCountMismatchWarning)] =
			new RowCountMismatchWarning("recipe.csv", 5, 3)
	};

	[Fact]
	public void EveryPublicCoreReasonType_HasSampleAndLocalizesUnderRussianCulture()
	{
		using (ResourcesCultureScope.Use("ru"))
		{
			PublicCoreErrorTypes().Should().NotBeEmpty(
				"the coverage loop must not pass vacuously if all public Core errors became internal");
			PublicCoreWarningTypes().Should().NotBeEmpty(
				"the coverage loop must not pass vacuously if all public Core warnings became internal");

			foreach (var reasonType in PublicCoreErrorTypes().Concat(PublicCoreWarningTypes()))
			{
				_typeData.Should().ContainKey(
					reasonType,
					"every public Core Error/Warning subclass needs a sample in _typeData");

				var sample = _typeData[reasonType];
				var localized = ReasonLocalizer.Localize(sample);

				localized.Should().NotBeNullOrEmpty(
					"{0} must localize to a non-empty sentence",
					reasonType.Name);
				localized.Should().NotBe(
					sample.Message,
					"{0} needs a ReasonLocalizer switch case producing localized text under ru",
					reasonType.Name);
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

	private static IEnumerable<Type> PublicCoreWarningTypes()
	{
		return typeof(Warning).Assembly
			.GetTypes()
			.Where(type => type.IsVisible
				&& !type.IsAbstract
				&& typeof(Warning).IsAssignableFrom(type)
				&& type != typeof(Warning));
	}
}
