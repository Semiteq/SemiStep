namespace Core.Services;

/// <summary>
/// Provides data for UI comboboxes for actions and enum-like columns.
/// </summary>
public interface IComboboxDataProvider
{
	/// <summary>
	/// Retrieves a list of enum options for a specific action column, potentially sourced from pin groups.
	/// </summary>
	/// <param name="actionId">Action identifier.</param>
	/// <param name="columnKey">Column key as defined in ColumnDefs.yaml and ActionsDefs.yaml.</param>
	/// <returns>List of pairs: (id, display name). Empty if unavailable.</returns>
	Result<IReadOnlyDictionary<short, string>> GetResultEnumOptions(short actionId, string columnKey);

	/// <summary>
	/// Retrieves a list of actions for UI.
	/// </summary>
	IReadOnlyDictionary<short, string> GetActions();
}
