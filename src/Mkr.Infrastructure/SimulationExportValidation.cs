using Mkr.Core;

namespace Mkr.Infrastructure;

internal static class SimulationExportValidation
{
    internal static void CheckSeries(PiSimulationStudy simulation)
    {
        var imc = simulation.Result.Imc.Points;
        var simc = simulation.Result.Simc.Points;
        if (imc.Length == 0 || imc.Length != simc.Length)
            throw new InvalidDataException("Les séries IMC et SIMC doivent contenir le même nombre de points.");

        for (int i = 0; i < imc.Length; i++)
        {
            double time = imc[i].Seconds;
            if (!double.IsFinite(time) || (i > 0 && time <= imc[i - 1].Seconds) ||
                time != simc[i].Seconds || imc[i].Disturbance != simc[i].Disturbance)
                throw new InvalidDataException("Vérifiez les horodatages et les perturbations des séries IMC et SIMC.");
        }
    }
}
