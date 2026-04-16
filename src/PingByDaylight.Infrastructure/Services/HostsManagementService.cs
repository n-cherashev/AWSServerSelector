using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using PingByDaylight.Application.Interfaces;
using PingByDaylight.Domain;

namespace PingByDaylight.Infrastructure.Services;

public class HostsManagementService : IHostsManagementService
{
    private readonly ILogger<HostsManagementService> _logger;

    public HostsManagementService(ILogger<HostsManagementService> logger)
    {
        _logger = logger;
    }

    public Task<UnitResult> ApplySelectionAsync(ServerSelection selection)
    {
        // TODO: Реализация записи в hosts файл
        _logger.LogInformation("Applying selection: {Region}, {ApplyMode}, {BlockMode}", 
            selection.RegionKey, selection.ApplyMode, selection.BlockMode);
        
        return Task.FromResult(UnitResult.Success());
    }

    public Task<UnitResult> ResetToDefaultAsync()
    {
        // TODO: Реализация сброса hosts файла
        _logger.LogInformation("Resetting to default configuration");
        return Task.FromResult(UnitResult.Success());
    }

    public Task<Result<string>> GetHostsContentAsync()
    {
        // TODO: Чтение текущего содержимого hosts
        return Task.FromResult(Result.Success("# Hosts content placeholder"));
    }
}
