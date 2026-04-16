using System.Windows.Controls;

namespace PingByDaylight.Presentation.Components;

public partial class HeaderMenuComponent : UserControl
{
    public Button MenuButton => HeaderMenuBtn;

    public HeaderMenuComponent()
    {
        InitializeComponent();
    }
}
