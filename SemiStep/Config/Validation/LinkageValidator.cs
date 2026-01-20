using System.Reflection;

using SemiStep.Config.Models;
using SemiStep.Config.Validation.Attributes;

namespace SemiStep.Config.Validation;

/// <summary>
/// Валидатор для проверки линковки между объектами конфигурации
/// Использует атрибут LinkToAttribute
/// </summary>
public sealed class LinkageValidator : IValidator
{
	public async Task<ValidationResult> ValidateAsync(object config, ConfigContext context)
	{
		var result = new ValidationResult();

		// TODO: Реализовать валидацию линковки через рефлексию
		// 1. Рекурсивно обойти все свойства объекта config
		// 2. Найти свойства с атрибутом [LinkTo]
		// 3. Извлечь целевую коллекцию по имени (TargetCollectionProperty)
		// 4. Проверить, существует ли объект с указанным ключом (TargetKeyProperty)
		// 5. Добавить ошибку если ссылка битая

		// Пример:
		// [LinkTo("Actions", "Key")]
		// public string ActionReference { get; set; }
		//
		// Нужно:
		// - Получить значение ActionReference
		// - Найти свойство "Actions" в корневом объекте
		// - Проверить, есть ли в коллекции Actions объект с Key == ActionReference

		await Task.CompletedTask;
		return result;
	}

	private void ValidateLinksInObject(object obj, object rootConfig, ValidationResult result, string path = "")
	{
		if (obj == null)
			return;

		var type = obj.GetType();

		foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			var linkAttr = property.GetCustomAttribute<LinkToAttribute>();
			if (linkAttr != null)
			{
				var value = property.GetValue(obj);
				if (value != null)
				{
					// TODO: Проверить существование целевого объекта
					// ValidateLink(value, linkAttr, rootConfig, result, $"{path}.{property.Name}");
				}
			}

			// Рекурсивно обработать вложенные объекты
			var propertyValue = property.GetValue(obj);
			if (propertyValue != null)
			{
				// TODO: Обработать коллекции и вложенные объекты
			}
		}
	}
}
