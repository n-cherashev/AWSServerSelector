using PingByDaylight.Application.Interfaces;

namespace PingByDaylight.Infrastructure.Services;

public class DialogNavigationService : IDialogNavigationService
{
    public Task ShowSettingsDialogAsync()
    {
        // TODO: Implement settings dialog navigation
        return Task.CompletedTask;
    }

    public Task ShowAboutDialogAsync()
    {
        // TODO: Implement about dialog navigation
        return Task.CompletedTask;
    }

    public Task ShowUpdateDialogAsync()
    {
        // TODO: Implement update dialog navigation
        return Task.CompletedTask;
    }

    public Task ShowConnectionInfoAsync()
    {
        // TODO: Implement connection info dialog navigation
        return Task.CompletedTask;
    }
}
