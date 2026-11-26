using System.Windows;
using Mkr.Core;

namespace Mkr.Desktop;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var (number, build) = ProductIdentity.VersionOf(typeof(AboutWindow).Assembly);
        Tagline.Text = ProductIdentity.Tagline;
        Meaning.Text = $"{ProductIdentity.CommercialName} : {ProductIdentity.Meaning}.";
        Description.Text = Loc.T("product.description");
        VersionLine.Text = build is null ? Loc.T("about.version.line", number) : Loc.T("about.version.build", number, build);
        Title = ProductIdentity.Title(Loc.T("about.title"));
        TechnicalLine.Text = Loc.T("about.technical", ProductIdentity.TechnicalName);
        Qualification.Text = Loc.T("product.qualification");
        ThirdParty.Text = Loc.T("about.thirdparty.text");
    }

    private void Close(object sender, RoutedEventArgs args) => base.Close();
}
