using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Verifica (sin Play Mode) la vida del boss estilo Mega Man X3 (Fase 4
/// del rediseño grande a boss fight) y su conexión real con el pulso de
/// energía de Fase 3. Todo síncrono: AplicarDanoBossNivel2 no es
/// corrutina. La única corrutina de este sistema (PresentacionVidaBossNivel2,
/// el tick escalonado de 0 a 100 antes de la pelea) se prueba solo en su
/// prefijo síncrono, mismo límite que el resto del proyecto — lo
/// importante ahí es que el VALOR real (vidaBossNivel2) ya está en el
/// máximo desde el primer frame, sin depender de que la animación termine
/// (ver el comentario de GameManager.IniciarPeleaNivel2).
/// </summary>
public static class VerificarVidaBossNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Vida del Boss Nivel 2")]
    public static void Verificar()
    {
        var gmChequeo = AbrirEscenaFresca();
        if (gmChequeo.administradorPrefab == null || gmChequeo.formaPrecisaPrefab == null)
        {
            Debug.LogError("FALLÓ: falta administradorPrefab o formaPrecisaPrefab en GameManager — correr antes 'Crear Administrador del Sistema' y 'Crear Prefab Forma Precisa'.");
            return;
        }

        PruebaVidaMaximaSincronicaAlArrancar();
        PruebaDanoBossResta();
        PruebaColapsoFijaPisoYNoBajaMas();
        PruebaPulsoDeEnergiaConectaConVidaReal();
        PruebaBarraSincronizaTrasPresentacion();
        PruebaForzarColapsoPorTiempo();

        Debug.Log("Verificación de vida del boss de Nivel 2 completa.");
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

    static float LeerVida(GameManager gm) => (float)typeof(GameManager).GetField("vidaBossNivel2", Flags).GetValue(gm);
    static void EscribirVida(GameManager gm, float v) => typeof(GameManager).GetField("vidaBossNivel2", Flags).SetValue(gm, v);
    static void AplicarDano(GameManager gm, float cantidad) =>
        typeof(GameManager).GetMethod("AplicarDanoBossNivel2", Flags).Invoke(gm, new object[] { cantidad });

    static void PruebaVidaMaximaSincronicaAlArrancar()
    {
        var gm = AbrirEscenaFresca();
        typeof(GameManager).GetField("cutsceneAperturaVistaSesion", Flags).SetValue(gm, true);
        gm.BotonJugarNivel2();

        float vida = LeerVida(gm);
        bool colapsado = (bool)typeof(GameManager).GetField("bossColapsadoNivel2", Flags).GetValue(gm);
        float fill = gm.barraVidaBossFillNivel2 != null ? gm.barraVidaBossFillNivel2.fillAmount : -1f;
        Debug.Log($"Al arrancar la pelea: vidaBossNivel2={vida} (esperado {gm.vidaBossMaxNivel2}, YA al máximo aunque la animación de presentación recién esté empezando), colapsado={colapsado} (esperado false), fillAmount del prefijo síncrono de la animación={fill:F3} (esperado >0 y <1 — a mitad de un tick escalonado)");
        if (vida != gm.vidaBossMaxNivel2 || colapsado)
            Debug.LogError("FALLÓ: el valor REAL de vida debe estar al máximo desde el primer frame — la animación es puro espectáculo, no debe demorar el número lógico (pedido explícito: 'no lo implementes al revés').");
        else
            Debug.Log("OK: la vida real ya está al máximo de forma síncrona; la barra solo se ve subir de a poco por espectáculo.");
    }

    static void PruebaDanoBossResta()
    {
        var gm = AbrirEscenaFresca();
        var fp = CrearFormaPrecisaDePrueba(gm, Vector2.zero);
        var boss = Object.Instantiate(gm.administradorPrefab, new Vector3(0f, gm.mitadAlto * 0.6f, 0f), Quaternion.identity);
        typeof(GameManager).GetField("administradorActivo", Flags).SetValue(gm, boss);
        EscribirVida(gm, gm.vidaBossMaxNivel2);

        AplicarDano(gm, gm.danoPulsoNivel2);

        float vidaEsperada = gm.vidaBossMaxNivel2 - gm.danoPulsoNivel2;
        Debug.Log($"Tras un golpe de danoPulsoNivel2={gm.danoPulsoNivel2}: vidaBossNivel2={LeerVida(gm)} (esperado {vidaEsperada})");
        if (Mathf.Abs(LeerVida(gm) - vidaEsperada) > 0.001f)
            Debug.LogError("FALLÓ: AplicarDanoBossNivel2 debería restar exactamente la cantidad pasada.");
        else
            Debug.Log("OK: un golpe resta exactamente el daño esperado — la barra BAJA con el daño (nunca sube).");
    }

    static void PruebaColapsoFijaPisoYNoBajaMas()
    {
        var gm = AbrirEscenaFresca();
        var boss = Object.Instantiate(gm.administradorPrefab, new Vector3(0f, gm.mitadAlto * 0.6f, 0f), Quaternion.identity);
        typeof(GameManager).GetField("administradorActivo", Flags).SetValue(gm, boss);
        EscribirVida(gm, gm.vidaBossMaxNivel2);

        AplicarDano(gm, gm.vidaBossMaxNivel2 * 10f); // muchísimo más de lo que le queda — no debería pasar de largo el piso

        bool colapsado = (bool)typeof(GameManager).GetField("bossColapsadoNivel2", Flags).GetValue(gm);
        Debug.Log($"Tras un golpe brutal: vidaBossNivel2={LeerVida(gm)} (esperado exactamente umbralColapsoBossNivel2={gm.umbralColapsoBossNivel2}), colapsado={colapsado} (esperado true)");
        if (Mathf.Abs(LeerVida(gm) - gm.umbralColapsoBossNivel2) > 0.001f || !colapsado)
            Debug.LogError("FALLÓ: la vida debería quedar clavada en el umbral, nunca por debajo — 'el administrador nunca es derrotado, solo fracasa'.");
        else
            Debug.Log("OK: el daño se clampea al umbral exacto y marca el colapso.");

        // Un golpe MÁS, ya colapsado — no debería hacer nada.
        AplicarDano(gm, gm.danoPulsoNivel2);
        Debug.Log($"Tras un golpe extra ya colapsado: vidaBossNivel2={LeerVida(gm)} (esperado sin cambios, {gm.umbralColapsoBossNivel2})");
        if (Mathf.Abs(LeerVida(gm) - gm.umbralColapsoBossNivel2) > 0.001f)
            Debug.LogError("FALLÓ: una vez colapsado, más daño no debería seguir bajando la vida.");
        else
            Debug.Log("OK: colapsado ignora daño adicional — no hay un '0% real' que gane la pelea de más.");
    }

    /// <summary>Integración real de punta a punta: 5 orbes de verdad (no un seteo directo de energía) deberían terminar restándole vida al boss.</summary>
    static void PruebaPulsoDeEnergiaConectaConVidaReal()
    {
        var gm = AbrirEscenaFresca();
        var fp = CrearFormaPrecisaDePrueba(gm, Vector2.zero);
        var boss = Object.Instantiate(gm.administradorPrefab, new Vector3(0f, gm.mitadAlto * 0.6f, 0f), Quaternion.identity);
        typeof(GameManager).GetField("administradorActivo", Flags).SetValue(gm, boss);
        EscribirVida(gm, gm.vidaBossMaxNivel2);
        // Fuerza el cooldown del pulso potente (Fase 5) para que este test
        // se quede pura verificación de cableado Fase 3 -> Fase 4 — cuál
        // de los dos (normal/potente) sale se prueba aparte, con mucho más
        // detalle, en VerificarComboPulsoPotenteNivel2.cs.
        typeof(GameManager).GetField("tCooldownPulsoPotenteRestanteNivel2", Flags).SetValue(gm, gm.cooldownPulsoPotenteNivel2);

        var metodoActualizar = typeof(GameManager).GetMethod("ActualizarOrbesNivel2", Flags);
        int orbesParaLlenar = Mathf.CeilToInt(gm.energiaMaxNivel2 / gm.cargaPorOrbeNivel2);
        for (int i = 0; i < orbesParaLlenar; i++)
        {
            var o = Object.Instantiate(gm.orbePrefab, new Vector3(gm.radioRecoleccionNivel2 * 0.5f, 0f, 0f), Quaternion.identity);
            Invocar(o, "Awake");
            metodoActualizar.Invoke(gm, new object[] { 1f });
        }

        float vidaEsperada = gm.vidaBossMaxNivel2 - gm.danoPulsoNivel2;
        Debug.Log($"Tras juntar {orbesParaLlenar} orbes reales (barra llena -> DescargarPulsoNivel2): vidaBossNivel2={LeerVida(gm)} (esperado {vidaEsperada})");
        if (Mathf.Abs(LeerVida(gm) - vidaEsperada) > 0.001f)
            Debug.LogError("FALLÓ: llenar la barra recolectando orbes de verdad debería terminar restándole vida al boss (Fase 3 -> Fase 4 conectadas).");
        else
            Debug.Log("OK: el camino completo orbe -> energía -> pulso -> daño al boss funciona de punta a punta.");
    }

    static void PruebaBarraSincronizaTrasPresentacion()
    {
        var gm = AbrirEscenaFresca();
        EscribirVida(gm, gm.vidaBossMaxNivel2 * 0.4f);
        // Simula que la animación de presentación ya terminó (sin tickear
        // la corrutina real — mismo criterio que el resto del proyecto).
        typeof(GameManager).GetField("animandoPresentacionVidaBossNivel2", Flags).SetValue(gm, false);

        typeof(GameManager).GetMethod("ActualizarBarraVidaBossNivel2", Flags).Invoke(gm, null);

        float esperado = gm.FraccionVidaBossNivel2;
        float real = gm.barraVidaBossFillNivel2 != null ? gm.barraVidaBossFillNivel2.fillAmount : -1f;
        Debug.Log($"Tras la presentación (simulada como terminada): fillAmount={real:F3} (esperado {esperado:F3})");
        if (gm.barraVidaBossFillNivel2 == null || Mathf.Abs(real - esperado) > 0.001f)
            Debug.LogError("FALLÓ: terminada la presentación, la barra debería reflejar la fracción real de vida en cada frame.");
        else
            Debug.Log("OK: una vez terminada la presentación, el sync por-frame refleja la vida real.");
    }

    /// <summary>El latch de Fase 6 (PeleaNivel2): si el jugador NO llegó al umbral para cuando termina el cuerpo de Fase 1, se fuerza — la sincronía con la música importa más que la precisión del umbral.</summary>
    static void PruebaForzarColapsoPorTiempo()
    {
        var gm = AbrirEscenaFresca();
        EscribirVida(gm, gm.vidaBossMaxNivel2 * 0.6f); // todavía lejos del umbral — no llegó a tiempo
        bool colapsadoAntes = (bool)typeof(GameManager).GetField("bossColapsadoNivel2", Flags).GetValue(gm);

        typeof(GameManager).GetMethod("ForzarColapsoBossNivel2", Flags).Invoke(gm, null);

        bool colapsadoDespues = (bool)typeof(GameManager).GetField("bossColapsadoNivel2", Flags).GetValue(gm);
        Debug.Log($"Forzar colapso con vida todavía alta: colapsado antes={colapsadoAntes} (esperado false), vidaBossNivel2={LeerVida(gm)} (esperado exactamente {gm.umbralColapsoBossNivel2}), colapsado después={colapsadoDespues} (esperado true)");
        if (colapsadoAntes || Mathf.Abs(LeerVida(gm) - gm.umbralColapsoBossNivel2) > 0.001f || !colapsadoDespues)
            Debug.LogError("FALLÓ: ForzarColapsoBossNivel2 debería dejar la vida exactamente en el umbral y marcar el colapso, sin importar cuánta vida le quedaba.");
        else
            Debug.Log("OK: el boss colapsa a la fuerza si no llegó al umbral a tiempo — la pelea nunca pierde la sincronía con el breakdown de la música.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
