using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Guided tuning of an accepted model: robustness objective in, tuning or an explained refusal out.</summary>
public partial class GuidedTuningWindow : Window
{
    private readonly IdentificationWorkspace _workspace;
    private readonly IdentificationStudy _model;
    private readonly CancellationTokenSource _lifetime = new();

    public sealed class Row
    {
        public string Response { get; init; } = "";
        public string Kp { get; init; } = "";
        public string Ti { get; init; } = "";
        public string Td { get; init; } = "";
        public string Ms { get; init; } = "";
        public string Verdict { get; init; } = "";
    }

    internal GuidedTuningWindow(IdentificationWorkspace workspace, IdentificationStudy model)
    {
        InitializeComponent();
        _workspace = workspace;
        _model = model;
        var result = model.Result;
        ModelLine.Text = $"Modèle {model.Family} · K = {result.Gain:G5} {model.PvUnit}/{model.MvUnit}" +
            (result.TauSeconds is { } tau ? $" · τ = {tau:G5} s" : "") + (result.SecondTauSeconds is { } fast ? $" · τ₂ = {fast:G5} s" : "") +
            $" · θ prudent = {result.TuningDelaySeconds:G5} s";
        ObjectiveChanged(this, null!);
        Closed += (_, _) => _lifetime.Cancel();
    }

    private string ObjectiveTag => (Objective.SelectedItem as ComboBoxItem)?.Tag as string ?? DesignObjectives.Balanced;

    private void ObjectiveChanged(object sender, SelectionChangedEventArgs args)
    {
        if (ObjectiveHelp is null) return;
        ObjectiveHelp.Text = ObjectiveTag switch
        {
            DesignObjectives.Robust => "Marges larges : réponse plus lente, tolère mieux les écarts de modèle et les variations du procédé.",
            DesignObjectives.Performance => "Réponse la plus rapide admise par la plage usuelle de conception : moins de marge, à réserver à un modèle bien établi.",
            _ => "Compromis courant entre rapidité et marge de stabilité."
        };
    }

    private async void Calculate(object sender, RoutedEventArgs args)
    {
        var token = _lifetime.Token;
        double? variation = null;
        if (!string.IsNullOrWhiteSpace(Variation.Text))
        {
            if (!double.TryParse(Variation.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var percent) || !(percent > 0 && percent <= 50))
            {
                Status.Text = "La variation attendue est un pourcentage supérieur à 0 et au plus 50, ou laissez le champ vide.";
                return;
            }
            variation = percent;
        }
        Compute.IsEnabled = false;
        Status.Text = "Calcul en cours : recherche du réglage le plus rapide qui respecte l'objectif sur toute l'incertitude…";
        try
        {
            var tuning = await _workspace.RecommendTuningAsync(_model, ObjectiveTag, variation, token);
            token.ThrowIfCancellationRequested();
            Show(tuning);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { Status.Text = error.Message; }
        finally { Compute.IsEnabled = true; }
    }

    private static string F(double value) => value.ToString("G5", CultureInfo.CurrentCulture);

    private void Show(GuidedTuning tuning)
    {
        Verdict.Visibility = Result.Visibility = Visibility.Visible;
        CopyButton.IsEnabled = true;
        Verdict.Background = (Brush)FindResource(tuning.IsRecommended ? "MkrSuccess" : "MkrDanger");
        VerdictText.Text = tuning.IsRecommended ? "RÉGLAGE RECOMMANDÉ (théorique)" : "RÉGLAGE REFUSÉ : les données ne le permettent pas";
        var text = new StringBuilder();
        var mv = _model.MvUnit;
        var pv = _model.PvUnit;
        text.AppendLine($"Objectif : {Objective.Text}");
        if (tuning.Recommendation is { } rec)
        {
            var c = rec.Controller;
            text.AppendLine($"Structure : {c.Structure} (forme idéale, erreur = consigne − mesure)");
            text.AppendLine($"Kp = {F(c.Kp)} {mv}/{pv}   Ti = {F(c.TiSeconds)} s" + (c.TdSeconds > 0 ? $"   Td = {F(c.TdSeconds)} s   Tf = {F(c.FilterSeconds)} s (filtre de la dérivée, Td/{F(tuning.DerivativeFilterRatio ?? 0)})" : ""));
            text.AppendLine($"Constante de temps de boucle fermée visée τc = {F(rec.ResponseSeconds)} s (retard effectif {F(tuning.EffectiveDelaySeconds)} s)");
            text.AppendLine($"Sensibilité maximale au pire cas : {F(rec.WorstPeakSensitivity)} (limite {F(tuning.MaximumPeakSensitivity)}) · pire cas : {rec.WorstCase}");
            text.AppendLine(tuning.UncertaintyQuantified
                ? "Incertitude prise en compte : " + (tuning.UncertaintyCoverageSigma is { } sigma ? $"±{sigma:G2} erreurs-types sur le gain, la constante de temps et le retard" : "aucune erreur-type disponible") +
                  (tuning.ProcessVariationPercent is { } v ? $" ; variation du procédé déclarée ±{v:G3} %." : ".")
                : "Incertitude des paramètres non quantifiée : seule l'incertitude du retard est prise en compte.");
        }
        else
        {
            text.AppendLine("Raisons :");
            foreach (var reason in tuning.Reasons ?? []) text.AppendLine("  · " + Reason(reason));
            text.AppendLine();
            text.AppendLine("Ce qu'il faut faire :");
            foreach (var action in tuning.Actions ?? []) text.AppendLine("  · " + action);
            if (tuning.ClosestTuning is { } near)
                text.AppendLine($"\nLe plus proche essayé : τc = {F(near.ResponseSeconds)} s, sensibilité au pire cas {F(near.WorstPeakSensitivity)} (limite {F(tuning.MaximumPeakSensitivity)}). Il n'est pas recommandé.");
        }
        if (tuning.Warnings is { Length: > 0 })
        {
            text.AppendLine();
            text.AppendLine("Réserves :");
            foreach (var warning in tuning.Warnings) text.AppendLine("  · " + Warning(warning));
        }
        text.AppendLine("  · résultat théorique : limites et vitesse de l'actionneur, filtre de la dérivée et bloc réel non évalués. Qualification sur installation requise.");
        Result.Text = text.ToString();
        var rows = (tuning.Alternatives ?? []).Select(a => new Row
        {
            Response = F(a.ResponseSeconds), Kp = F(a.Controller.Kp), Ti = F(a.Controller.TiSeconds), Td = a.Controller.TdSeconds > 0 ? F(a.Controller.TdSeconds) : "—",
            Ms = F(a.WorstPeakSensitivity),
            Verdict = !a.StableOnAllCorners ? "instable" : a.WorstPeakSensitivity <= tuning.MaximumPeakSensitivity + 1e-9 ? "respecte l'objectif" : "trop sensible"
        }).ToList();
        Alternatives.ItemsSource = rows;
        AlternativesTitle.Visibility = Alternatives.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Status.Text = tuning.IsRecommended ? "Réglage calculé." : "Aucun réglage recommandé.";
    }

    private static string Reason(string code) => code switch
    {
        "parameter_uncertainty_excludes_the_model" => "l'incertitude des paramètres est trop grande : le signe du gain ou une constante de temps positive n'est plus établi.",
        "no_tuning_meets_the_objective_on_the_uncertainty_set" => "aucun réglage ne respecte l'objectif de robustesse sur toute l'incertitude du modèle.",
        _ => code
    };

    private static string Warning(string code) => code switch
    {
        "uncertainty_not_quantified" => "incertitude des paramètres non quantifiée (erreurs-types indisponibles pour ce modèle).",
        "closed_loop_slower_than_the_process" => "la boucle fermée visée est plus lente que le procédé lui-même : l'incertitude impose beaucoup de marge.",
        "pid_second_pole_exceeds_delay" => "second pôle supérieur au retard : un PID est justifié (règle SIMC) ; la dérivée est filtrée (Tf = Td/10 par défaut, plage usuelle 2 à 20) et ce filtre fait partie de la boucle jugée.",
        "second_pole_uncertainty_not_propagated" => "l'incertitude du second pôle n'est pas propagée (gain, constante lente et retard le sont).",
        "half_rule_second_pole_not_above_delay" => "second pôle non supérieur au retard : règle de la moitié, puis PI.",
        "first_order_model_pi_is_the_rule" => "modèle du premier ordre : le PI est la règle SIMC.",
        "integrating_process_simc" => "règle SIMC des procédés intégrateurs.",
        _ => code
    };

    private void Copy(object sender, RoutedEventArgs args)
    {
        try { Clipboard.SetText(Result.Text); Status.Text = "Résultat copié."; }
        catch (Exception error) when (error is System.Runtime.InteropServices.ExternalException) { Status.Text = "Copie impossible : le presse-papiers est occupé."; }
    }
}
