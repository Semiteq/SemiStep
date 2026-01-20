using Core.Entities;

namespace Core.Metadata;

public interface IMetadataProvider
{
	ActionMetadata GetAction(string actionKey);

	PropertyMetadata GetProperty(ColumnId columnId);

	IReadOnlyList<ActionMetadata> GetAllActions();

	bool ActionExists(string actionKey);

	bool PropertyExists(ColumnId columnId);
}
