using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verifica (sin Play Mode) que: (1) la semilla inicial de gemas aparece
/// cerca del punto de partida, no repartida por todo el mundo grande, y
/// (2) el orbe explosivo hace estallar exactamente orbesPorExplosion gemas
/// nuevas, todas dentro de radioExplosion del punto donde se recogió. No
/// guarda la escena.
/// </summary>
public static class VerificarClustersYExplosivo
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Clusters y Explosivo")]
    public static void Verificar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();

        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");
        gm.EmpezarPartida();

        // --- 1. Semilla inicial cerca del origen ---
        var iniciales = Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None).ToList();
        float radioSemilla = gm.mitadAncho * 0.9f;
        float maxDist = iniciales.Count > 0 ? iniciales.Max(o => ((Vector2)o.transform.position).magnitude) : -1f;
        Debug.Log($"Semilla inicial: {iniciales.Count} gemas (esperado {gm.orbesObjetivo}), " +
                   $"radioSemilla={radioSemilla:F2}, distancia máxima al origen={maxDist:F2}");

        if (iniciales.Count != gm.orbesObjetivo)
            Debug.LogError($"FALLÓ: esperaba {gm.orbesObjetivo} gemas iniciales, había {iniciales.Count}");
        else if (maxDist > radioSemilla + 0.01f)
            Debug.LogError($"FALLÓ: una gema inicial quedó a {maxDist:F2}, más lejos que el radio de semilla {radioSemilla:F2}");
        else
            Debug.Log("OK: semilla inicial agrupada cerca del origen.");

        // --- 1b. GenerarOrbeCercaDe respeta el anillo mínimo/máximo ---
        var metodoGenerarCerca = typeof(GameManager).GetMethod("GenerarOrbeCercaDe", Flags);
        Vector2 centroAnillo = new Vector2(-4f, -3f);
        for (int i = 0; i < 200; i++)
            metodoGenerarCerca.Invoke(gm, new object[] { centroAnillo, gm.radioReposicionOrbe, gm.radioReposicionMinimo });

        var generadas = Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None)
            .Where(o => Vector2.Distance(o.transform.position, centroAnillo) <= gm.radioReposicionOrbe + 0.05f)
            .ToList();
        float minDist = generadas.Min(o => Vector2.Distance(o.transform.position, centroAnillo));
        float maxDist2 = generadas.Max(o => Vector2.Distance(o.transform.position, centroAnillo));
        Debug.Log($"Anillo de reposición: {generadas.Count} generadas cerca de {centroAnillo}, " +
                   $"rango pedido [{gm.radioReposicionMinimo:F2}, {gm.radioReposicionOrbe:F2}], observado [{minDist:F2}, {maxDist2:F2}]");
        if (minDist < gm.radioReposicionMinimo - 0.05f || maxDist2 > gm.radioReposicionOrbe + 0.05f)
            Debug.LogError($"FALLÓ: alguna gema quedó fuera del anillo pedido.");
        else
            Debug.Log("OK: todas las gemas de reposición cayeron dentro del anillo mínimo/máximo.");

        // --- 2. Orbe explosivo ---
        var nucleo = Object.Instantiate(gm.nucleoPrefab, Vector3.zero, Quaternion.identity);
        nucleo.radio = 0.5f;

        var idsAntes = new HashSet<int>(Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None).Select(o => o.GetInstanceID()));

        Vector2 centroExplosion = new Vector2(3f, 2f);
        var orbeExplosivo = Object.Instantiate(gm.orbePrefab, centroExplosion, Quaternion.identity);
        // Instantiate en Edit Mode no corre Awake() sincrónicamente (a
        // diferencia de Play Mode real) — sin esto, MarcarComoExplosivo
        // pisa un SpriteRenderer todavía sin asignar. Ver
        // project_workflow_sin_play_mode.
        Invocar(orbeExplosivo, "Awake");
        orbeExplosivo.MarcarComoExplosivo();

        gm.ProcesarPickupOrbe(nucleo, orbeExplosivo);

        var nuevas = Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None)
            .Where(o => !o.recolectado && !idsAntes.Contains(o.GetInstanceID()))
            .ToList();

        float maxDistExplosion = nuevas.Count > 0 ? nuevas.Max(o => Vector2.Distance(o.transform.position, centroExplosion)) : -1f;
        float minDistExplosion = nuevas.Count > 0 ? nuevas.Min(o => Vector2.Distance(o.transform.position, centroExplosion)) : -1f;
        Debug.Log($"Explosión: {nuevas.Count} gemas nuevas (esperado {gm.orbesPorExplosion}), " +
                   $"rango pedido [{gm.radioExplosionMinimo:F2}, {gm.radioExplosion:F2}], observado [{minDistExplosion:F2}, {maxDistExplosion:F2}]");

        if (nuevas.Count != gm.orbesPorExplosion)
            Debug.LogError($"FALLÓ: esperaba {gm.orbesPorExplosion} gemas de la explosión, aparecieron {nuevas.Count}");
        else if (maxDistExplosion > gm.radioExplosion + 0.01f || minDistExplosion < gm.radioExplosionMinimo - 0.01f)
            Debug.LogError($"FALLÓ: alguna gema de la explosión quedó fuera del anillo [{gm.radioExplosionMinimo:F2}, {gm.radioExplosion:F2}]");
        else
            Debug.Log("OK: la explosión generó la cantidad justa, todas dentro del anillo mínimo/máximo.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
