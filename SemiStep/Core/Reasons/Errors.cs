using FluentResults;

namespace Core.Reasons;

internal static class Errors
{
	internal static Error ActionNameEmpty => new("ACTION_NAME_EMPTY");
	internal static Error ActionNameNotFound => new("ACTION_NAME_NOT_FOUND");
	internal static Error ActionIdNotFound => new("ACTION_ID_NOT_FOUND");
	internal static Error ActionPropertyCreationFailure => new("ACTION_PROPERTY_CREATION_FAILURE");

	internal static Error ColumnGroupNameEmpty => new("COLUMN_GROUP_NAME_EMPTY");
	internal static Error ColumnNotFoundInAction => new("COLUMN_NOT_FOUND_IN_ACTION");

	internal static Error FormulaComputationFailure => new("FORMULA_COMPUTATION_FAILURE");
	internal static Error FormulaDivisionByZero => new("FORMULA_DIVISION_BY_ZERO");
	internal static Error FormulaNotFound => new("FORMULA_NOT_FOUND");
	internal static Error FormulaTargetNotFound => new("FORMULA_TARGET_NOT_FOUND");
	internal static Error FormulaVariableNotNumeric => new("FORMULA_VARIABLE_NOT_NUMERIC");
	internal static Error FormulaVariableNotFound => new("FORMULA_VARIABLE_NOT_FOUND");
	internal static Error FormulaVariableUnknown => new("FORMULA_VARIABLE_UNKNOWN");

	internal static Error IndexOutOfRange => new("INDEX_OUT_OF_RANGE");
	internal static Error NoStepsInRecipe => new("NO_STEPS_IN_RECIPE");

	internal static Error NoActionsInConfig => new("NO_ACTIONS_IN_CONFIG");

	internal static Error StepActionIsNull => new("STEP_ACTION_IS_NULL");
	internal static Error StepActionPropertyIsNull => new("STEP_ACTION_PROPERTY_IS_NULL");
	internal static Error StepColumnNotFound => new("STEP_COLUMN_NOT_FOUND");
	internal static Error StepFailedToSetDefaultTarget => new("STEP_FAILED_TO_SET_DEFAULT_TARGET");
	internal static Error StepMissingAction => new("STEP_MISSING_ACTION");
	internal static Error StepIsNull => new("STEP_IS_NULL");
	internal static Error StepPropertyIsNull => new("STEP_PROPERTY_IS_NULL");
	internal static Error StepPropertyUpdateFailed => new("STEP_PROPERTY_UPDATE_FAILED");

	internal static Error RecipeIsNull => new("RECIPE_IS_NULL");

	internal static Error StringExceededMaxLength => new("STRING_EXCEEDED_MAX_LENGTH");
	internal static Error TargetsNotDefined => new("TARGETS_NOT_DEFINED");

	internal static Error NumericValueOutOfRange => new("NUMERIC_VALUE_OUT_OF_RANGE");

	internal static Error PropertyConversionFailed => new("PROPERTY_CONVERSION_FAILED");
	internal static Error PropertyCreationFailed => new("PROPERTY_CREATION_FAILED");
	internal static Error PropertyDefaultValueFailed => new("PROPERTY_DEFAULT_VALUE_FAILED");
	internal static Error PropertyNonNumeric => new("PROPERTY_NON_NUMBERIC");
	internal static Error PropertyParsingFailed => new("PROPERTY_PARSING_FAILED");
	internal static Error PropertyTypeMismatch => new("PROPERTY_TYPE_MISMATCH");
	internal static Error PropertyValidationFailed => new("PROPERTY_VALIDATION_FAILED");
}
