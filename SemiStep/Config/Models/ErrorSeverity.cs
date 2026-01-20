namespace SemiStep.Config.Models;

/// <summary>
/// Уровень серьезности ошибки конфигурации
/// </summary>
public enum ErrorSeverity
{
	/// <summary>
	/// Информационное сообщение
	/// </summary>
	Info,

	/// <summary>
	/// Предупреждение - конфигурация может работать, но есть проблемы
	/// </summary>
	Warning,

	/// <summary>
	/// Ошибка - конфигурация не может быть использована
	/// </summary>
	Error
}
