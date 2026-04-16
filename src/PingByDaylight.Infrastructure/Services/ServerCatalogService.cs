using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using PingByDaylight.Application.Interfaces;
using PingByDaylight.Domain;

namespace PingByDaylight.Infrastructure.Services;

public class ServerCatalogService : IServerCatalogService
{
    private readonly ILogger<ServerCatalogService> _logger;
    private readonly IReadOnlyList<ServerInfo> _servers;

    public ServerCatalogService(ILogger<ServerCatalogService> logger)
    {
        _logger = logger;
        
        // Заглушка - в реальности загружается из regions.json
        _servers = new List<ServerInfo>
        {
            new("eu-west", "EU West", "Europe", "Europe", ["1.1.1.1"], false),
            new("eu-central", "EU Central", "Europe", "Europe", ["2.2.2.2"], false),
            new("us-east", "US East", "North America", "North America", ["3.3.3.3"], false),
            new("us-west", "US West", "North America", "North America", ["4.4.4.4"], false),
            new("asia-east", "Asia East", "Asia", "Asia", ["5.5.5.5"], false),
        }.AsReadOnly();
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
