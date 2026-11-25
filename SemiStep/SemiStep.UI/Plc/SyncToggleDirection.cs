namespace SemiStep.UI.Plc;

/// <summary>The sync toggle command in flight, set by the command itself before it changes the sync flag.</summary>
public enum SyncToggleDirection
{
	None,
	Enabling,
	Disabling
}
