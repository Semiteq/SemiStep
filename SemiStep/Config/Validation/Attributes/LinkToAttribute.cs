namespace SemiStep.Config.Validation.Attributes;

/// <summary>
/// Атрибут для указания ссылки на другой объект в конфигурации
/// Используется для валидации линковки между объектами
/// </summary>
/// <example>
/// [LinkTo("Actions", "Key")]
/// public string ActionReference { get; set; }
/// 
/// Это означает, что значение ActionReference должно существовать
/// в коллекции Actions по полю Key
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class LinkToAttribute : Attribute
{
	/// <summary>
	/// Имя свойства коллекции, содержащей целевые объекты
	/// Например: "Actions", "Properties"
	/// </summary>
	public string TargetCollectionProperty { get; }

	/// <summary>
	/// Имя свойства в целевом объекте, которое используется как ключ
	/// Например: "Key", "Id"
	/// </summary>
	public string TargetKeyProperty { get; }

	public LinkToAttribute(string targetCollectionProperty, string targetKeyProperty)
	{
		TargetCollectionProperty = targetCollectionProperty ?? throw new ArgumentNullException(nameof(targetCollectionProperty));
		TargetKeyProperty = targetKeyProperty ?? throw new ArgumentNullException(nameof(targetKeyProperty));
	}
}
