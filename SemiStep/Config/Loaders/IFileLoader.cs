using SemiStep.Config.Dto;

namespace SemiStep.Config.Loaders;

/// <summary>
/// Интерфейс для загрузки конфигурационных файлов
/// </summary>
public interface IFileLoader
{
	/// <summary>
	/// Загружает и десериализует конфигурационный файл
	/// </summary>
	/// <param name="filePath">Путь к файлу</param>
	/// <returns>Десериализованный объект конфигурации</returns>
	Task<ConfigRootDto> LoadAsync(string filePath);

	/// <summary>
	/// Проверяет, поддерживается ли указанный формат файла
	/// </summary>
	bool SupportsFormat(string filePath);
}
