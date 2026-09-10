using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Mide (sin Play Mode) cuánto tarda de verdad ComputarSeparacion() para
/// los ~300 núcleos que el usuario reportó que laggeaban — a diferencia de
/// otras pruebas de este proyecto, esto SÍ puede cronometrar tiempo real
/// (System.Diagnostics.Stopwatch, no Time.deltaTime) porque
/// ComputarSeparacion es una función pura sin dependencia del reloj de
/// Unity. No guarda la escena.
/// </summary>
public static class VerificarRendimientoSeparacion
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Rendimiento Separación")]
    public static void Verificar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();

        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        const int cantidad = 300;
        var nucleos = new Nucleo[cantidad];
        for (int i = 0; i < cantidad; i++)
        {
            // Agrupados en un área chica (~4x4) a propósito: es el peor
            // caso real (el enjambre persiguiendo el mismo Objetivo queda
            // compacto), no 300 esparcidos por todo el mundo.
            var pos = new Vector3(Random.Range(-2f, 2f), Random.Range(-2f, 2f), 0f);
            var n = Object.Instantiate(gm.nucleoPrefab, pos, Quaternion.identity);
            n.radio = Random.Range(0.2f, 0.6f);
            Invocar(n, "Awake");
            Invocar(n, "OnEnable");
            nucleos[i] = n;
        }

        var metodoGrid = typeof(GameManager).GetMethod("ActualizarGridSeparacion", Flags);
        var metodoSeparacion = typeof(Nucleo).GetMethod("ComputarSeparacion", Flags);

        metodoGrid.Invoke(gm, null); // reconstruye el grid una vez, como haría Update() en un frame real

        var reloj = Stopwatch.StartNew();
        for (int i = 0; i < cantidad; i++)
            metodoSeparacion.Invoke(nucleos[i], new object[] { gm, (Vector2)nucleos[i].transform.position });
        reloj.Stop();

        double msPorFrame = reloj.Elapsed.TotalMilliseconds;
        Debug.Log($"ComputarSeparacion para los {cantidad} núcleos (agrupados): {msPorFrame:F3} ms total " +
                   $"(equivalente a un frame real de separación; a 60fps hay 16.6ms de presupuesto por frame).");

        if (msPorFrame > 8.0)
            Debug.LogError($"FALLÓ (posible): {msPorFrame:F3} ms es una porción grande del presupuesto de un frame a 60fps — revisar tamanoCeldaSeparacion o considerar bajar aún más el costo.");
        else
            Debug.Log("OK: tiempo total muy por debajo del presupuesto de un frame — no debería notarse como lag.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
