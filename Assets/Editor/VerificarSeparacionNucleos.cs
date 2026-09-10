using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verifica (sin Play Mode) la separación tipo boids y el achique por
/// cantidad de núcleos. Como el movimiento real depende de Time.deltaTime
/// (que no avanza fuera de Play Mode, ver project_workflow_sin_play_mode),
/// esto prueba las dos funciones puras involucradas directamente —
/// ComputarSeparacion() con posiciones fijas, y FactorEscalaPorCantidad()/
/// AplicarEscala() con una cantidad real de núcleos activos — en vez de
/// simular el paso del tiempo. No guarda la escena.
/// </summary>
public static class VerificarSeparacionNucleos
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Separación Núcleos")]
    public static void Verificar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();

        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");

        // --- 1. FactorEscalaPorCantidad con distintas cantidades reales ---
        int[] cantidades = { 1, 6, 23, 40, 60 };
        var nucleos = new System.Collections.Generic.List<Nucleo>();
        int previos = 0;
        foreach (int cantidadObjetivo in cantidades)
        {
            while (nucleos.Count < cantidadObjetivo)
            {
                var n = Object.Instantiate(gm.nucleoPrefab, Vector3.zero, Quaternion.identity);
                n.radio = 0.5f;
                Invocar(n, "Awake");
                Invocar(n, "OnEnable"); // registra en GameManager.nucleosActivos
                nucleos.Add(n);
            }
            float factor = gm.FactorEscalaPorCantidad;
            Debug.Log($"Con {nucleos.Count} núcleos activos -> FactorEscalaPorCantidad={factor:F3}");
            previos = nucleos.Count;
        }

        if (Mathf.Abs(gm.FactorEscalaPorCantidad - gm.factorEscalaMinimoPorCantidad) > 0.001f)
            Debug.LogError($"FALLÓ: con {nucleos.Count} núcleos (bien por encima de cantidadParaEscalaMinima={gm.cantidadParaEscalaMinima}) esperaba el piso {gm.factorEscalaMinimoPorCantidad}, dio {gm.FactorEscalaPorCantidad:F3}");
        else
            Debug.Log("OK: el achique llega al piso configurado y no sigue bajando.");

        // AplicarEscala real, con 60 núcleos activos: la escala debería
        // quedar en radio*2*factorPiso, no en radio*2 sin achicar.
        var primero = nucleos[0];
        Invocar(primero, "AplicarEscala");
        float escalaEsperada = primero.radio * 2f * gm.factorEscalaMinimoPorCantidad;
        Debug.Log($"AplicarEscala con {nucleos.Count} activos: localScale.x={primero.transform.localScale.x:F3} (esperado ~{escalaEsperada:F3})");
        if (Mathf.Abs(primero.transform.localScale.x - escalaEsperada) > 0.01f)
            Debug.LogError("FALLÓ: AplicarEscala no aplicó el factor de achique por cantidad.");
        else
            Debug.Log("OK: AplicarEscala aplica el achique por cantidad a la escala real (la que lleva el collider).");

        // Limpieza: saca todos menos 2, para la prueba de separación de abajo.
        for (int i = nucleos.Count - 1; i >= 2; i--)
        {
            Invocar(nucleos[i], "OnDisable");
            Object.DestroyImmediate(nucleos[i].gameObject);
            nucleos.RemoveAt(i);
        }

        // --- 2. ComputarSeparacion con dos núcleos casi encimados ---
        var a = nucleos[0]; var b = nucleos[1];
        a.transform.position = Vector3.zero;
        b.transform.position = new Vector3(0.05f, 0f, 0f);
        a.radio = 0.5f; b.radio = 0.5f;

        // ComputarSeparacion ya no recorre nucleosActivos directo — usa el
        // grid espacial (VecinosCercanos), que en el juego real reconstruye
        // GameManager.Update() cada frame. Sin esto el grid queda vacío
        // (recién construido en Awake, antes de mover a/b a 0.05 de
        // distancia) y la separación da 0 — no porque el código esté mal,
        // sino porque el test no reconstruyó el grid después de reposicionar.
        Invocar(gm, "ActualizarGridSeparacion");

        var metodoSeparacion = typeof(Nucleo).GetMethod("ComputarSeparacion", Flags);
        Vector2 separacion = (Vector2)metodoSeparacion.Invoke(a, new object[] { gm, (Vector2)a.transform.position });

        float distMinimaEsperada = (a.radio + b.radio) * gm.margenSeparacionNucleos * gm.FactorEscalaPorCantidad;
        float magnitudEsperada = distMinimaEsperada - 0.05f;
        Debug.Log($"ComputarSeparacion (a 0.05 de distancia, distMinima={distMinimaEsperada:F2}): " +
                   $"separacion={separacion} (magnitud {separacion.magnitude:F3}, esperada ~{magnitudEsperada:F3}), dirección hacia -X esperada");

        if (separacion.magnitude < 0.01f)
            Debug.LogError("FALLÓ: dos núcleos casi encimados no generaron ninguna fuerza de separación.");
        else if (separacion.x >= 0f)
            Debug.LogError($"FALLÓ: la separación debería empujar a 'a' hacia -X (b está a su +X), dio {separacion}");
        else if (Mathf.Abs(separacion.magnitude - magnitudEsperada) > 0.05f)
            Debug.LogError($"FALLÓ: magnitud de separación {separacion.magnitude:F3} no coincide con la esperada {magnitudEsperada:F3}");
        else
            Debug.Log("OK: ComputarSeparacion empuja en la dirección y magnitud correctas.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
