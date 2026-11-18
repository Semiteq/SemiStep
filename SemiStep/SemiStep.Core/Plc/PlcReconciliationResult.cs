namespace SemiStep.Core.Plc;

/// <summary>
/// <see cref="PlcStepCount"/> is null until the PLC recipe was read and <see cref="Committed"/> is
/// null until the managing area was read, so a null value marks a fact the reconciliation never
/// established rather than a fact it established as zero or false.
/// </summary>
internal readonly record struct PlcReconciliationResult(
	PlcReconciliationOutcome Outcome,
	int LocalStepCount,
	bool LocalValid,
	int? PlcStepCount,
	bool? Committed,
	bool HasConflictSubscriber);
