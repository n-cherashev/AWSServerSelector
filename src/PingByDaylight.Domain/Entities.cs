namespace PingByDaylight.Domain;

public record ServerInfo(
    string Key,
    string DisplayName,
    string GroupKey,
    string GroupDisplayName,
    string[] Hosts,
    bool IsUnstable = false
);

/// <summary>
/// Вспомогательная запись для передачи информации о сервере с пингом
/// </summary>
public record ServerInfoWithPing(
    ServerInfo ServerInfo,
    int PingMs,
    bool IsReachable
);

public record ServerSelection(
    string RegionKey,
    ApplyMode ApplyMode,
    BlockMode BlockMode,
    bool MergeUnstable,
    IEnumerable<ServerInfoWithPing> ServersWithPing = null!
);

public enum ApplyMode
{
    Gatekeep,
    Redirect
}

public enum BlockMode
{
    Inbound,
    Outbound,
    Both
}

public record ConnectionStatus(
    string ServerKey,
    int LatencyMs,
    ConnectionState State,
    DateTime LastChecked,
    int JitterMs = 0,
    double PacketLossPercent = 0.0
);

public enum ConnectionState
{
    Unknown,
    Connecting,
    Connected,
    Timeout,
    Error
}
