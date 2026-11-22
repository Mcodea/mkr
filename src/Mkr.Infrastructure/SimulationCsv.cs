using System.Text;
using Mkr.Core;

namespace Mkr.Infrastructure;

public static class SimulationCsv
{
    public static void Export(string path, PiSimulationStudy simulation)
    {
        SimulationExportValidation.CheckSeries(simulation);
        var imc = simulation.Result.Imc.Points;
        var simc = simulation.Result.Simc.Points;

        var destination = Path.GetFullPath(path);
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".{Guid.NewGuid():N}.tmp");
        try
        {
            using (var writer = new StreamWriter(temporary, false, new UTF8Encoding(false)) { NewLine = "\r\n" })
            {
                writer.WriteLine("time_s,sp_delta,imc_pv,imc_cv,imc_mv,simc_pv,simc_cv,simc_mv,disturbance_mv");
                for (int i = 0; i < imc.Length; i++)
                {
                    var left = imc[i];
                    var right = simc[i];
                    writer.WriteLine(FormattableString.Invariant(
                        $"{left.Seconds:G17},{simulation.Scenario.Setpoint:G17},{left.Pv:G17},{left.Cv:G17},{left.Mv:G17},{right.Pv:G17},{right.Cv:G17},{right.Mv:G17},{left.Disturbance:G17}"));
                }
            }
            File.Move(temporary, destination, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }
}
