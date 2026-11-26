using System.Text;
using System.Windows;
using System.Windows.Controls;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Review of one study stage under the session identity, with the four-eyes rule. Decisions are only ever appended.</summary>
public partial class ReviewWindow : Window
{
    private readonly IdentificationWorkspace _workspace;
    private readonly Guid _runId;
    private ReviewState? _current;

    internal ReviewWindow(IdentificationWorkspace workspace, Guid runId, string label)
    {
        InitializeComponent();
        _workspace = workspace;
        _runId = runId;
        Title = $"ORIAS — Revue de l'étude";
        Identity.Text = $"Étude {label} · identité de la session : {CurrentUser.Get().Label}";
        Stage.ItemsSource = Enum.GetValues<ReviewStage>();
        Stage.SelectedItem = ReviewStage.Model;
    }

    private static string StateName(ReviewState state) => state switch
    {
        ReviewState.Calculated => "Calculé", ReviewState.Verified => "Vérifié", ReviewState.Approved => "Approuvé",
        ReviewState.Rejected => "Rejeté", _ => "Non applicable"
    };

    private void StageChanged(object sender, SelectionChangedEventArgs args) => Refresh();

    private void Refresh()
    {
        if (Stage.SelectedItem is not ReviewStage stage) return;
        var history = _workspace.ReviewHistory(_runId).Where(e => e.Stage == stage).ToArray();
        _current = history.Length > 0 ? history[^1].State : null;
        StateLine.Text = _current is { } state ? $"État actuel de l'étape : {StateName(state)}" : "Aucune revue enregistrée pour cette étape.";
        Calculated.IsEnabled = _current is null;
        Verify.IsEnabled = _current is ReviewState.Calculated;
        Approve.IsEnabled = _current is ReviewState.Verified;
        Reject.IsEnabled = _current is ReviewState.Calculated or ReviewState.Verified or ReviewState.Approved;
        var text = new StringBuilder();
        foreach (var e in history)
            text.AppendLine($"{e.RecordedAt.UtcDateTime:yyyy-MM-dd HH:mm:ss}Z  {StateName(e.State),-10} {e.Reviewer}\n    motif : {e.Reason} · preuve : {e.Evidence}");
        History.Text = text.Length == 0 ? "" : text.ToString();
    }

    private void Record(object sender, RoutedEventArgs args)
    {
        if (Stage.SelectedItem is not ReviewStage stage) return;
        var next = sender == Calculated ? ReviewState.Calculated : sender == Verify ? ReviewState.Verified
            : sender == Approve ? ReviewState.Approved : ReviewState.Rejected;
        if (string.IsNullOrWhiteSpace(Reason.Text) || string.IsNullOrWhiteSpace(Evidence.Text))
        {
            Status.Text = "Renseignez le motif et la référence de preuve.";
            return;
        }
        try
        {
            _workspace.RecordReview(_runId, stage, _current, next, Reason.Text.Trim(), Evidence.Text.Trim(), SelfReview.IsChecked == true);
            Status.Text = $"Décision enregistrée : {StateName(next)}.";
            SelfReview.IsChecked = false;
            Reason.Text = "";
            Refresh();
        }
        catch (ReviewSeparationException error) { Status.Text = error.Message; }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException) { Status.Text = error.Message; Refresh(); }
    }
}
