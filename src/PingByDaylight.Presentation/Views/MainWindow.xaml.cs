using System.Windows;
using PingByDaylight.Application.ViewModels;

namespace PingByDaylight.Presentation.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
