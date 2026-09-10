using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Verifica (sin Play Mode) el sistema de orbes escasos + barra de energía
/// de Nivel 2 (Fase 3 del rediseño grande a boss fight). El daño real al
/// boss que dispara la barra llena se prueba aparte, de punta a punta, en
/// VerificarVidaBossNivel2.cs (Fase 4) — acá el foco es orbes/imán/carga.
/// Todo lo que prueba acá es síncrono de punta a punta:
/// ActualizarOrbesNivel2/GenerarOrbeNivel2/ProcesarPickupOrbeNivel2/
/// DescargarPulsoNivel2 no son corrutinas, así que no hay límite de "un
/// solo yield" como en el resto de la pelea.
///
/// CUIDADO CON DESTROY DIFERIDO: Orbe.recolectado (no la destrucción real
/// del GameObject) es la señal que usa ActualizarOrbesNivel2 para no
/// contar dos veces un orbe ya levantado en el mismo frame — mismo patrón
/// que Nivel 1. Los conteos de este archivo filtran por recolectado==false
/// en vez de contar GameObjects crudos, por la misma razón que el resto
/// del proyecto usa DestroyImmediate para limpiar antes de una captura.
/// </summary>
public static class VerificarOrbesEnergiaNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    // ActualizarOrbesNivel2 ahora toma dt explícito (no Time.deltaTime
    // adentro — no controlable en batch mode, ver el comentario en
    // GameManager.cs). Un dt grande hace el movimiento del imán bien
    // visible en un solo tick sin depender de ninguna corrida real.
    const float DtSintetico = 1f;

    [MenuItem("Enjambre/Debug/Verificar Orbes y Energía Nivel 2")]
    public static void Verificar()
    {
        var gmChequeo = AbrirEscenaFresca();
        if (gmChequeo.administradorPrefab == null || gmChequeo.formaPrecisaPrefab == null)
        {
            Debug.LogError("FALLÓ: falta administradorPrefab o formaPrecisaPrefab en GameManager — correr antes 'Crear Administrador del Sistema' y 'Crear Prefab Forma Precisa'.");
            return;
        }

        PruebaArranquePeleaGeneraOrbesEscasos();
        PruebaImanAcercaSinRecolectar();
        PruebaRecoleccionCargaEnergiaYAura();
        PruebaBarraLlenaDisparaPulsoYResetea();
        PruebaReponeEscasezTrasRecolectar();
        PruebaGeneraLejosDelJugador();

        Debug.Log("Verificación de orbes/energía de Nivel 2 completa.");
    }

    static GameManager AbrirEscenaFresca()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        gm.estado = EstadoJuego.Menu;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");
        return gm;
    }

    static FormaPrecisa CrearFormaPrecisaDePrueba(GameManager gm, Vector2 pos)
    {
        var fp = Object.Instantiate(gm.formaPrecisaPrefab, pos, Quaternion.identity);
        Invocar(fp, "Awake");
        typeof(GameManager).GetField("formaPrecisaActiva", Flags).SetValue(gm, fp);
        return fp;
    }

    static int OrbesNoRecolectados() => Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None).Count(o => !o.recolectado);

    static float LeerEnergia(GameManager gm) => (float)typeof(GameManager).GetField("energiaNivel2", Flags).GetValue(gm);
    static void EscribirEnergia(GameManager gm, float v) => typeof(GameManager).GetField("energiaNivel2", Flags).SetValue(gm, v);

    /// <summary>Arranca la pelea real (cutscene ya "vista" en sesión, mismo camino que PruebaArranqueYDerrotaSincronos de VerificarPeleaNivel2.cs) para probar el spawn inicial tal cual lo dispara IniciarPeleaNivel2.</summary>
    static void PruebaArranquePeleaGeneraOrbesEscasos()
    {
        var gm = AbrirEscenaFresca();
        typeof(GameManager).GetField("cutsceneAperturaVistaSesion", Flags).SetValue(gm, true);
        gm.BotonJugarNivel2();

        int activos = OrbesNoRecolectados();
        Debug.Log($"Tras arrancar la pelea: orbes activos={activos} (esperado {gm.orbesObjetivoNivel2}), energiaNivel2={LeerEnergia(gm)} (esperado 0)");
        if (activos != gm.orbesObjetivoNivel2 || LeerEnergia(gm) != 0f)
            Debug.LogError("FALLÓ: IniciarPeleaNivel2 debería dejar exactamente orbesObjetivoNivel2 orbes activos y la barra en 0.");
        else
            Debug.Log("OK: la pelea arranca con la cantidad escasa exacta de orbes, barra vacía.");
    }

    static void PruebaImanAcercaSinRecolectar()
    {
        var gm = AbrirEscenaFresca();
        var fp = CrearFormaPrecisaDePrueba(gm, Vector2.zero);

        // A mitad de camino entre el radio de imán y el de recolección —
        // debería acercarse, pero no levantarse todavía en un solo tick.
        float distInicial = (gm.radioImanNivel2 + gm.radioRecoleccionNivel2) * 0.5f;
        var o = Object.Instantiate(gm.orbePrefab, new Vector3(distInicial, 0f, 0f), Quaternion.identity);
        Invocar(o, "Awake");

        var metodo = typeof(GameManager).GetMethod("ActualizarOrbesNivel2", Flags);
        metodo.Invoke(gm, new object[] { DtSintetico });

        float distFinal = Vector2.Distance(o.transform.position, fp.transform.position);
        Debug.Log($"Imán — distancia inicial={distInicial:F3}, final={distFinal:F3} (esperado menor), recolectado={o.recolectado} (esperado false)");
        if (distFinal >= distInicial || o.recolectado)
            Debug.LogError("FALLÓ: dentro del radio de imán, el orbe debería acercarse sin recolectarse todavía.");
        else
            Debug.Log("OK: el imán acerca el orbe sin recolectarlo de un tick al otro.");
    }

    static void PruebaRecoleccionCargaEnergiaYAura()
    {
        var gm = AbrirEscenaFresca();
        var fp = CrearFormaPrecisaDePrueba(gm, Vector2.zero);
        float energiaAntes = LeerEnergia(gm);

        var o = Object.Instantiate(gm.orbePrefab, new Vector3(gm.radioRecoleccionNivel2 * 0.5f, 0f, 0f), Quaternion.identity);
        Invocar(o, "Awake");

        var metodo = typeof(GameManager).GetMethod("ActualizarOrbesNivel2", Flags);
        metodo.Invoke(gm, new object[] { DtSintetico });

        float energiaDespues = LeerEnergia(gm);
        var srAura = (SpriteRenderer)typeof(FormaPrecisa).GetField("srAura", Flags).GetValue(fp);
        float fraccionEsperada = gm.FraccionEnergiaNivel2;
        Debug.Log($"Recolección — recolectado={o.recolectado} (esperado true), energia {energiaAntes}->{energiaDespues} (esperado +{gm.cargaPorOrbeNivel2}), aura.alpha={(srAura != null ? srAura.color.a : -1f):F3} (esperado > 0, ~{Mathf.Lerp(0f, 0.8f, fraccionEsperada):F3})");
        if (!o.recolectado || energiaDespues != energiaAntes + gm.cargaPorOrbeNivel2 || srAura == null || srAura.color.a <= 0f)
            Debug.LogError("FALLÓ: recolectar un orbe dentro del radio real debería sumar carga y encender la aura de FormaPrecisa.");
        else
            Debug.Log("OK: recolectar suma la carga esperada y la aura (la señal PRINCIPAL, no el HUD) refleja la fracción cargada.");
    }

    static void PruebaBarraLlenaDisparaPulsoYResetea()
    {
        var gm = AbrirEscenaFresca();
        var fp = CrearFormaPrecisaDePrueba(gm, Vector2.zero);
        // Deja la barra a un solo orbe de llenarse, así el pickup real de
        // abajo es el que cruza el umbral — prueba la descarga de verdad,
        // no un seteo directo a mano.
        EscribirEnergia(gm, gm.energiaMaxNivel2 - gm.cargaPorOrbeNivel2);

        var o = Object.Instantiate(gm.orbePrefab, new Vector3(gm.radioRecoleccionNivel2 * 0.5f, 0f, 0f), Quaternion.identity);
        Invocar(o, "Awake");
        var metodo = typeof(GameManager).GetMethod("ActualizarOrbesNivel2", Flags);
        metodo.Invoke(gm, new object[] { DtSintetico });

        float energiaFinal = LeerEnergia(gm);
        Debug.Log($"Tras cruzar el umbral: energiaNivel2={energiaFinal} (esperado 0 — se descarga y resetea sola)");
        if (energiaFinal != 0f)
            Debug.LogError("FALLÓ: al llegar a energiaMaxNivel2 debería descargar el pulso y volver a 0, no quedarse llena.");
        else
            Debug.Log("OK: la barra se descarga sola al llenarse (el daño real al boss se prueba aparte, en VerificarVidaBossNivel2.cs).");
    }

    static void PruebaReponeEscasezTrasRecolectar()
    {
        var gm = AbrirEscenaFresca();
        var fp = CrearFormaPrecisaDePrueba(gm, Vector2.zero);
        for (int i = 0; i < gm.orbesObjetivoNivel2; i++)
            Invocar(Object.Instantiate(gm.orbePrefab, new Vector3(3f + i, 3f, 0f), Quaternion.identity), "Awake");

        var metodo = typeof(GameManager).GetMethod("ActualizarOrbesNivel2", Flags);
        // Primer tick: ninguno está en rango de FormaPrecisa (están lejos,
        // en 3..3+n) — solo confirma que con el objetivo ya cubierto no
        // genera de más.
        metodo.Invoke(gm, new object[] { DtSintetico });
        int activosAntes = OrbesNoRecolectados();

        // Ahora fuerza la recolección de uno solo, poniéndolo encima del
        // jugador, y vuelve a tickear — debería reponerlo.
        var todos = Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None);
        todos[0].transform.position = Vector2.zero;
        metodo.Invoke(gm, new object[] { DtSintetico });
        int activosDespues = OrbesNoRecolectados();

        Debug.Log($"Escasez — activos antes={activosAntes} (esperado {gm.orbesObjetivoNivel2}), tras recolectar uno y re-tickear={activosDespues} (esperado {gm.orbesObjetivoNivel2} de nuevo, repuesto)");
        if (activosAntes != gm.orbesObjetivoNivel2 || activosDespues != gm.orbesObjetivoNivel2)
            Debug.LogError("FALLÓ: la cantidad de orbes activos debería mantenerse siempre en orbesObjetivoNivel2, reponiendo lo recolectado.");
        else
            Debug.Log("OK: recolectar uno lo repone al toque — nunca quedan menos orbes de los escasos que se definieron.");
    }

    static void PruebaGeneraLejosDelJugador()
    {
        var gm = AbrirEscenaFresca();
        var fp = CrearFormaPrecisaDePrueba(gm, Vector2.zero);
        var metodo = typeof(GameManager).GetMethod("GenerarOrbeNivel2", Flags);
        float distanciaMinima = Mathf.Min(gm.mitadAncho, gm.mitadAlto) * 0.7f;

        int total = 20, cumplen = 0;
        for (int i = 0; i < total; i++)
        {
            metodo.Invoke(gm, null);
            var nuevo = Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None).Last();
            float dist = Vector2.Distance(nuevo.transform.position, fp.transform.position);
            if (dist >= distanciaMinima - 0.01f) cumplen++;
            Object.DestroyImmediate(nuevo.gameObject); // limpio entre samples — cada llamada debe evaluarse contra la MISMA posición del jugador
        }

        Debug.Log($"Distancia mínima al generar ({distanciaMinima:F2}): {cumplen}/{total} muestras la respetaron (se tolera alguna falla — hay un fallback de último intento).");
        if (cumplen < total - 2)
            Debug.LogError("FALLÓ: GenerarOrbeNivel2 debería casi siempre generar lejos del jugador (objetivo de diseño: obligar a moverse por toda la arena).");
        else
            Debug.Log("OK: los orbes nuevos aparecen lejos de donde está parado el jugador, no al lado.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
