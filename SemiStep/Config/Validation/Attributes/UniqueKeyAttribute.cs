namespace SemiStep.Config.Validation.Attributes;

/// <summary>
/// Атрибут для указания, что значение свойства должно быть уникальным
/// в пределах коллекции
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class UniqueKeyAttribute : Attribute
{
	/// <summary>
	/// Опциональное имя коллекции, в пределах которой должна быть уникальность
	/// Например: "Action.Properties" означает уникальность в рамках Properties конкретного Action
	/// Null означает уникальность на верхнем уровне
	/// </summary>
	public string? WithinCollection { get; }

	public UniqueKeyAttribute(string? withinCollection = null)
	{
		WithinCollection = withinCollection;
	}
}
