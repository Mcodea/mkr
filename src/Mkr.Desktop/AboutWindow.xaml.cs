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
        Description.Text = ProductIdentity.Description;
        VersionLine.Text = build is null ? $"Version {number}" : $"Version {number} · compilation {build}";
        TechnicalLine.Text = $"Nom technique du projet : {ProductIdentity.TechnicalName} (dépôt, modules et formats de données inchangés).";
        Qualification.Text = ProductIdentity.QualificationNotice;
        ThirdParty.Text = "PDFsharp / MigraDoc (génération des fiches PDF) · Microsoft.Data.Sqlite et SQLitePCLRaw (stockage local) · " +
            "System.Security.Cryptography.Pkcs · composants Python du moteur de calcul listés dans engine/requirements.txt. " +
            "Les licences sont celles de chaque composant, fournies avec sa distribution.";
    }

    private void Close(object sender, RoutedEventArgs args) => base.Close();
}
