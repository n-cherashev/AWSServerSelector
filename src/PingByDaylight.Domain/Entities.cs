namespace PingByDaylight.Domain;

public record ServerInfo(
    string Key,
    string DisplayName,
    string GroupKey,
    string GroupDisplayName,
    string[] Hosts,
    bool IsUnstable = false
);

public record ServerSelection(
    string RegionKey,
    ApplyMode ApplyMode,
    BlockMode BlockMode,
    bool MergeUnstable
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
    DateTime LastChecked
);

public enum ConnectionState
{
    Unknown,
    Connecting,
    Connected,
    Timeout,
    Error
}
