using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Verifica (sin Play Mode) los dos patrones propios del boss agregados a
/// la Fase 2 vía revisión (inspirados en un par de spellcards de danmaku
/// que el usuario mandó como referencia, adaptados al lenguaje del
/// proyecto): Tela Radial y Espiral Giratoria, más el camino de colisión
/// ROTADO nuevo que los soporta (LaserHazard.Configurar(centro,tamaño,
/// ángulo,...) / GameManager.ResolverImpactoLaserRotado) sin tocar el
/// camino Rect original que ya usan RafagaMurallaLaser/los 4 patrones
/// heredados/VerificarMurallaLaser.
/// </summary>
public static class VerificarFase2PatronesPropios
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Fase 2 Patrones Propios")]
    public static void Verificar()
    {
        PruebaRotadoAAnguloCeroEquivaleARect();
        PruebaTelaRadialDisparaDesdeElBoss();
        PruebaEspiralGiratoriaDisparaLaPrimeraOleada();
        PruebaClimaxFavoreceLosPatronesPropios();

        Debug.Log("Verificación de los patrones propios de la Fase 2 completa.");
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

    static FormaPrecisa CrearFormaEn(GameManager gm, Vector2 pos)
    {
        var fp = Object.Instantiate(gm.formaPrecisaPrefab, pos, Quaternion.identity);
        Invocar(fp, "Awake");
        typeof(GameManager).GetField("formaPrecisaActiva", Flags).SetValue(gm, fp);
        return fp;
    }

    static int Vidas(FormaPrecisa fp) => (int)typeof(FormaPrecisa).GetField("vidas", Flags).GetValue(fp);

    /// <summary>
    /// A ánguloGrados=0, ResolverImpactoLaserRotado(centro,tamaño,0) tiene
    /// que golpear EXACTAMENTE los mismos puntos que ResolverImpactoLaser
    /// sobre el Rect equivalente (centro.x-tamaño.x/2, centro.y-tamaño.y/2,
    /// tamaño.x, tamaño.y) — la garantía en la que se apoya todo lo demás
    /// de este archivo (si esto no vale, nada de lo que dispare rotado a
    /// 0° es confiable). Se prueba con vidas reales (no inspeccionando
    /// matemática interna) para probar el camino de punta a punta.
    /// </summary>
    static void PruebaRotadoAAnguloCeroEquivaleARect()
    {
        Vector2 centro = new Vector2(1f, 2f);
        Vector2 tamano = new Vector2(2f, 6f); // ancho=2, largo=6
        Rect rectEquivalente = new Rect(centro.x - tamano.x * 0.5f, centro.y - tamano.y * 0.5f, tamano.x, tamano.y);

        Vector2 puntoAdentro = centro + new Vector2(0.5f, 1f); // adentro de ambos por construcción
        Vector2 puntoAfuera = centro + new Vector2(5f, 5f); // bien afuera de ambos

        var gm1 = AbrirEscenaFresca();
        var fpAdentroRect = CrearFormaEn(gm1, puntoAdentro);
        int vidasAntesRect = Vidas(fpAdentroRect);
        gm1.ResolverImpactoLaser(rectEquivalente);
        bool golpeoRect = Vidas(fpAdentroRect) == vidasAntesRect - 1;

        var gm2 = AbrirEscenaFresca();
        var fpAdentroRotado = CrearFormaEn(gm2, puntoAdentro);
        int vidasAntesRotado = Vidas(fpAdentroRotado);
        gm2.ResolverImpactoLaserRotado(centro, tamano, 0f);
        bool golpeoRotado = Vidas(fpAdentroRotado) == vidasAntesRotado - 1;

        var gm3 = AbrirEscenaFresca();
        var fpAfueraRect = CrearFormaEn(gm3, puntoAfuera);
        int vidasAntesRectAfuera = Vidas(fpAfueraRect);
        gm3.ResolverImpactoLaser(rectEquivalente);
        bool noGolpeoRect = Vidas(fpAfueraRect) == vidasAntesRectAfuera;

        var gm4 = AbrirEscenaFresca();
        var fpAfueraRotado = CrearFormaEn(gm4, puntoAfuera);
        int vidasAntesRotadoAfuera = Vidas(fpAfueraRotado);
        gm4.ResolverImpactoLaserRotado(centro, tamano, 0f);
        bool noGolpeoRotado = Vidas(fpAfueraRotado) == vidasAntesRotadoAfuera;

        Debug.Log($"Equivalencia Rect vs Rotado(0°) — punto adentro: golpeó Rect={golpeoRect}, golpeó Rotado={golpeoRotado} (esperado ambos true). Punto afuera: golpeó Rect={!noGolpeoRect}, golpeó Rotado={!noGolpeoRotado} (esperado ambos false)");
        if (!golpeoRect || !golpeoRotado || !noGolpeoRect || !noGolpeoRotado)
            Debug.LogError("FALLÓ: ResolverImpactoLaserRotado a ángulo 0 debería comportarse IDÉNTICO a ResolverImpactoLaser sobre el mismo rectángulo — si difieren, los patrones rotados no son confiables ni siquiera en el caso más simple.");
        else
            Debug.Log("OK: a ángulo 0, el camino rotado golpea exactamente lo mismo que el camino Rect original.");
    }

    static void PruebaTelaRadialDisparaDesdeElBoss()
    {
        var gm = AbrirEscenaFresca();
        Vector2 origenEsperado = new Vector3(0f, gm.mitadAlto * 0.6f, 0f);

        typeof(GameManager).GetMethod("PatronTelaRadialFase2", Flags).Invoke(gm, null);

        var muros = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None);
        bool todosEnElOrigen = muros.All(m => Vector2.Distance(m.transform.position, origenEsperado) < 0.01f);
        Debug.Log($"PatronTelaRadialFase2(): LaserHazard creados={muros.Length} (esperado {gm.rayosTelaRadialNivel2}), todos centrados en el boss={todosEnElOrigen} (esperado true — es lo que le da 'identidad', a diferencia de los 4 heredados que se centran en el jugador)");
        if (muros.Length != gm.rayosTelaRadialNivel2 || !todosEnElOrigen)
            Debug.LogError("FALLÓ: Tela Radial debería crear exactamente rayosTelaRadialNivel2 paredes, todas centradas en la posición del boss.");
        else
            Debug.Log("OK: Tela Radial dispara la cantidad de rayos configurada, todos desde el boss.");
    }

    static void PruebaEspiralGiratoriaDisparaLaPrimeraOleada()
    {
        var gm = AbrirEscenaFresca();

        // Corrutina real — en batch mode corre síncrono hasta su primer
        // yield real (el WaitForSeconds al final de la primera oleada),
        // así que la primera tanda de brazos SÍ queda creada de punta a
        // punta (mismo límite de siempre, ver VerificarPeleaNivel2.cs).
        typeof(GameManager).GetMethod("PatronEspiralGiratoriaFase2", Flags).Invoke(gm, null);

        int muros = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Length;
        Debug.Log($"PatronEspiralGiratoriaFase2(), primera oleada síncrona: LaserHazard creados={muros} (esperado {gm.brazosEspiralGiratoriaNivel2}, un brazo por rayo de la primera tanda)");
        if (muros != gm.brazosEspiralGiratoriaNivel2)
            Debug.LogError("FALLÓ: la primera oleada de Espiral Giratoria debería crear exactamente brazosEspiralGiratoriaNivel2 paredes antes de esperar a la siguiente.");
        else
            Debug.Log("OK: Espiral Giratoria dispara su primera oleada completa de inmediato — el giro entre oleadas sale de escalonar el disparo en el tiempo, no de rotar una pared ya viva.");
    }

    /// <summary>Los 2 patrones propios tienen que pesar más en el clímax (112-128s) — "el tramo más difícil del nivel" (pedido explícito). Estadístico con margen grande a propósito (68.75% vs 42.3% esperado en teoría, con 300 tiradas por lado el ruido no alcanza a cruzar un margen de 15 puntos).</summary>
    static void PruebaClimaxFavoreceLosPatronesPropios()
    {
        var gm = AbrirEscenaFresca();
        var metodo = typeof(GameManager).GetMethod("DispararPatronLaserFase2", Flags);
        var propios = new[] { "tela_radial", "espiral_giratoria" };

        int propiosClimax = ContarPropios(gm, metodo, true, 300, propios);
        int propiosNormal = ContarPropios(gm, metodo, false, 300, propios);
        float fraccionClimax = propiosClimax / 300f;
        float fraccionNormal = propiosNormal / 300f;

        Debug.Log($"Fracción de patrones propios elegidos: clímax={fraccionClimax:P0} ({propiosClimax}/300), fuera de clímax={fraccionNormal:P0} ({propiosNormal}/300) — esperado clímax al menos 15 puntos por encima");
        if (fraccionClimax < fraccionNormal + 0.15f)
            Debug.LogError("FALLÓ: en el clímax, los patrones propios del boss deberían salir notablemente más seguido que fuera de él.");
        else
            Debug.Log("OK: el clímax favorece de verdad los patrones propios del boss sobre los 4 heredados.");
    }

    static int ContarPropios(GameManager gm, MethodInfo metodo, bool climax, int tiradas, string[] propios)
    {
        int cuenta = 0;
        for (int i = 0; i < tiradas; i++)
        {
            string nombre = (string)metodo.Invoke(gm, new object[] { climax });
            if (propios.Contains(nombre)) cuenta++;
        }
        return cuenta;
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
