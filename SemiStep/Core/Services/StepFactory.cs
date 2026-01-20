using System.Collections.Immutable;

using Core.Entities;
using Core.Exceptions;
using Core.Metadata;

namespace Core.Services;

public sealed class StepFactory
{
	private readonly IMetadataProvider _metadata;

	public StepFactory(IMetadataProvider metadata)
	{
		_metadata = metadata;
	}

	public Step Create(string actionKey)
	{
		if (!_metadata.ActionExists(actionKey))
			throw new ActionNotFoundException(actionKey);

		var action = _metadata.GetAction(actionKey);
		var properties = action.PropertyColumns
			.Select(col => _metadata.GetProperty(col))
			.ToImmutableDictionary(
				p => p.ColumnId,
				p => new PropertyValue(p.DefaultValue, p.Type));

		return new Step(actionKey, properties);
	}
}
