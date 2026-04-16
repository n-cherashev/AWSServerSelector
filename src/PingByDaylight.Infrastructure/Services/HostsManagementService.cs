using System.IO;
using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using PingByDaylight.Application.Interfaces;
using PingByDaylight.Domain;

namespace PingByDaylight.Infrastructure.Services;

public class HostsManagementService : IHostsManagementService
{
    private readonly ILogger<HostsManagementService> _logger;
    private readonly string _hostsPath;
    private readonly string _backupPath;

    public HostsManagementService(ILogger<HostsManagementService> logger)
    {
        _logger = logger;
        
        // Определяем путь к hosts файлу в зависимости от ОС
        if (OperatingSystem.IsWindows())
        {
            _hostsPath = @"C:\Windows\System32\drivers\etc\hosts";
            _backupPath = @"C:\Windows\System32\drivers\etc\hosts.backup";
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            _hostsPath = "/etc/hosts";
            _backupPath = "/etc/hosts.backup";
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported operating system");
        }
    }

    public async Task<UnitResult<string>> ApplySelectionAsync(ServerSelection selection)
    {
        try
        {
            _logger.LogInformation("Applying selection: {Region}, {ApplyMode}, {BlockMode}", 
                selection.RegionKey, selection.ApplyMode, selection.BlockMode);
            
            // Создаём резервную копию
            await CreateBackupAsync();
            
            // Читаем текущий hosts файл
            var originalContent = await File.ReadAllTextAsync(_hostsPath);
            
            // Получаем все сервера из каталога
            // TODO: Inject IServerCatalogService or pass servers explicitly
            // Для sekarang используем заглушку - в реальной реализации нужно передавать список серверов
            
            // Фильтруем сервера: исключаем лучшие по пингу в каждом регионе
            var serversToBlock = FilterBestServers(selection.ServersWithPing);
            
            // Генерируем новое содержимое
            var newContent = GenerateHostsContent(originalContent, serversToBlock, selection.ApplyMode);
            
            // Записываем новый файл
            await File.WriteAllTextAsync(_hostsPath, newContent, Encoding.UTF8);
            
            _logger.LogInformation("Successfully applied hosts configuration with {Count} blocked servers", 
                serversToBlock.Count());
            
            return UnitResult.Success<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply hosts configuration");
            return UnitResult.Failure<string>($"Ошибка применения конфигурации: {ex.Message}");
        }
    }

    public async Task<UnitResult<string>> ResetToDefaultAsync()
    {
        try
        {
            _logger.LogInformation("Resetting to default configuration");
            
            // Восстанавливаем из резервной копии если она существует
            if (File.Exists(_backupPath))
            {
                await File.CopyAsync(_backupPath, _hostsPath, overwrite: true);
                _logger.LogInformation("Restored from backup");
            }
            else
            {
                // Если резервной копии нет, создаём минимальный hosts
                var defaultContent = """
                    # Copyright (c) 1993-2009 Microsoft Corp.
                    #
                    # This is a sample HOSTS file used by Microsoft TCP/IP for Windows.
                    #
                    # localhost name resolution is handled within DNS itself.
                    127.0.0.1       localhost
                    ::1             localhost
                    
                    # --- PingByDaylight entries cleared ---
                    
                    """;
                await File.WriteAllTextAsync(_hostsPath, defaultContent, Encoding.UTF8);
                _logger.LogInformation("Created default hosts file");
            }
            
            return UnitResult.Success<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset hosts configuration");
            return UnitResult.Failure<string>($"Ошибка сброса конфигурации: {ex.Message}");
        }
    }

    public async Task<Result<string>> GetHostsContentAsync()
    {
        try
        {
            var content = await File.ReadAllTextAsync(_hostsPath);
            return Result.Success(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read hosts file");
            return Result.Failure<string>($"Ошибка чтения hosts файла: {ex.Message}");
        }
    }

    private async Task CreateBackupAsync()
    {
        if (File.Exists(_hostsPath))
        {
            await File.CopyAsync(_hostsPath, _backupPath, overwrite: true);
            _logger.LogDebug("Backup created at {Path}", _backupPath);
        }
    }

    /// <summary>
    /// Фильтрует сервера, исключая лучшие по пингу в каждом регионе
    /// </summary>
    private static IEnumerable<ServerInfoWithPing> FilterBestServers(IEnumerable<ServerInfoWithPing> serversWithPing)
    {
        // Группируем по регионам
        var grouped = serversWithPing.GroupBy(s => s.ServerInfo.GroupKey);
        
        foreach (var group in grouped)
        {
            // Находим лучший сервер в группе (с минимальным пингом)
            var bestServer = group
                .Where(s => s.PingMs > 0 && s.IsReachable)
                .OrderBy(s => s.PingMs)
                .FirstOrDefault();
            
            // Возвращаем все сервера кроме лучшего
            foreach (var server in group)
            {
                // Если это лучший сервер и он имеет валидный пинг - пропускаем его
                if (bestServer != null && server.ServerInfo.Key == bestServer.ServerInfo.Key)
                {
                    continue;
                }
                
                yield return server;
            }
        }
    }

    /// <summary>
    /// Генерирует содержимое hosts файла
    /// </summary>
    private static string GenerateHostsContent(
        string originalContent, 
        IEnumerable<ServerInfoWithPing> serversToBlock, 
        ApplyMode applyMode)
    {
        var sb = new StringBuilder();
        
        // Сохраняем оригинальные строки до нашего блока
        var lines = originalContent.Split('\n');
        var inOurBlock = false;
        
        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("# --- PingByDaylight"))
            {
                inOurBlock = true;
                continue;
            }
            
            if (inOurBlock && line.Trim().StartsWith("# --- End PingByDaylight"))
            {
                inOurBlock = false;
                continue;
            }
            
            if (!inOurBlock)
            {
                sb.AppendLine(line);
            }
        }
        
        // Добавляем наш блок
        sb.AppendLine();
        sb.AppendLine("# --- PingByDaylight entries start ---");
        sb.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# Mode: {applyMode}");
        sb.AppendLine();
        
        var ipAddress = applyMode == ApplyMode.Gatekeep ? "0.0.0.0" : "127.0.0.1";
        
        foreach (var server in serversToBlock)
        {
            foreach (var host in server.ServerInfo.Hosts)
            {
                sb.AppendLine($"{ipAddress} {host}");
            }
        }
        
        sb.AppendLine();
        sb.AppendLine("# --- End PingByDaylight entries ---");
        
        return sb.ToString();
    }
}

/// <summary>
/// Вспомогательная запись для передачи информации о сервере с пингом
/// </summary>
public record ServerInfoWithPing(
    ServerInfo ServerInfo,
    int PingMs,
    bool IsReachable
);
