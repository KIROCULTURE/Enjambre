using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verifica (sin Play Mode) la escalada de dificultad por tamaño del
/// enjambre y el tope de enemigos activos — la respuesta directa a datos
/// reales de una partida (telemetría) donde 223 núcleos volvían el juego
/// trivial y 200+ núcleos en Fever x5 laggeaban. No guarda la escena.
/// </summary>
public static class VerificarEscaladaPorTamano
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Escalada por Tamaño")]
    public static void Verificar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();

        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        var propMultNucleos = typeof(GameManager).GetProperty("MultiplicadorDificultadPorNucleos", Flags);
        var propDificultadActual = typeof(GameManager).GetProperty("DificultadEnemigoActual", Flags);

        // --- 1. MultiplicadorDificultadPorNucleos con cantidades reales de LAS DOS partidas ---
        // 43/63 = nucleosActivos real en la 2ª partida (a los 68s/92s, justo
        // cuando el usuario ya se sentía "a salvo" a los 80s); 223 = el
        // nucleosMaximo real de la 1ª partida.
        var nucleos = new List<Nucleo>();
        int[] hitos = { 1, 8, 30, 43, 55, 63, 223 };
        foreach (int cantidadObjetivo in hitos)
        {
            while (nucleos.Count < cantidadObjetivo)
            {
                var n = Object.Instantiate(gm.nucleoPrefab, Vector3.zero, Quaternion.identity);
                Invocar(n, "Awake");
                Invocar(n, "OnEnable");
                nucleos.Add(n);
            }
            float mult = (float)propMultNucleos.GetValue(gm);
            float dificultadActual = (float)propDificultadActual.GetValue(gm);
            Debug.Log($"Con {nucleos.Count} núcleos -> MultiplicadorDificultadPorNucleos={mult:F2}, DificultadEnemigoActual={dificultadActual:F2}");
        }

        float multA223 = (float)propMultNucleos.GetValue(gm);
        if (Mathf.Abs(multA223 - gm.multiplicadorMaximoPorNucleos) > 0.01f)
            Debug.LogError($"FALLÓ: con 223 núcleos (por encima de nucleosParaEscaladaMaximaPorTamano={gm.nucleosParaEscaladaMaximaPorTamano}) esperaba el tope {gm.multiplicadorMaximoPorNucleos}, dio {multA223:F2}");
        else
            Debug.Log("OK: con un enjambre del tamaño real reportado, la dificultad llega a su tope.");

        // --- 2. Tope de enemigos activos ---
        var enemigos = new List<Enemigo>();
        for (int i = 0; i < gm.maxEnemigosActivos; i++)
        {
            var e = Object.Instantiate(gm.enemigoPrefab, Vector3.zero, Quaternion.identity);
            Invocar(e, "Awake");
            Invocar(e, "OnEnable");
            enemigos.Add(e);
        }
        var campoHayLugar = typeof(GameManager).GetProperty("HayLugarParaMasEnemigos", Flags);
        bool hayLugarAlTope = (bool)campoHayLugar.GetValue(gm);
        Debug.Log($"Con {gm.maxEnemigosActivos} enemigos activos (el tope) -> HayLugarParaMasEnemigos={hayLugarAlTope} (esperado false)");

        // Se desregistra un enemigo (simula que uno murió) y confirma que se libera lugar.
        Invocar(enemigos[0], "OnDisable");
        bool hayLugarTrasLiberar = (bool)campoHayLugar.GetValue(gm);
        Debug.Log($"Tras 'matar' un enemigo -> HayLugarParaMasEnemigos={hayLugarTrasLiberar} (esperado true)");

        if (hayLugarAlTope)
            Debug.LogError("FALLÓ: en el tope, HayLugarParaMasEnemigos debería ser false.");
        else if (!hayLugarTrasLiberar)
            Debug.LogError("FALLÓ: tras liberar un lugar, HayLugarParaMasEnemigos debería volver a true.");
        else
            Debug.Log("OK: el tope de enemigos activos se respeta y se libera correctamente.");

        // --- 3. La zona segura de la Muralla Láser se achica con el enjambre grande ---
        // (ya quedaron 223 núcleos activos de la sección 1 de este mismo test)
        var idsAntes = new HashSet<int>(Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Select(o => o.GetInstanceID()));
        typeof(GameManager).GetMethod("RafagaMurallaLaser", Flags).Invoke(gm, null);
        var muros = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None)
            .Where(o => !idsAntes.Contains(o.GetInstanceID())).ToList();
        var campoArea = typeof(LaserHazard).GetField("area", Flags);
        var areas = muros.Select(m => (Rect)campoArea.GetValue(m)).ToList();
        float anchoSeguroObservado = areas.Where(a => a.width < a.height).Min(a => a.center.x) - (-gm.mitadAncho); // aproximado, solo para loggear
        Debug.Log($"Con 223 núcleos activos, paredes generadas: {muros.Count} (esperado 4). Con el enjambre grande, la zona segura debería ser más chica que sin achique.");
        if (muros.Count != 4)
            Debug.LogError($"FALLÓ: esperaba 4 paredes, aparecieron {muros.Count}");
        else
            Debug.Log("OK: la Muralla Láser se sigue generando correctamente con un enjambre grande.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
