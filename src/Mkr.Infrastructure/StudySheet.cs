using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using Mkr.Core;
using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace Mkr.Infrastructure;

public static class StudySheet
{
    private static string N(double? value) => value?.ToString("G6", CultureInfo.GetCultureInfo("fr-FR")) ?? "indéterminé";

    public static void Export(string path, string project, IdentificationStudy model, PiStudy pi,
        PiSimulationStudy sim, ImportSummary training, ImportSummary validation, IReadOnlyList<ReviewEvent> reviews,
        AcquisitionOrigin? trainingOrigin = null, AcquisitionOrigin? validationOrigin = null, LoopBrief? loop = null)
    {
        if (model.Id != pi.ModelId || pi.Id != sim.PiId || training.Id != model.TrainingId ||
            validation.Id != model.ValidationId)
            throw new InvalidDataException("Les résultats proviennent de plusieurs études.");
        SimulationExportValidation.CheckSeries(sim);
        if (OperatingSystem.IsWindows()) GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        var doc = new Document();
        doc.Info.Title = "MKR — Fiche de mise au point";
        doc.Styles["Normal"]!.Font.Name = "Arial";
        var section = doc.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.Headers.Primary.AddParagraph("MKR   /   ÉTUDE DE RÉGULATION");
        section.Footers.Primary.AddParagraph($"Simulation {sim.Id}");
        void Heading(string value) => section.AddParagraph(value, "Heading2");
        void Line(string value) => section.AddParagraph(value);
        section.AddParagraph("Fiche de mise au point", "Heading1");
        Line(project);
        Line($"Simulation enregistrée le {sim.CalculatedAt:yyyy-MM-dd HH:mm:ss zzz}");
        if (loop is not null)
        {
            var fields = new[] {
                ("Repère", loop.Tag), ("Grandeur régulée", loop.ControlledQuantity),
                ("Point de fonctionnement", loop.OperatingPoint), ("Objectif", loop.Objective),
                ("Description", loop.Description)
            };
            if (fields.Any(field => !string.IsNullOrWhiteSpace(field.Item2)))
            {
                Heading("Fiche de boucle du projet au moment de l’export");
                foreach (var (label, value) in fields)
                    if (!string.IsNullOrWhiteSpace(value)) Line($"{label} : {value}");
            }
        }
        Heading("Modèle et essais");
        Line($"{model.Family} · gain {N(model.Result.Gain)} {model.PvUnit}/{model.MvUnit} · constante de temps {N(model.Result.TauSeconds)} s · retard {N(model.Result.DelaySeconds)} s");
        foreach (var (label, source, origin, columns, window, fit) in new[]
        {
            ("Estimation", training, trainingOrigin, model.TrainingColumns, model.TrainingWindow, model.Result.Training),
            ("Validation", validation, validationOrigin, model.ValidationColumns, model.ValidationWindow, model.Result.Validation)
        })
        {
            Line($"{label} · {source.FileName}");
            Line($"Temps : {columns.TimeColumn} · commande MV : {columns.MvColumn} · mesure PV : {columns.PvColumn}");
            var range = window is null ? "fichier complet"
                : $"indices {window.RowStart} à {window.RowEnd} inclus (première donnée = 0, en-tête exclu)";
            Line($"Plage utilisée : {range} · {fit.Samples} échantillons");
            Line($"RMSE : {N(fit.Rmse)} {model.PvUnit} · erreur absolue moyenne : {N(fit.Mae)} {model.PvUnit}");
            if (origin is not null)
            {
                Line(origin.Describe());
                foreach (var gap in origin.Statistics?.Gaps ?? [])
                    Line($"Lacune {gap.From:HH:mm:ss.fff}–{gap.To:HH:mm:ss.fff} UTC · {gap.MissedSamples} mesure(s) · {gap.Reason}");
            }
            Line($"SHA-256  {source.Sha256}");
        }
        if (model.Comparison is { } comparison)
        {
            Heading("Choix du modèle");
            Line($"Famille retenue : {comparison.Selected} · comparaison version {comparison.AlgorithmVersion}");
            Line($"Seuils de validation : RMSE ≤ {N(comparison.Criteria.MaximumRmse)} {model.PvUnit} · |r₁| ≤ {N(comparison.Criteria.MaximumResidualLagOne)}.");
            foreach (var (name, candidate) in comparison.Candidates)
                Line($"{name} · RMSE {N(candidate.ValidationRmse)} {model.PvUnit} · r₁ {N(candidate.ValidationResidualLagOne)} · " +
                    (candidate.RejectionReasons.Length == 0 ? "critères satisfaits" : string.Join(", ", candidate.RejectionReasons)));
        }
        Heading("Décision de validation");
        Line(model.Assessment is { } decision
            ? $"Validation indépendante version {decision.AlgorithmVersion} · RMSE ≤ {N(decision.Criteria.MaximumRmse)} {model.PvUnit} · " +
              $"|r₁| ≤ {N(decision.Criteria.MaximumResidualLagOne)} · " +
              (decision.Accepted ? "modèle accepté." : "modèle refusé : " + string.Join(", ", decision.Reasons) + ".")
            : model.Comparison?.Selected == model.Family.ToString()
                ? "Modèle accepté par la comparaison FOPDT/SOPDT ci-dessus (étude antérieure à la décision explicite)."
                : "AUCUN CRITÈRE DE VALIDATION N’A ÉTÉ APPLIQUÉ À CE MODÈLE. Les réglages ci-dessous ne sont pas utilisables.");
        Heading("Réglages PI génériques");
        Line($"Erreur = consigne − mesure. Cible de réponse {N(pi.Result.ResponseSeconds)} s.");
        foreach (var (label, coefficients, result) in new[] {
            ("IMC / Lambda", pi.Result.Imc, sim.Result.Imc), ("SIMC", pi.Result.Simc, sim.Result.Simc) })
        {
            Line($"{label} · Kp {N(coefficients.Kp)} {model.MvUnit}/{model.PvUnit} · Ti {N(coefficients.TiSeconds)} s");
            Line($"IAE {N(result.Iae)} {model.PvUnit}·s · saturation {N(result.SaturationSeconds)} s · erreur finale {N(result.FinalError)} {model.PvUnit}");
        }
        Heading("Conditions de simulation");
        var scenario = sim.Scenario;
        Line($"Consigne en écart {N(scenario.Setpoint)} {model.PvUnit} · pas {N(scenario.SampleSeconds)} s · durée {N(scenario.DurationSeconds)} s.");
        Line($"Bornes de commande {N(scenario.MinimumMv)} à {N(scenario.MaximumMv)} {model.MvUnit} · perturbation {N(scenario.Disturbance)} {model.MvUnit} à {N(scenario.DisturbanceAtSeconds)} s.");
        Line(scenario.MaximumMvRate is { } rate
            ? $"Vitesse maximale de commande {N(rate)} {model.MvUnit}/s."
            : "Aucune limite de vitesse de commande configurée.");
        Heading("Bloc et mise au point");
        Line("Correspondance constructeur non établie dans cette étude. La CPU, la version de bibliothèque, le sens d’action, les unités et les limites du bloc restent à vérifier sur banc.");
        Heading("Traçabilité");
        Line($"Modèle {model.Id} · algorithme {model.Result.AlgorithmVersion} · moteur {model.EngineVersion}");
        Line($"Réglage {pi.Id} · algorithme {pi.Result.AlgorithmVersion} · moteur {pi.EngineVersion}");
        Line($"Simulation {sim.Id} · algorithme {sim.Result.AlgorithmVersion} · moteur {sim.EngineVersion}");
        var warnings = model.Result.Warnings.Concat(pi.Result.Warnings).Concat(sim.Result.Warnings).Distinct();
        Line("Avertissements enregistrés : " + string.Join(", ", warnings));
        if (reviews.Count == 0) Line("Aucune revue humaine enregistrée pour ces résultats.");
        foreach (var review in reviews)
            Line($"{review.RecordedAt:yyyy-MM-dd HH:mm zzz} · {review.Stage} · {review.State} · {review.Reviewer} · {review.Reason} · preuve {review.Evidence}");

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var pdf = renderer.PdfDocument;
        var validationPoints = model.Result.Validation.Points;
        if (validationPoints is { Length: > 1 } && validationPoints[^1].Seconds > validationPoints[0].Seconds)
        {
            using var graph = XGraphics.FromPdfPage(pdf.AddPage());
            var font = new XFont("Arial", 10);
            graph.DrawString("Validation indépendante du modèle", new XFont("Arial", 16), XBrushes.Black, 44, 52);
            graph.DrawString($"Modèle {model.Id}", font, XBrushes.Black, 44, 74);
            graph.DrawString("Mesure observée (bleu) et prédiction (orange pointillé)", font, XBrushes.Black, 44, 100);
            var low = validationPoints.Min(p => Math.Min(p.Observed, p.Predicted));
            var high = validationPoints.Max(p => Math.Max(p.Observed, p.Predicted));
            var first = validationPoints[0].Seconds;
            var last = validationPoints[^1].Seconds;
            graph.DrawString($"PV ({model.PvUnit}) · min {N(low)} · max {N(high)}", font, XBrushes.Black, 44, 134);
            graph.DrawRectangle(XPens.Gray, 44, 148, 490, 250);
            XPoint Position(double seconds, double value) => new(
                44 + 490 * ((seconds - first) / (last - first)),
                high == low ? 273 : 398 - 250 * ((value - low) / (high - low)));
            graph.DrawLines(new XPen(XColors.SteelBlue, 1.5),
                validationPoints.Select(p => Position(p.Seconds, p.Observed)).ToArray());
            var predictionPen = new XPen(XColors.DarkOrange, 1.2) { DashStyle = XDashStyle.Dash };
            graph.DrawLines(predictionPen, validationPoints.Select(p => Position(p.Seconds, p.Predicted)).ToArray());
            graph.DrawString($"Temps : {N(first)} à {N(last)} s", font, XBrushes.Black, 44, 420);
            graph.DrawString($"RMSE : {N(model.Result.Validation.Rmse)} {model.PvUnit}", font, XBrushes.Black, 44, 446);
        }
        var page = pdf.AddPage();
        using (var g = XGraphics.FromPdfPage(page))
        {
            var font = new XFont("Arial", 10);
            g.DrawString("Réponses simulées · IMC (bleu) et SIMC (orange)", new XFont("Arial", 16), XBrushes.Black, 44, 52);
            g.DrawString($"Simulation {sim.Id}", font, XBrushes.Black, 44, 74);
            void Plot(string label, Func<SimulationPoint, double?> field, double top)
            {
                var curves = new[] { sim.Result.Imc.Points, sim.Result.Simc.Points };
                var values = curves.SelectMany(points => points.Select(field)).OfType<double>().ToArray();
                if (values.Length == 0) return;
                double lo = values.Min(), hi = values.Max(), span = hi == lo ? 1 : hi-lo;
                g.DrawString($"{label} · min {N(lo)} · max {N(hi)}", font, XBrushes.Black, 44, top-9);
                g.DrawRectangle(XPens.Gray, 44, top, 490, 190);
                for (int i=0; i<curves.Length; i++)
                {
                    var points = curves[i].Where(p => field(p).HasValue).Select(p => new XPoint(
                        44 + 490*p.Seconds/scenario.DurationSeconds, top+190-190*(field(p)!.Value-lo)/span)).ToArray();
                    if (points.Length > 1) g.DrawLines(new XPen(i==0 ? XColors.SteelBlue : XColors.DarkOrange, 1.2), points);
                }
                g.DrawString($"Temps : 0 à {N(scenario.DurationSeconds)} s", font, XBrushes.Black, 44, top+210);
            }
            Plot($"Mesure PV ({model.PvUnit})", p => p.Pv, 100);
            Plot($"Commande appliquée MV ({model.MvUnit})", p => p.Mv, 390);
        }
        var tmp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, $".{Guid.NewGuid():N}.tmp");
        try { pdf.Save(tmp); File.Move(tmp, path, overwrite: true); }
        finally { File.Delete(tmp); }
    }
}
