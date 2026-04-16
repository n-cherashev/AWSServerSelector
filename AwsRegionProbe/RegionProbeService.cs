using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;

namespace AwsRegionProbe
{
    #region Models

    public class RegionInfo
    {
        public string Key { get; set; } = string.Empty;
        public string GroupKey { get; set; } = string.Empty;
        public string GroupDisplayName { get; set; } = string.Empty;
        public string[] Hosts { get; set; } = Array.Empty<string>();
        public bool Stable { get; set; }
        public string DisplayNameKey { get; set; } = string.Empty;
        public int TcpPort { get; set; } = 443;
    }

    public class RegionCatalog
    {
        public List<RegionInfo> Regions { get; set; } = new();
    }

    public class ProbeSettings
    {
        public int DefaultTcpPort { get; set; } = 443;
        public int DefaultHttpPort { get; set; } = 80;
        public int TimeoutMilliseconds { get; set; } = 5000;
        public int RetryCount { get; set; } = 2;
        public int RetryDelayMilliseconds { get; set; } = 500;
        public bool UseTcpProbe { get; set; } = true;
        public bool UseHttpProbe { get; set; } = false;
        public bool MeasureJitter { get; set; } = true;
        public bool MeasurePacketLoss { get; set; } = true;
        public int MinSuccessfulProbes { get; set; } = 1;
    }

    public class ConfigRoot
    {
        public RegionCatalog RegionCatalog { get; set; } = new();
        public ProbeSettings ProbeSettings { get; set; } = new();
    }

    public enum ConnectionState
    {
        Connected,
        Disconnected,
        Unreachable,
        Timeout
    }

    public class ConnectionStatus
    {
        public string ServerKey { get; }
        public int LatencyMs { get; }
        public ConnectionState State { get; }
        public DateTime Timestamp { get; }
        public int JitterMs { get; }
        public double PacketLoss { get; }

        public ConnectionStatus(
            string serverKey,
            int latencyMs,
            ConnectionState state,
            DateTime timestamp,
            int jitterMs = 0,
            double packetLoss = 0)
        {
            ServerKey = serverKey;
            LatencyMs = latencyMs;
            State = state;
            Timestamp = timestamp;
            JitterMs = jitterMs;
            PacketLoss = packetLoss;
        }
    }

    public class Result<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public string? Error { get; }

        private Result(T value)
        {
            IsSuccess = true;
            Value = value;
        }

        private Result(string error)
        {
            IsSuccess = false;
            Error = error;
        }

        public static Result<T> Success(T value) => new(value);
        public static Result<T> Failure(string error) => new(error);
    }

    #endregion

    #region Services

    public interface IServerCatalogService
    {
        Task<Result<RegionInfo>> GetServerByKeyAsync(string serverKey);
    }

    public class ServerCatalogService : IServerCatalogService
    {
        private readonly Dictionary<string, RegionInfo> _regions;

        public ServerCatalogService(ConfigRoot config)
        {
            _regions = config.RegionCatalog.Regions
                .ToDictionary(r => r.Key, r => r);
        }

        public Task<Result<RegionInfo>> GetServerByKeyAsync(string serverKey)
        {
            if (_regions.TryGetValue(serverKey, out var region))
                return Task.FromResult(Result.Success(region));

            return Task.FromResult(Result.Failure<RegionInfo>($"Region '{serverKey}' not found"));
        }
    }

    public class RegionProbeService
    {
        private readonly IServerCatalogService _serverCatalogService;
        private readonly ProbeSettings _settings;
        private readonly AsyncRetryPolicy<int> _retryPolicy;

        public RegionProbeService(
            IServerCatalogService serverCatalogService,
            ProbeSettings settings)
        {
            _serverCatalogService = serverCatalogService;
            _settings = settings;

            _retryPolicy = Policy
                .Handle<SocketException>(ex =>
                    ex.SocketErrorCode is SocketError.TimedOut
                        or SocketError.HostUnreachable
                        or SocketError.NetworkUnreachable)
                .WaitAndRetryAsync(
                    settings.RetryCount,
                    retry => TimeSpan.FromMilliseconds(settings.RetryDelayMilliseconds * retry));
        }

        public async Task<Result<ConnectionStatus>> ProbeAsync(
            string serverKey,
            CancellationToken ct = default)
        {
            var serverResult = await _serverCatalogService.GetServerByKeyAsync(serverKey);
            if (serverResult.IsFailure)
                return Result.Failure<ConnectionStatus>(serverResult.Error!);

            var best = await ProbeHostsTcpAsync(
                serverResult.Value.Hosts,
                serverResult.Value.TcpPort,
                ct);

            if (best is null)
                return Result.Failure<ConnectionStatus>("All hosts unreachable");

            return Result.Success(new ConnectionStatus(
                serverKey,
                best.LatencyMs,
                ConnectionState.Connected,
                DateTime.UtcNow,
                best.JitterMs,
                best.PacketLoss));
        }

        private async Task<(int LatencyMs, int JitterMs, double PacketLoss)?> ProbeHostsTcpAsync(
            string[] hosts,
            int port,
            CancellationToken ct)
        {
            var results = new List<int>();
            var timeout = TimeSpan.FromMilliseconds(_settings.TimeoutMilliseconds);

            foreach (var host in hosts)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeout);

                try
                {
                    var latency = await _retryPolicy.ExecuteAsync(
                        async () => await ProbeTcpAsync(host, port, cts.Token),
                        cts.Token);

                    if (latency > 0)
                        results.Add(latency);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // Host unreachable, continue with next
                }
            }

            if (results.Count < _settings.MinSuccessfulProbes)
                return null;

            var sorted = results.Order().ToList();
            return (
                LatencyMs: (int)sorted.Average(),
                JitterMs: sorted.Any() ? sorted.Last() - sorted.First() : 0,
                PacketLoss: (hosts.Length - results.Count) * 100.0 / hosts.Length
            );
        }

        private async Task<int> ProbeTcpAsync(
            string host,
            int port,
            CancellationToken ct)
        {
            using var socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);

            var sw = Stopwatch.StartNew();
            try
            {
                await socket.ConnectAsync(host, port, ct);
                sw.Stop();
                return (int)sw.ElapsedMilliseconds;
            }
            catch
            {
                return -1; // failed
            }
            finally
            {
                socket.Dispose();
            }
        }

        /// <summary>
        /// HTTP-based probe for more accurate application-level latency (TTFB)
        /// </summary>
        public async Task<int> ProbeHttpAsync(
            string url,
            CancellationToken ct = default)
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(_settings.TimeoutMilliseconds)
            };

            var sw = Stopwatch.StartNew();
            try
            {
                using var response = await client.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead,
                    ct);

                sw.Stop();
                return (int)sw.ElapsedMilliseconds;
            }
            catch
            {
                return -1;
            }
        }
    }

    #endregion

    #region Usage Example

    public class Program
    {
        public static async Task Main()
        {
            // Load config from JSON
            var config = LoadConfig("region-catalog-config.json");

            // Initialize services
            var catalogService = new ServerCatalogService(config);
            var probeService = new RegionProbeService(
                catalogService,
                config.ProbeSettings);

            // Probe specific regions
            var regionsToProbe = new[]
            {
                "Europe (Ireland)",
                "US East (N. Virginia)",
                "Asia Pacific (Tokyo)"
            };

            foreach (var regionKey in regionsToProbe)
            {
                var result = await probeService.ProbeAsync(regionKey);

                if (result.IsSuccess)
                {
                    var status = result.Value;
                    Console.WriteLine(
                        $"{regionKey}: {status.LatencyMs}ms " +
                        $"(jitter: {status.JitterMs}ms, " +
                        $"loss: {status.PacketLoss:F1}%)");
                }
                else
                {
                    Console.WriteLine($"{regionKey}: {result.Error}");
                }
            }
        }

        private static ConfigRoot LoadConfig(string path)
        {
            var json = File.ReadAllText(path);
            return System.Text.Json.JsonSerializer.Deserialize<ConfigRoot>(json)!;
        }
    }

    #endregion
}
