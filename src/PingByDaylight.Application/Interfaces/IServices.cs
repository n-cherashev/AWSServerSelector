using CSharpFunctionalExtensions;
using PingByDaylight.Domain;

namespace PingByDaylight.Application.Interfaces;

public interface IServerCatalogService
{
    Task<Result<IReadOnlyList<ServerInfo>>> GetServersAsync();
    Task<Result<ServerInfo>> GetServerByKeyAsync(string key);
}

public interface IConnectionProbeService
{
    Task<Result<ConnectionStatus>> ProbeAsync(string serverKey);
    Task<Result<IReadOnlyList<ConnectionStatus>>> ProbeAllAsync(IEnumerable<string> serverKeys);
}

public interface IHostsManagementService
{
    Task<UnitResult<string>> ApplySelectionAsync(ServerSelection selection);
    Task<UnitResult<string>> ResetToDefaultAsync();
    Task<Result<string>> GetHostsContentAsync();
}

public interface IUserSettingsService
{
    Task<Result<T>> GetSettingAsync<T>(string key, T defaultValue);
    Task<UnitResult<string>> SetSettingAsync<T>(string key, T value);
}
