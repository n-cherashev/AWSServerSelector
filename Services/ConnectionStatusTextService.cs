using AWSServerSelector.Services.Interfaces;

namespace AWSServerSelector.Services;

public sealed class ConnectionStatusTextService : IConnectionStatusTextService
{
    public string BuildMatchStatusText(string baseStatus, bool npcapWorking, bool npcapUnavailable)
    {
        var npcapText = npcapWorking
            ? "NPCap OK"
            : npcapUnavailable
                ? "NPCap недоступен"
                : "NPCap неизвестно";
        return $"{baseStatus} ({npcapText})";
    }
}
