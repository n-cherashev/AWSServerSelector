using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using PingByDaylight.Application.Interfaces;

namespace PingByDaylight.Infrastructure.Services;

public class LocalizationService : ILocalizationService
{
    private readonly ILogger<LocalizationService> _logger;
    private string _currentCulture = "en";

    public LocalizationService(ILogger<LocalizationService> logger)
    {
        _logger = logger;
    }

    public string GetString(string key)
    {
        // TODO: Загрузка из ресурсов (.resx файлов)
        return key; // Заглушка - возвращаем ключ как строку
    }

    public void SetLanguage(string culture)
    {
        _currentCulture = culture;
        _logger.LogInformation("Language changed to {Culture}", culture);
    }
}
