using Core.Entities;
using Core.Loops;
using Core.Reasons.Warnings;
using Core.Snapshot;

using FluentResults;

namespace Core.Analyzer;

/// <summary>
/// Implements ordered analysis pipeline and validity rules.
/// </summary>
public sealed class RecipeAnalyzer : IRecipeAnalyzer
{
	private readonly IStructureValidator _structureValidator;
	private readonly ILoopParser _loopParser;
	private readonly ILoopSemanticEvaluator _loopSemanticEvaluator;
	private readonly ITimingCalculator _timingCalculator;

	public RecipeAnalyzer(
		IStructureValidator structureValidator,
		ILoopParser loopParser,
		ILoopSemanticEvaluator loopSemanticEvaluator,
		ITimingCalculator timingCalculator)
	{
		_structureValidator = structureValidator;
		_loopParser = loopParser;
		_loopSemanticEvaluator = loopSemanticEvaluator;
		_timingCalculator = timingCalculator;
	}

	public RecipeAnalysisSnapshot Analyze(Recipe recipe)
	{
		var allReasons = new List<IReason>();
		var flags = AnalysisFlags.None;

		bool empty = recipe.Steps.Count == 0;
		if (empty)
		{
			flags = flags with { EmptyRecipe = true };
			allReasons.Add(new CoreEmptyRecipeWarning());
		}

		var structure = _structureValidator.Validate(recipe);
		allReasons.AddRange(structure.Reasons);

		var loopParse = _loopParser.Parse(recipe);
		allReasons.AddRange(loopParse.Reasons);

		var semantics = _loopSemanticEvaluator.Evaluate(loopParse);
		allReasons.AddRange(semantics.Reasons);

		if (semantics.LoopIntegrityCompromised)
			flags = flags with { LoopIntegrityCompromised = true };
		if (semantics.MaxDepthExceeded)
			flags = flags with { MaxDepthExceeded = true };

		var timing = _timingCalculator.Calculate(recipe, semantics);

		var stepCount = recipe.Steps.Count;

		var loopTree = LoopTree.Create(timing.UpdatedNodes);

		var hasStructuralErrors = structure.Reasons.OfType<BilingualError>().Any();
		var isValid = !hasStructuralErrors
					  && !flags.EmptyRecipe
					  && !flags.LoopIntegrityCompromised
					  && !flags.MaxDepthExceeded;

		return new RecipeAnalysisSnapshot(
			recipe,
			stepCount,
			loopTree,
			timing.StepStartTimes,
			timing.TotalDuration,
			allReasons,
			flags,
			isValid);
	}
}
