using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PingByDaylight.Application.Interfaces;
using PingByDaylight.Domain;

namespace PingByDaylight.Infrastructure.Services;

public class ServerCatalogService : IServerCatalogService
{
    private readonly ILogger<ServerCatalogService> _logger;
    private readonly IReadOnlyList<ServerInfo> _servers;

    public ServerCatalogService(ILogger<ServerCatalogService> logger, IOptions<RegionCatalogOptions> options)
    {
        _logger = logger;
        
        var config = options.Value;
        _servers = config.Regions.Select(r => new ServerInfo(
            r.Key,
            r.Key, // DisplayName - используем ключ как отображаемое имя
            r.GroupKey,
            r.GroupDisplayName,
            r.Hosts.ToArray(),
            !r.Stable
        )).ToList().AsReadOnly();
    }

    public Task<Result<IReadOnlyList<ServerInfo>>> GetServersAsync()
    {
        return Task.FromResult(Result.Success(_servers));
    }

    public Task<Result<ServerInfo>> GetServerByKeyAsync(string key)
    {
        var server = _servers.FirstOrDefault(s => s.Key == key);
        if (server != null)
        {
            return Task.FromResult(Result.Success(server));
        }
        
        return Task.FromResult(Result.Failure<ServerInfo>($"Server '{key}' not found"));
    }
}
