using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Verifica (sin Play Mode) "Sentencia Final" (pedido vía revisión,
/// feedback jugando: "esa segunda fase tiene que ser la parte más 'si
/// pierdo ahora, tengo que pasar de nuevo por la fase 1'") — el patrón
/// único que se dispara UNA vez justo al entrar al clímax (112s),
/// reemplazando por un momento la selección aleatoria normal de
/// DispararPatronLaserFase2. Reusa DispararTelaRadialNivel2/
/// SecuenciaEspiralGiratoriaFase2 parametrizados (los mismos que ya usan
/// los patrones normales) con números más grandes — la garantía del ojo
/// seguro (ver VerificarFase2PatronesPropios.cs) es independiente de
/// cuántos rayos se disparen, así que no hace falta reprobarla acá.
/// </summary>
public static class VerificarSentenciaFinalNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Sentencia Final Nivel 2")]
    public static void Verificar()
    {
        PruebaBeat0NoDisparaRayosTodavia();
        PruebaTelaRadialAmplificadaProduceElDoble();
        PruebaEspiralAmplificadaPrimeraOleada();
        PruebaFaseLaseresEligeSentenciaFinalAlEntrarAlClimax();

        Debug.Log("Verificación de Sentencia Final de Nivel 2 completa.");
    }

    static GameManager AbrirEscenaFresca()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");
        var boss = Object.Instantiate(gm.administradorPrefab, new Vector3(0f, gm.mitadAlto * 0.6f, 0f), Quaternion.identity);
        typeof(GameManager).GetField("administradorActivo", Flags).SetValue(gm, boss);
        return gm;
    }

    /// <summary>El beat 0 (carga dramática) es puro efecto visual/sonoro — no debería crear ningún LaserHazard todavía, el peligro real arranca recién en el beat 1 tras el respiro.</summary>
    static void PruebaBeat0NoDisparaRayosTodavia()
    {
        var gm = AbrirEscenaFresca();
        typeof(GameManager).GetMethod("PatronSentenciaFinalNivel2", Flags).Invoke(gm, null); // corre síncrono hasta el primer WaitForSeconds real (0.8s del beat 0)

        int muros = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Length;
        Debug.Log($"Tras arrancar Sentencia Final (solo el beat 0 dramático corre en batch mode): LaserHazard creados={muros} (esperado 0)");
        if (muros != 0)
            Debug.LogError("FALLÓ: el beat 0 de Sentencia Final es puro aviso — no debería disparar ningún rayo todavía.");
        else
            Debug.Log("OK: el beat 0 es puro aviso dramático, el peligro real arranca después del respiro.");
    }

    static void PruebaTelaRadialAmplificadaProduceElDoble()
    {
        var gm = AbrirEscenaFresca();
        var metodo = typeof(GameManager).GetMethod("DispararTelaRadialNivel2", Flags);

        metodo.Invoke(gm, new object[] { gm.rayosSentenciaFinalNivel2 });

        int muros = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Length;
        int esperados = gm.rayosSentenciaFinalNivel2 * 2; // 2 por rayo, ver el ojo seguro
        Debug.Log($"DispararTelaRadialNivel2({gm.rayosSentenciaFinalNivel2}) (Sentencia Final): LaserHazard creados={muros} (esperado {esperados} — el doble que el patrón normal, {gm.rayosTelaRadialNivel2} rayos)");
        if (muros != esperados)
            Debug.LogError("FALLÓ: la Tela Radial amplificada del clímax debería crear 2*rayosSentenciaFinalNivel2 paredes.");
        else
            Debug.Log("OK: la Tela Radial del clímax es notablemente más densa que la normal, reusando la misma función (misma garantía de ojo seguro).");
    }

    static void PruebaEspiralAmplificadaPrimeraOleada()
    {
        var gm = AbrirEscenaFresca();
        var metodo = typeof(GameManager).GetMethod("SecuenciaEspiralGiratoriaFase2", Flags);

        // Corrutina real — en batch mode corre síncrono hasta su primer
        // yield real (el WaitForSeconds al final de la primera oleada),
        // mismo límite de siempre (ver PruebaEspiralGiratoriaDisparaLaPrimeraOleada
        // en VerificarFase2PatronesPropios.cs, mismo patrón).
        gm.StartCoroutine((System.Collections.IEnumerator)metodo.Invoke(gm,
            new object[] { gm.brazosSentenciaFinalNivel2, gm.oleadasSentenciaFinalNivel2, gm.pasoAnguloSentenciaFinalNivel2, gm.esperaEntreOleadasSentenciaFinalNivel2 }));

        int muros = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Length;
        int esperados = gm.brazosSentenciaFinalNivel2 * 2;
        Debug.Log($"SecuenciaEspiralGiratoriaFase2 amplificada, primera oleada: LaserHazard creados={muros} (esperado {esperados} — el doble de brazosSentenciaFinalNivel2, más que el patrón normal con {gm.brazosEspiralGiratoriaNivel2} brazos)");
        if (muros != esperados)
            Debug.LogError("FALLÓ: la primera oleada de la Espiral amplificada debería crear 2*brazosSentenciaFinalNivel2 paredes — si dispara todas las oleadas de una en vez de escalonarlas en el tiempo, el giro deja de ser navegable (ver el comentario de PatronSentenciaFinalNivel2).");
        else
            Debug.Log("OK: la Espiral amplificada sigue escalonando sus oleadas en el tiempo (mismo giro navegable que la normal, solo más brazos/rápida).");
    }

    /// <summary>
    /// Al entrar al clímax con tPelea ya alto (simulando "recién se cruzó
    /// a 112s"), FaseLaseresYVictoriaNivel2 debería ir por Sentencia Final
    /// (que no dispara NINGÚN LaserHazard en su prefijo síncrono, solo el
    /// beat 0 dramático) en vez de la selección aleatoria normal
    /// (DispararPatronLaserFase2, que si tocara un patrón que no sea
    /// Espiral Giratoria dispara sus muros de forma síncrona e inmediata).
    /// </summary>
    static void PruebaFaseLaseresEligeSentenciaFinalAlEntrarAlClimax()
    {
        var gm = AbrirEscenaFresca();
        var fp = Object.Instantiate(gm.formaPrecisaPrefab, Vector2.zero, Quaternion.identity);
        Invocar(fp, "Awake");
        typeof(GameManager).GetField("formaPrecisaActiva", Flags).SetValue(gm, fp);
        typeof(GameManager).GetProperty("PeleaActiva").GetSetMethod(true).Invoke(gm, new object[] { true });
        typeof(GameManager).GetField("tPelea", Flags).SetValue(gm, gm.duracionRebuildNivel2 + 0.1f); // recién cruzando a clímax (112s)

        typeof(GameManager).GetMethod("FaseLaseresYVictoriaNivel2", Flags).Invoke(gm, null);

        int muros = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Length;
        Debug.Log($"FaseLaseresYVictoriaNivel2 arrancando ya en el clímax (tPelea={gm.duracionRebuildNivel2 + 0.1f:F1}): LaserHazard creados={muros} (esperado 0 — debería haber ido por Sentencia Final, cuyo prefijo síncrono no dispara rayos todavía, no por un patrón normal al azar)");
        if (muros != 0)
            Debug.LogError("FALLÓ: al entrar al clímax por primera vez, debería dispararse Sentencia Final (sin rayos en su prefijo síncrono) en vez de la selección aleatoria normal.");
        else
            Debug.Log("OK: entrar al clímax dispara Sentencia Final antes que cualquier patrón normal al azar.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
