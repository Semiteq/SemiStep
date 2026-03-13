using Csv.ClipboardService;
using Csv.Facade;
using Csv.FsService;

using Microsoft.Extensions.DependencyInjection;

using Shared.ServiceContracts;

namespace Csv;

public static class CsvDi
{
	public static IServiceCollection AddCsv(this IServiceCollection services)
	{
		services.AddSingleton<CsvFileSerializer>();
		services.AddSingleton<CsvClipboardSerializer>();
		services.AddSingleton<ICsvService, CsvService>();
		services.AddSingleton<ICsvClipboardService, CsvClipboard>();

		return services;
	}
}
