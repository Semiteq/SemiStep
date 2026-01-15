using System.Reflection;

using Config.Models;
using Config.Validation.Attributes;

namespace Config.Validation;

public sealed class RangeValidator : IValidator
{
	public async Task<ValidationResult> ValidateAsync(object config, ConfigContext context)
	{
		var result = new ValidationResult();

		// TODO: Реализовать валидацию диапазонов через рефлексию
		// 1. Рекурсивно обойти все свойства объекта config
		// 2. Найти свойства с атрибутом [RangeValidation]
		// 3. Проверить значения на соответствие диапазону
		// 4. Добавить ошибки в result

		await Task.CompletedTask;
		return result;
	}

	private void ValidateObject(object obj, ValidationResult result, string path = "")
	{
		if (obj == null)
		{
			return;
		}

		var type = obj.GetType();

		foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			var rangeAttr = property.GetCustomAttribute<RangeValidationAttribute>();
			if (rangeAttr != null)
			{
				var value = property.GetValue(obj);
				// TODO: Проверить значение на соответствие диапазону
			}

			// Рекурсивно валидировать вложенные объекты
			var propertyValue = property.GetValue(obj);
			if (propertyValue != null && !property.PropertyType.IsPrimitive && property.PropertyType != typeof(string))
			{
				ValidateObject(propertyValue, result, $"{path}.{property.Name}");
			}
		}
	}
}
