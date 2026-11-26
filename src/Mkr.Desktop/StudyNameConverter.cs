using System.Globalization;
using System.Windows.Data;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Readable study name for the screens (family, local date and time in the user's regional format, decision): no technical identifier.</summary>
internal sealed class StudyNameConverter : IValueConverter
{
    public static string Name(IdentificationStudy study)
    {
        var family = study.Family == ModelFamily.Integrateur ? Loc.T("cs.integrateur_2") : study.Family.ToString();
        var decision = study.Assessment is { Accepted: true } ? Loc.T("cs.accepte") : study.Assessment is not null ? Loc.T("cs.refuse") : Loc.T("cs.non_decide_2");
        return $"{family} · {study.CalculatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)} · {decision}";
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is IdentificationStudy study ? Name(study) : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
