using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verifica (sin Play Mode): (1) que Telemetria escribe un CSV real y
/// legible con las filas esperadas durante una partida simulada, y (2) que
/// el evento Muralla Láser ahora dispara por tiempo (no por combo) y su
/// intervalo se acorta con la escalada global, tal como
/// GameManager.ActualizarEventoLaser especifica. No guarda la escena.
/// </summary>
public static class VerificarTelemetriaYLaserPorTiempo
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Telemetría y Láser por Tiempo")]
    public static void Verificar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();

        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        // --- 1. Telemetria: EmpezarPartida crea el archivo y registra "inicio" ---
        gm.EmpezarPartida();

        string dirTelemetria = Path.Combine(Application.dataPath, "..", "..", "capturas", "telemetria");
        var archivos = Directory.Exists(dirTelemetria)
            ? Directory.GetFiles(dirTelemetria, "partida_*.csv").OrderByDescending(f => f).ToList()
            : new List<string>();
        Debug.Log($"Archivos de telemetría encontrados: {archivos.Count} (esperado >= 1) en {dirTelemetria}");
        if (archivos.Count == 0) { Debug.LogError("FALLÓ: no se creó ningún archivo de telemetría."); return; }

        string archivoActual = archivos[0];

        // --- 2. Simular unas cuantas gemas para generar filas "gema"/"fever_hito" ---
        var nucleo = Object.Instantiate(gm.nucleoPrefab, Vector3.zero, Quaternion.identity);
        nucleo.radio = 0.5f;
        for (int i = 0; i < 20; i++)
        {
            var orbe = Object.Instantiate(gm.orbePrefab, Vector3.zero, Quaternion.identity);
            gm.ProcesarPickupOrbe(nucleo, orbe);
        }

        // --- 3. Forzar el evento Muralla Láser por tiempo (no por combo) ---
        var campoTProximo = typeof(GameManager).GetField("tProximoEventoLaser", Flags);
        var metodoActualizar = typeof(GameManager).GetMethod("ActualizarEventoLaser", Flags);
        campoTProximo.SetValue(gm, -0.01f); // "ya venció" — dispara en la próxima llamada

        var idsAntes = new HashSet<int>(Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Select(o => o.GetInstanceID()));
        metodoActualizar.Invoke(gm, null);
        var nuevosMuros = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None)
            .Where(o => !idsAntes.Contains(o.GetInstanceID())).ToList();

        float nuevoIntervalo = (float)campoTProximo.GetValue(gm);
        Debug.Log($"ActualizarEventoLaser: generó {nuevosMuros.Count} paredes (esperado 4), " +
                   $"tProximoEventoLaser quedó en {nuevoIntervalo:F2} (esperado {gm.intervaloEventoLaser:F2} a escalada tier 1)");

        if (nuevosMuros.Count != 4)
            Debug.LogError($"FALLÓ: esperaba 4 paredes nuevas al forzar el evento, aparecieron {nuevosMuros.Count}");
        else if (Mathf.Abs(nuevoIntervalo - gm.intervaloEventoLaser) > 0.01f)
            Debug.LogError($"FALLÓ: el intervalo no se reseteó a intervaloEventoLaser ({gm.intervaloEventoLaser}), quedó en {nuevoIntervalo}");
        else
            Debug.Log("OK: el evento Muralla Láser dispara por tiempo y reprograma su propio intervalo.");

        // --- 4. Cerrar y leer el CSV de telemetría ---
        Telemetria.CerrarSiHabiaAlguna();
        string[] lineas = File.ReadAllLines(archivoActual);
        Debug.Log($"CSV de telemetría: {lineas.Length} líneas (encabezado + eventos).");
        Debug.Log("Encabezado: " + lineas[0]);
        int filasGema = lineas.Count(l => l.Contains(",gema,"));
        int filasFeverHito = lineas.Count(l => l.Contains(",fever_hito,"));
        int filasLaser = lineas.Count(l => l.Contains(",laser_evento,"));
        Debug.Log($"Filas 'gema'={filasGema} (esperado 20), 'fever_hito'={filasFeverHito} (esperado 1, cada 20 combos), 'laser_evento'={filasLaser} (esperado 1)");

        if (lineas[0] != "tiempo,evento,nivelFever,comboOrbes,nucleosActivos,orbesActivos,detalle")
            Debug.LogError("FALLÓ: el encabezado del CSV no es el esperado.");
        else if (filasGema != 20 || filasFeverHito != 1 || filasLaser != 1)
            Debug.LogError("FALLÓ: no aparecieron todas las filas esperadas en el CSV.");
        else
            Debug.Log("OK: el CSV de telemetría se escribe y se puede leer de vuelta con las filas esperadas.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
