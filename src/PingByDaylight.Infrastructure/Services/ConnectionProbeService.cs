using System.Net.NetworkInformation;
using System.Net.Sockets;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using PingByDaylight.Application.Interfaces;
using PingByDaylight.Domain;

namespace PingByDaylight.Infrastructure.Services;

public class ConnectionProbeService : IConnectionProbeService
{
    private readonly ILogger<ConnectionProbeService> _logger;
    private readonly IServerCatalogService _serverCatalogService;
    private readonly int _timeoutMs = 3000;
    private readonly int _maxRetries = 2;

    public ConnectionProbeService(
        ILogger<ConnectionProbeService> logger,
        IServerCatalogService serverCatalogService)
    {
        _logger = logger;
        _serverCatalogService = serverCatalogService;
    }

    public async Task<Result<ConnectionStatus>> ProbeAsync(string serverKey)
    {
        try
        {
            // Получаем информацию о сервере из каталога
            var serverResult = await _serverCatalogService.GetServerByKeyAsync(serverKey);
            if (!serverResult.IsSuccess)
            {
                return Result.Failure<ConnectionStatus>($"Server '{serverKey}' not found");
            }

            var server = serverResult.Value;
            
            // Пингуем все хосты сервера и берем лучший результат
            var bestResult = await ProbeHostsAsync(server.Hosts);
            
            if (bestResult == null)
            {
                return Result.Failure<ConnectionStatus>("All hosts failed to respond");
            }

            var status = new ConnectionStatus(
                serverKey,
                bestResult.Value.LatencyMs,
                bestResult.Value.State,
                DateTime.UtcNow,
                bestResult.Value.JitterMs,
                bestResult.Value.PacketLossPercent
            );

            _logger.LogDebug(
                "Probed {ServerKey}: {Latency}ms, State={State}, Jitter={Jitter}ms, Loss={Loss}%",
                serverKey, 
                status.LatencyMs, 
                status.State,
                status.JitterMs,
                status.PacketLossPercent
            );

            return Result.Success(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to probe server {ServerKey}", serverKey);
            return Result.Failure<ConnectionStatus>($"Probe failed: {ex.Message}");
        }
    }

    private async Task<(int LatencyMs, ConnectionState State, int JitterMs, double PacketLossPercent)?> ProbeHostsAsync(string[] hosts)
    {
        var results = new List<(int LatencyMs, ConnectionState State)>();
        
        foreach (var host in hosts)
        {
            var result = await ProbeSingleHostAsync(host);
            if (result != null && result.Value.State == ConnectionState.Connected)
            {
                results.Add(result.Value);
            }
        }

        if (results.Count == 0)
        {
            // Если все хосты не ответили, возвращаем последний статус
            var lastResult = await ProbeSingleHostAsync(hosts.FirstOrDefault() ?? "8.8.8.8");
            return lastResult.HasValue 
                ? (lastResult.Value.LatencyMs, lastResult.Value.State, 0, 100.0) 
                : null;
        }

        // Вычисляем статистику
        var latencies = results.Select(r => r.LatencyMs).OrderBy(l => l).ToList();
        var avgLatency = (int)latencies.Average();
        var minLatency = latencies.First();
        var maxLatency = latencies.Last();
        var jitter = maxLatency - minLatency;
        var packetLoss = ((hosts.Length - results.Count) / (double)hosts.Length) * 100;

        return (avgLatency, ConnectionState.Connected, jitter, packetLoss);
    }

    private async Task<(int LatencyMs, ConnectionState State)?> ProbeSingleHostAsync(string host)
    {
        using var ping = new Ping();
        
        try
        {
            var latencies = new List<int>();
            
            for (int i = 0; i < _maxRetries; i++)
            {
                var reply = await ping.SendPingAsync(host, _timeoutMs);
                
                if (reply.Status == IPStatus.Success)
                {
                    latencies.Add((int)reply.RoundtripTime);
                }
            }

            if (latencies.Count == 0)
            {
                return (0, ConnectionState.Timeout);
            }

            var avgLatency = (int)latencies.Average();
            return (avgLatency, ConnectionState.Connected);
        }
        catch (PingException ex)
        {
            _logger.LogDebug(ex, "Ping exception for host {Host}", host);
            return (0, ConnectionState.Error);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Exception for host {Host}", host);
            return (0, ConnectionState.Error);
        }
    }

    public async Task<Result<IReadOnlyList<ConnectionStatus>>> ProbeAllAsync(IEnumerable<string> serverKeys)
    {
        var keys = serverKeys.ToList();
        _logger.LogInformation("Starting probe for {Count} servers", keys.Count);
        
        // Выполняем пинг параллельно с ограничением конкуренции
        var semaphore = new SemaphoreSlim(5); // Максимум 5 одновременных пингов
        var tasks = keys.Select(async key =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await ProbeAsync(key);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        
        var statuses = new List<ConnectionStatus>();
        string? firstError = null;

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

        _logger.LogInformation(
            "Probe completed: {Success}/{Total} successful",
            statuses.Count,
            keys.Count
        );

        return firstError == null 
            ? Result.Success<IReadOnlyList<ConnectionStatus>>(statuses)
            : Result.Failure<IReadOnlyList<ConnectionStatus>>(firstError);
    }
}
