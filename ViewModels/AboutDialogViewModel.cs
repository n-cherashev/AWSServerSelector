using CommunityToolkit.Mvvm.ComponentModel;

namespace AWSServerSelector.ViewModels;

public partial class AboutDialogViewModel : ObservableObject
{
    public string DialogTitle => "О программе";

    [ObservableProperty]
    private string aboutText = string.Empty;

    [ObservableProperty]
    private string developer = string.Empty;

    [ObservableProperty]
    private string versionText = string.Empty;

    [ObservableProperty]
    private string awesomeText = string.Empty;

    public void Configure(string currentVersion)
    {
        AboutText = "Менеджер серверов для Dead by Daylight";
        Developer = "Разработчик: Wafphlez";
        VersionText = $"Версия: {currentVersion}";
        AwesomeText = "Приятной игры!";
        OnPropertyChanged(nameof(DialogTitle));
    }
}
