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
        AcquisitionOrigin? trainingOrigin = null, AcquisitionOrigin? validationOrigin = null)
    {
        if (model.Id != pi.ModelId || pi.Id != sim.PiId || training.Id != model.TrainingId ||
            validation.Id != model.ValidationId)
            throw new InvalidDataException("Les résultats proviennent de plusieurs études.");
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
        Heading("Modèle et essais");
        Line($"{model.Family} · gain {N(model.Result.Gain)} {model.PvUnit}/{model.MvUnit} · constante de temps {N(model.Result.TauSeconds)} s · retard {N(model.Result.DelaySeconds)} s");
        Line($"Racine de l’erreur quadratique moyenne (RMSE) sur validation indépendante : {N(model.Result.Validation.Rmse)} {model.PvUnit}.");
        foreach (var (label, source, origin) in new[] { ("Estimation", training, trainingOrigin), ("Validation", validation, validationOrigin) })
        {
            Line($"{label} · {source.FileName}");
            if (origin is not null)
                Line($"Modbus TCP · unité {origin.Source.UnitId} · offset {origin.Source.FirstRegister} · " +
                     $"pause {origin.Source.IntervalMs} ms · horodatage UTC de réception · fin {origin.StopReason}");
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
        var page = pdf.AddPage();
        using (var g = XGraphics.FromPdfPage(page))
        {
            var font = new XFont("Arial", 10);
            g.DrawString("Réponses simulées · IMC (bleu) et SIMC (orange)", new XFont("Arial", 16), XBrushes.Black, 44, 52);
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
