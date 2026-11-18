namespace SemiStep.Core.Plc;

/// <summary>
/// Every reconciliation produces exactly one of these, including the two paths that unwind through
/// an <see cref="OperationCanceledException"/>.
/// </summary>
internal enum PlcReconciliationOutcome
{
	ProtocolVersionMismatch,
	ProtocolVersionCheckFailed,
	ManagingAreaReadFailed,
	NotCommitted,
	RecipeReadFailed,
	AppliedFromPlc,
	ApplyFromPlcFailed,
	ConflictRaised,
	PlcEmpty,
	Identical,
	TimedOut,
	Cancelled
}
