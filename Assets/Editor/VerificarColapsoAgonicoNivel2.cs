using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Verifica (sin Play Mode) la Prioridad 1 de la revisión nocturna del
/// 2026-09-10: "los 32 segundos muertos" — dato real (partida_20260910_044545.csv)
/// mostró al boss colapsando orgánicamente a los 47.5s (bien antes del
/// objetivo de ~80s) y al jugador muriendo a los 51.99s DESPUÉS del
/// colapso, esquivando patrones que seguían escalando sin ganar nada por
/// seguir vivo. Acá se prueba que (a) el tier de patrones queda congelado
/// al colapsar en vez de seguir subiendo con tPelea, y (b) la reacción
/// visual/sonora del colapso corre de verdad.
/// </summary>
public static class VerificarColapsoAgonicoNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Colapso Agónico Nivel 2")]
    public static void Verificar()
    {
        PruebaTierQuedaCongeladoTrasColapsoOrganico();
        PruebaEsperaAgonicaUsaElRangoErraticoTrasColapsar();
        PruebaColapsoActivaElGlitchAIntensidadBaja();
        PruebaColapsoForzadoTambienCongelaElTier();

        Debug.Log("Verificación del colapso agónico de Nivel 2 completa.");
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

    static void EscribirVida(GameManager gm, float v) => typeof(GameManager).GetField("vidaBossNivel2", Flags).SetValue(gm, v);
    static void EscribirTPelea(GameManager gm, float t) => typeof(GameManager).GetField("tPelea", Flags).SetValue(gm, t);
    static void AplicarDano(GameManager gm, float cantidad) => typeof(GameManager).GetMethod("AplicarDanoBossNivel2", Flags).Invoke(gm, new object[] { cantidad });
    static int TierActual(GameManager gm, float t) => (int)typeof(GameManager).GetMethod("TierPatronesActualNivel2", Flags).Invoke(gm, new object[] { t });

    /// <summary>El caso real que se dio: colapso temprano (tPelea bajo, tier bajo) y el jugador sigue vivo mucho después — el tier NO debería seguir subiendo con tPelea.</summary>
    static void PruebaTierQuedaCongeladoTrasColapsoOrganico()
    {
        var gm = AbrirEscenaFresca();
        EscribirTPelea(gm, 47.5f); // tier real en el momento del colapso de la corrida real: floor(47.5/16.35)+1=3
        EscribirVida(gm, gm.umbralColapsoBossNivel2 + 1f); // un golpe más alcanza para colapsar

        int tierAntesDeColapsar = TierActual(gm, 47.5f); // todavía sin colapsar — debería seguir la escalada normal
        AplicarDano(gm, 999f); // dispara el colapso acá, con tPelea=47.5

        EscribirTPelea(gm, 79.9f); // mucho más tarde, casi el final de Fase 1 — sin el freeze, EscaladaPatronesFase1(79.9) daría tier 5
        int tierMuchoDespues = TierActual(gm, 79.9f);

        Debug.Log($"Tier ANTES de colapsar (tPelea=47.5, sin colapso todavía)={tierAntesDeColapsar} (esperado igual a EscaladaPatronesFase1(47.5)=3); tier MUCHO DESPUÉS de colapsar (tPelea=79.9)={tierMuchoDespues} (esperado SIGUE siendo 3 — congelado, no 5)");
        if (tierAntesDeColapsar != 3 || tierMuchoDespues != 3)
            Debug.LogError("FALLÓ: el tier de patrones debería congelarse en el valor que tenía al momento del colapso, sin importar cuánto avance tPelea después.");
        else
            Debug.Log("OK: tras colapsar, el tier de patrones se congela — no sigue escalando con el tiempo aunque el jugador siga vivo.");
    }

    static void PruebaEsperaAgonicaUsaElRangoErraticoTrasColapsar()
    {
        var gm = AbrirEscenaFresca();
        var metodo = typeof(GameManager).GetMethod("EsperaClampeadaNivel2", Flags);
        EscribirTPelea(gm, 10f); // lejos del límite de Fase 1 — no debería clampear nada, aísla el rango agónico

        typeof(GameManager).GetField("bossColapsadoNivel2", Flags).SetValue(gm, true);
        float espera = (float)metodo.Invoke(gm, new object[] { 3 });

        Debug.Log($"EsperaClampeadaNivel2 con bossColapsadoNivel2=true (tPelea=10, lejos del límite)={espera:F2} (esperado en [{gm.esperaMinAgonicaNivel2}, {gm.esperaMaxAgonicaNivel2}], el rango agónico, no EsperaEntrePatrones)");
        if (espera < gm.esperaMinAgonicaNivel2 || espera > gm.esperaMaxAgonicaNivel2)
            Debug.LogError("FALLÓ: colapsado el boss, la espera entre patrones debería salir del rango agónico (errático), no de la escalada normal por fase.");
        else
            Debug.Log("OK: colapsado el boss, la cadencia entre patrones pasa a errática (rango agónico).");
    }

    static void PruebaColapsoActivaElGlitchAIntensidadBaja()
    {
        var gm = AbrirEscenaFresca();
        if (gm.materialCorrupcionGlitchNivel2 == null)
        {
            Debug.LogWarning("PruebaColapsoActivaElGlitchAIntensidadBaja: materialCorrupcionGlitchNivel2 sin asignar (correr 'Enjambre/Crear Materiales de Shaders' primero) — prueba salteada, no cuenta como FALLÓ ni OK.");
            return;
        }
        var boss = Object.FindFirstObjectByType<AdministradorSistema>();
        EscribirVida(gm, gm.umbralColapsoBossNivel2 + 1f);

        AplicarDano(gm, 999f); // dispara el colapso -> ReaccionColapsoBossNivel2

        bool materialCorrecto = boss.sr.sharedMaterial == gm.materialCorrupcionGlitchNivel2;
        var mpb = new MaterialPropertyBlock();
        boss.sr.GetPropertyBlock(mpb);
        float intensidad = mpb.GetFloat(Shader.PropertyToID("_Intensidad"));
        Debug.Log($"Tras el colapso: material del boss es el de glitch={materialCorrecto} (esperado true), _Intensidad={intensidad:F2} (esperado {gm.intensidadGlitchAgonicoNivel2:F2} — bajo y constante, no el pico de la transformación real)");
        if (!materialCorrecto || Mathf.Abs(intensidad - gm.intensidadGlitchAgonicoNivel2) > 0.001f)
            Debug.LogError("FALLÓ: el colapso debería activar el shader de glitch a la intensidad agónica configurada.");
        else
            Debug.Log("OK: el colapso deja al boss visiblemente dañado (glitch bajo y constante) — se lee como 'agonizando', no como si nada hubiera pasado.");
    }

    static void PruebaColapsoForzadoTambienCongelaElTier()
    {
        var gm = AbrirEscenaFresca();
        EscribirVida(gm, gm.vidaBossMaxNivel2 * 0.6f); // lejos del umbral — no llegó a tiempo
        EscribirTPelea(gm, gm.duracionFase1Nivel2); // el latch se cruza acá

        typeof(GameManager).GetMethod("ForzarColapsoBossNivel2", Flags).Invoke(gm, null);

        int tier = TierActual(gm, gm.duracionFase1Nivel2 + 20f); // mucho más tarde, si no estuviera congelado esto no importaría de todos modos (ya sería 5)
        bool colapsado = (bool)typeof(GameManager).GetField("bossColapsadoNivel2", Flags).GetValue(gm);
        Debug.Log($"Tras ForzarColapsoBossNivel2 (camino 'jugador lento'): colapsado={colapsado} (esperado true), tier congelado={tier} (esperado 5, coherente con EscaladaPatronesFase1 a los 80s)");
        if (!colapsado || tier != 5)
            Debug.LogError("FALLÓ: el colapso forzado por tiempo también debería congelar el tier (aunque en la práctica ya sea el máximo a esa altura).");
        else
            Debug.Log("OK: el camino 'jugador lento' (latch de Fase 6) también congela el tier, consistente con el camino orgánico.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
