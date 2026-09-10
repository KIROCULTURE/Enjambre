using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Verifica (sin Play Mode) la Fase 2 Super Administrador (punto 8 del
/// rediseño grande a boss fight): solo patrones de láser, reusados sin
/// tocar del viejo Show de Láseres (PatronBarridoSimple/Cruz/Corredor/
/// Abanico, ver VerificarPeleaNivel2.cs para los tests de esos 4 en sí),
/// más agresivos hacia el clímax de la canción (112-128s). El driver real
/// (FaseLaseresYVictoriaNivel2) es una corrutina con un while — se prueba
/// por partes puras (EsperaEntrePatronesFase2Nivel2/EsperaClampeadaFase2Nivel2)
/// más un disparo real de un patrón, mismo límite de siempre con
/// corrutinas en batch mode.
/// </summary>
public static class VerificarFase2LaseresNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Fase 2 Láseres Nivel 2")]
    public static void Verificar()
    {
        PruebaEsperaEscalaEntreTramoYClimax();
        PruebaEsperaClampeadaRespetaElFinalDeLaCancion();
        PruebaDispararPatronNoLanzaExcepcionYCreaUnMuro();
        PruebaImpactoDuranteFase2EtiquetaCorrectamente();
        PruebaFase2NoArrancaAntesDelFinalDelBreakdown();

        Debug.Log("Verificación de la Fase 2 (Super Administrador, láseres) de Nivel 2 completa.");
    }

    static GameManager AbrirEscenaFresca()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");
        return gm;
    }

    static void PruebaEsperaEscalaEntreTramoYClimax()
    {
        var gm = AbrirEscenaFresca();
        var metodo = typeof(GameManager).GetMethod("EsperaEntrePatronesFase2Nivel2", Flags);

        float sinClimax = (float)metodo.Invoke(gm, new object[] { false });
        float conClimax = (float)metodo.Invoke(gm, new object[] { true });

        Debug.Log($"EsperaEntrePatronesFase2Nivel2: sin clímax={sinClimax:F2} (esperado en [{gm.esperaPatronesFase2MinNivel2}, {gm.esperaPatronesFase2MaxNivel2}]), con clímax={conClimax:F2} (esperado en [{gm.esperaPatronesFase2ClimaxMinNivel2}, {gm.esperaPatronesFase2ClimaxMaxNivel2}])");
        if (sinClimax < gm.esperaPatronesFase2MinNivel2 || sinClimax > gm.esperaPatronesFase2MaxNivel2 ||
            conClimax < gm.esperaPatronesFase2ClimaxMinNivel2 || conClimax > gm.esperaPatronesFase2ClimaxMaxNivel2)
            Debug.LogError("FALLÓ: la espera entre patrones de Fase 2 debería caer dentro de los rangos configurados según si es o no el clímax.");
        else if (gm.esperaPatronesFase2ClimaxMaxNivel2 > gm.esperaPatronesFase2MinNivel2)
            Debug.LogError("FALLÓ: el clímax (112-128s) tiene que ser estrictamente más agresivo que el resto de la Fase 2 — el tramo más difícil del nivel (pedido explícito).");
        else
            Debug.Log("OK: el clímax de la canción dispara patrones bien más seguido que el resto de la Fase 2.");
    }

    static void PruebaEsperaClampeadaRespetaElFinalDeLaCancion()
    {
        var gm = AbrirEscenaFresca();
        var metodo = typeof(GameManager).GetMethod("EsperaClampeadaFase2Nivel2", Flags);
        var campoTPelea = typeof(GameManager).GetField("tPelea", Flags);

        campoTPelea.SetValue(gm, gm.duracionPeleaNivel2 - 0.15f); // a 0.15s del final de la canción
        float espera = (float)metodo.Invoke(gm, new object[] { true });

        Debug.Log($"EsperaClampeadaFase2Nivel2 a 0.15s del final de la canción (clímax)={espera:F2} (esperado <= 0.15)");
        if (espera > 0.15f + 0.001f)
            Debug.LogError("FALLÓ: cerca del final de la canción, la espera debería recortarse para no pasarse de duracionPeleaNivel2 — la cutscene de cierre tiene que enganchar puntual con el final del tema.");
        else
            Debug.Log("OK: la última espera de la Fase 2 nunca empuja tPelea más allá del final de la canción.");
    }

    static void PruebaDispararPatronNoLanzaExcepcionYCreaUnMuro()
    {
        var gm = AbrirEscenaFresca();
        var fp = Object.Instantiate(gm.formaPrecisaPrefab, Vector2.zero, Quaternion.identity);
        Invocar(fp, "Awake");
        typeof(GameManager).GetField("formaPrecisaActiva", Flags).SetValue(gm, fp);

        int murosAntes = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Length;
        typeof(GameManager).GetMethod("DispararPatronLaserFase2", Flags).Invoke(gm, new object[] { false });
        int murosDespues = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Length;

        Debug.Log($"Tras DispararPatronLaserFase2(false): LaserHazard en escena antes={murosAntes}, después={murosDespues} (esperado más que antes — cualquiera de los 6 patrones deja al menos un LaserHazard en escena)");
        if (murosDespues <= murosAntes)
            Debug.LogError("FALLÓ: DispararPatronLaserFase2 debería disparar alguno de los patrones de la Fase 2 y dejar al menos un LaserHazard en escena.");
        else
            Debug.Log("OK: la Fase 2 dispara sus patrones (4 reusados + 2 propios del boss) y deja muros de verdad en escena.");
    }

    /// <summary>ResolverImpactoLaser (revisión): un golpe durante la Fase 2 no debería etiquetarse como si todavía fuera de Fase 1 (EscaladaPatronesFase1 queda clavada en 5 después de los 80s — mezclaría el balance por fase del punto 10).</summary>
    static void PruebaImpactoDuranteFase2EtiquetaCorrectamente()
    {
        var gm = AbrirEscenaFresca();
        var fp = Object.Instantiate(gm.formaPrecisaPrefab, Vector2.zero, Quaternion.identity);
        Invocar(fp, "Awake");
        typeof(GameManager).GetField("formaPrecisaActiva", Flags).SetValue(gm, fp);
        typeof(GameManager).GetField("superAdministradorActivo", Flags).SetValue(gm, true);
        int vidasAntes = (int)typeof(FormaPrecisa).GetField("vidas", Flags).GetValue(fp);

        // No se puede leer el string "fuente" que le llega a RecibirGolpe
        // (solo alimenta Telemetria.Registrar, no queda en ningún campo
        // inspeccionable) — lo que SÍ se puede confirmar sin reventar es
        // que ResolverImpactoLaser sigue golpeando de verdad durante la
        // Fase 2 (no quedó gateado por accidente al tocar esta rama).
        gm.ResolverImpactoLaser(new Rect(-1f, -1f, 2f, 2f));

        int vidasDespues = (int)typeof(FormaPrecisa).GetField("vidas", Flags).GetValue(fp);
        Debug.Log($"ResolverImpactoLaser durante la Fase 2 (superAdministradorActivo=true): vidas antes={vidasAntes}, después={vidasDespues} (esperado una menos)");
        if (vidasDespues != vidasAntes - 1)
            Debug.LogError("FALLÓ: un impacto de pared durante la Fase 2 debería seguir restando una vida igual que en Fase 1.");
        else
            Debug.Log("OK: los impactos de pared siguen dañando de verdad durante la Fase 2 (la rama nueva de etiquetado no rompió el daño real).");
    }

    /// <summary>
    /// Bug real encontrado vía revisión: los 3 caminos hacia la Fase 2
    /// (beat real completo, salto manual, "ya la vi antes") llegan a
    /// TerminarEscaladaPrivilegiosNivel2 con tPelea DISTINTOS — el salto
    /// "ya la vi antes" resuelve todo apenas se cruza el latch de Fase 1
    /// (~80s), no cerca del final del breakdown (~88s). Sin la espera de
    /// FaseLaseresYVictoriaNivel2, los láseres arrancaban hasta 8s antes
    /// de tiempo, encima del breakdown casi silencioso que el punto 7 pide
    /// explícitamente no saturar. A propósito fuerza PeleaActiva=true acá
    /// (a diferencia de AbrirEscenaFresca, que la deja en false): sin eso
    /// TerminarEscaladaPrivilegiosNivel2 nunca arranca FaseLaseresYVictoriaNivel2
    /// y este test pasaría en falso sin haber probado nada.
    /// </summary>
    static void PruebaFase2NoArrancaAntesDelFinalDelBreakdown()
    {
        var gm = AbrirEscenaFresca();
        var boss = Object.Instantiate(gm.administradorPrefab, new Vector3(0f, gm.mitadAlto * 0.6f, 0f), Quaternion.identity);
        typeof(GameManager).GetField("administradorActivo", Flags).SetValue(gm, boss);
        typeof(GameManager).GetField("tPelea", Flags).SetValue(gm, gm.duracionFase1Nivel2); // el latch se cruza recién acá — ver PeleaNivel2
        typeof(GameManager).GetField("escaladaPrivilegiosVistaSesion", Flags).SetValue(gm, true); // "ya la vi antes" — el camino que resuelve todo síncrono
        typeof(GameManager).GetProperty("PeleaActiva").GetSetMethod(true).Invoke(gm, new object[] { true });

        typeof(GameManager).GetMethod("IniciarEscaladaPrivilegiosNivel2", Flags).Invoke(gm, null);

        int muros = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Length;
        Debug.Log($"Tras 'ya la vi antes' con tPelea={gm.duracionFase1Nivel2} (recién cruzando el latch, breakdown termina en {gm.duracionBreakdownNivel2}): LaserHazard en escena={muros} (esperado 0 — FaseLaseresYVictoriaNivel2 debería estar esperando, no disparando todavía)");
        if (muros != 0)
            Debug.LogError("FALLÓ: la Fase 2 no debería disparar ningún patrón antes de que termine el breakdown, sin importar por qué camino se llegó a la transformación.");
        else
            Debug.Log("OK: cualquiera de los 3 caminos a la transformación espera al final real del breakdown antes de arrancar los láseres de la Fase 2.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
