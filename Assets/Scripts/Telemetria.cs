using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Registra en un CSV lo que pasa durante una partida real (jugada por el
/// usuario, no simulada en batch mode) para poder analizar el balance con
/// datos en vez de a ojo — GameManager llama a estos métodos en los puntos
/// clave (inicio de partida, cada gema recogida, subida/bajada de Fever,
/// núcleo partido/perdido, evento de Muralla Láser, fin de partida).
///
/// Un archivo nuevo por partida en capturas/telemetria/, mismo directorio
/// "fácil de encontrar" que ya usan las capturas de pantalla de este
/// proyecto (Application.dataPath/../../capturas). Cada fila se escribe Y
/// se vacía al disco al toque (no se guarda en un buffer hasta el final)
/// para no perder nada si la partida termina en un crash en vez de un
/// TerminarPartida() normal.
/// </summary>
public static class Telemetria
{
    static StreamWriter escritor;

    public static void IniciarPartida()
    {
        CerrarSiHabiaAlguna();
        try
        {
            string dir = Path.Combine(Application.dataPath, "..", "..", "capturas", "telemetria");
            Directory.CreateDirectory(dir);
            string ruta = Path.Combine(dir, $"partida_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            escritor = new StreamWriter(ruta, append: false);
            escritor.WriteLine("tiempo,evento,nivelFever,comboOrbes,nucleosActivos,orbesActivos,detalle");
            escritor.Flush();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Telemetria: no pude crear el archivo de esta partida — " + e.Message);
            escritor = null;
        }
    }

    public static void Registrar(float tiempo, string evento, int nivelFever, int comboOrbes, int nucleosActivos, int orbesActivos, string detalle = "")
    {
        if (escritor == null) return;
        try
        {
            escritor.WriteLine($"{tiempo:F2},{evento},{nivelFever},{comboOrbes},{nucleosActivos},{orbesActivos},{detalle}");
            escritor.Flush();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Telemetria: fallo al escribir — " + e.Message);
        }
    }

    public static void CerrarSiHabiaAlguna()
    {
        if (escritor == null) return;
        try { escritor.Flush(); escritor.Close(); } catch { /* ya se va a cerrar solo si esto falla */ }
        escritor = null;
    }
}
