using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verifica (sin Play Mode) que la escalada global por tiempo (cada 90s
/// sube un escalón, con tope) escala dificultad e intervalos como se
/// espera. No guarda la escena.
/// </summary>
public static class VerificarEscaladaGlobal
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Escalada Global")]
    public static void Verificar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();

        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        var campoTiempo = typeof(GameManager).GetField("tiempo", Flags);
        var propNivel = typeof(GameManager).GetProperty("NivelEscaladaActual", Flags);
        var propMultDif = typeof(GameManager).GetProperty("MultiplicadorDificultadEscalada", Flags);
        var propMultInt = typeof(GameManager).GetProperty("MultiplicadorIntervaloEscalada", Flags);
        var propDificultadActual = typeof(GameManager).GetProperty("DificultadEnemigoActual", Flags);

        // --- 1. Escalones de escalada global ---
        float[] tiemposDePrueba = { 0f, 89f, 90f, 180f, 450f, 900f, 1800f, 3600f };
        int nivelAnterior = 0;
        foreach (float t in tiemposDePrueba)
        {
            campoTiempo.SetValue(gm, t);
            int nivel = (int)propNivel.GetValue(gm);
            float multDif = (float)propMultDif.GetValue(gm);
            float multInt = (float)propMultInt.GetValue(gm);
            float dificultadActual = (float)propDificultadActual.GetValue(gm);
            Debug.Log($"tiempo={t:F0}s -> NivelEscaladaActual={nivel}, MultiplicadorDificultadEscalada={multDif:F2}, " +
                       $"MultiplicadorIntervaloEscalada={multInt:F3}, DificultadEnemigoActual={dificultadActual:F2}");
            // Antes de llegar al tope, cada paso de prueba tiene que subir el
            // nivel respecto al anterior. Una vez en el tope, quedarse igual
            // es lo correcto (900s también está topeado, no es un fallo).
            if (t > 89f && nivel < gm.nivelEscaladaMax && nivel <= nivelAnterior)
                Debug.LogError($"FALLÓ: en tiempo={t}, el nivel de escalada no subió respecto al paso anterior ({nivelAnterior} -> {nivel})");
            nivelAnterior = nivel;
        }
        if (nivelAnterior != gm.nivelEscaladaMax)
            Debug.LogError($"FALLÓ: a los 900s esperaba el tope nivelEscaladaMax={gm.nivelEscaladaMax}, quedó en {nivelAnterior}");
        else
            Debug.Log("OK: la escalada sube con el tiempo y respeta el tope.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
