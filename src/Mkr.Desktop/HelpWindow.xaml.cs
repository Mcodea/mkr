using System.Windows;

namespace Mkr.Desktop;

public partial class HelpWindow : Window
{
    public HelpWindow() => InitializeComponent();

    private void CloseHelp(object sender, RoutedEventArgs args) => Close();
}
