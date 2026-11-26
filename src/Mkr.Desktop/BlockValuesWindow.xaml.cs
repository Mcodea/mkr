using Mkr.Core;
using System.Windows;

namespace Mkr.Desktop;

public partial class BlockValuesWindow : Window
{
    internal BlockValuesWindow(string title, string text)
    {
        InitializeComponent();
        Title = title;
        Values.Text = text;
    }

    private void CopyContent(object sender, RoutedEventArgs args)
    {
        try
        {
            Clipboard.SetText(Values.Text);
            CopyStatus.Text = Loc.T("cs.copie");
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            CopyStatus.Text = Loc.T("cs.presse_papiers_indisponible_reessayez");
        }
    }
}
