using System.Net.NetworkInformation;
using System.Net.Sockets;
using Ardalis.Result;
using Microsoft.Extensions.Logging;
using PingByDaylight.Application.Interfaces;
using PingByDaylight.Domain;

namespace PingByDaylight.Infrastructure.Services;

public class ConnectionProbeService : IConnectionProbeService
{
    private readonly ILogger<ConnectionProbeService> _logger;

    public ConnectionProbeService(ILogger<ConnectionProbeService> logger)
    {
        _logger = logger;
    }

    public async Task<Result<ConnectionStatus>> ProbeAsync(string serverKey)
    {
        try
        {
            // В реальной реализации здесь будет получение хоста из каталога
            // Для примера используем заглушку
            var ping = new Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 3000);
            
            var state = reply.Status switch
            {
                IPStatus.Success => ConnectionState.Connected,
                IPStatus.TimedOut => ConnectionState.Timeout,
                _ => ConnectionState.Error
            };

            var status = new ConnectionStatus(
                serverKey,
                (int)reply.RoundtripTime,
                state,
                DateTime.UtcNow
            );

            return Result.Success(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to probe server {ServerKey}", serverKey);
            return Result.Error(new Error("NetworkError", $"Probe failed: {ex.Message}"));
        }
    }

    public async Task<Result<IReadOnlyList<ConnectionStatus>>> ProbeAllAsync(IEnumerable<string> serverKeys)
    {
        var tasks = serverKeys.Select(ProbeAsync);
        var results = await Task.WhenAll(tasks);
        
        var statuses = new List<ConnectionStatus>();
        Error? firstError = null;

        foreach (var result in results)
        {
            if (result.IsSuccess)
            {
                statuses.Add(result.Value);
            }
            else if (firstError == null)
            {
                firstError = result.Error;
            }
        }

        return firstError == null 
            ? Result.Success<IReadOnlyList<ConnectionStatus>>(statuses)
            : Result.Error<IReadOnlyList<ConnectionStatus>>(firstError);
    }
}
