using System.Globalization;
using System.Windows.Data;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Readable study name for the screens (family, local date and time in the user's regional format, decision): no technical identifier.</summary>
internal sealed class StudyNameConverter : IValueConverter
{
    public static string Name(IdentificationStudy study)
    {
        var family = study.Family == ModelFamily.Integrateur ? "Intégrateur" : study.Family.ToString();
        var decision = study.Assessment is { Accepted: true } ? "accepté" : study.Assessment is not null ? "refusé" : "non décidé";
        return $"{family} · {study.CalculatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)} · {decision}";
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is IdentificationStudy study ? Name(study) : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
