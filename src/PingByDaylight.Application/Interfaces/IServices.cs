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
    Task<UnitResult> ApplySelectionAsync(ServerSelection selection);
    Task<UnitResult> ResetToDefaultAsync();
    Task<Result> GetHostsContentAsync();
}

public interface IUserSettingsService
{
    Task<Result<T>> GetSettingAsync<T>(string key, T defaultValue);
    Task<UnitResult> SetSettingAsync<T>(string key, T value);
}

public interface ILocalizationService
{
    string GetString(string key);
    void SetLanguage(string culture);
}

public interface IDialogNavigationService
{
    Task ShowSettingsDialogAsync();
    Task ShowAboutDialogAsync();
    Task ShowUpdateDialogAsync();
    Task ShowConnectionInfoAsync();
}
