using Core.Entities;
using Core.Reasons.Errors;

using FluentResults;

namespace Core.Analyzer;

/// <summary>
/// Validates basic structural invariants (null steps, missing action property).
/// </summary>
public sealed class StructureValidator : IStructureValidator
{
	public StructureResult Validate(Recipe recipe)
	{
		var reasons = new List<IReason>();

		if (recipe.Steps.Count == 0)
			return new StructureResult(reasons);

		for (int i = 0; i < recipe.Steps.Count; i++)
		{
			var step = recipe.Steps[i];
			if (step == null)
			{
				reasons.Add(new CoreStepNullError(i));
				continue;
			}

			if (!step.Properties.ContainsKey(MandatoryColumns.Action))
			{
				reasons.Add(new CoreStepMissingActionError(i));
				continue;
			}

			var actionProperty = step.Properties[MandatoryColumns.Action];
			if (actionProperty == null)
			{
				reasons.Add(new CoreStepActionNullError(i));
			}
		}

		return new StructureResult(reasons);
	}
}
