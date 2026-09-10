using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Verifica (sin Play Mode) la terminal de escalada de privilegios y la
/// transformación a Super Administrador (Fase 7 del rediseño grande a boss
/// fight). Mismo límite que el resto del proyecto: EscaladaPrivilegiosNivel2()
/// es una corrutina real, solo se puede tickear hasta su primer yield real
/// (el "ya la vi antes" no tiene ningún yield antes de su primer WaitForSeconds
/// real así que corre entera de forma síncrona al invocarla — ver
/// PruebaYaVistaResuelveTodoSincronicamente). El resto de los caminos se
/// prueban por su estado FINAL a través de los métodos "instantánea"
/// (FinalizarEscaladaPrivilegiosInstantanea/AsegurarSuperAdministradorNivel2),
/// mismo patrón que VerificarCutsceneApertura.cs.
/// </summary>
public static class VerificarEscaladaPrivilegiosNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static readonly BindingFlags FlagsEstaticos = BindingFlags.NonPublic | BindingFlags.Static;

    [MenuItem("Enjambre/Debug/Verificar Escalada de Privilegios Nivel 2")]
    public static void Verificar()
    {
        var gmChequeo = AbrirEscenaFresca();
        if (gmChequeo.administradorPrefab == null || gmChequeo.formaPrecisaPrefab == null || gmChequeo.orbePrefab == null)
        {
            Debug.LogError("FALLÓ: falta administradorPrefab/formaPrecisaPrefab/orbePrefab en GameManager.");
            return;
        }
        if (gmChequeo.panelTerminalNivel2 == null || gmChequeo.textoTerminalNivel2 == null || gmChequeo.botonSaltarTerminalNivel2 == null)
        {
            Debug.LogError("FALLÓ: falta panelTerminalNivel2/textoTerminalNivel2/botonSaltarTerminalNivel2 — correr antes 'Enjambre/Agregar Pantalla de Terminal Nivel 2 (Fase 7)'.");
            return;
        }

        PruebaSetupSincronicoAlArrancar();
        PruebaYaVistaResuelveTodoSincronicamente();
        PruebaSaltoManualTransformaInstantaneo();
        PruebaAsegurarSuperAdministradorEsIdempotente();
        PruebaOrbesSeApaganDuranteEscaladaYFase2();
        PruebaEsperaClampeadaRespetaElLimiteDeFase1();

        Debug.Log("Verificación de la escalada de privilegios de Nivel 2 completa.");
    }

    static GameManager AbrirEscenaFresca()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        gm.estado = EstadoJuego.Jugando; // la escalada corre con Jugando, no Cutscene (ver el comentario de IniciarEscaladaPrivilegiosNivel2)
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");
        // A propósito NO se fuerza PeleaActiva=true acá: TerminarEscaladaPrivilegiosNivel2
        // solo encadena EsperaFinalYVictoriaNivel2 si PeleaActiva es true, y
        // esa corrutina de cola no es lo que están probando estos tests —
        // dejarla en false evita corrutinas colgadas de más sin aportar nada.
        return gm;
    }

    static AdministradorSistema CrearBossDePrueba(GameManager gm)
    {
        var boss = Object.Instantiate(gm.administradorPrefab, new Vector3(0f, gm.mitadAlto * 0.6f, 0f), Quaternion.identity);
        typeof(GameManager).GetField("administradorActivo", Flags).SetValue(gm, boss);
        return boss;
    }

    static void IniciarEscalada(GameManager gm) => typeof(GameManager).GetMethod("IniciarEscaladaPrivilegiosNivel2", Flags).Invoke(gm, null);
    static bool LeerEnEscalada(GameManager gm) => (bool)typeof(GameManager).GetField("enEscaladaPrivilegiosNivel2", Flags).GetValue(gm);
    static bool LeerVistaSesion(GameManager gm) => (bool)typeof(GameManager).GetField("escaladaPrivilegiosVistaSesion", Flags).GetValue(gm);
    static void EscribirVistaSesion(GameManager gm, bool v) => typeof(GameManager).GetField("escaladaPrivilegiosVistaSesion", Flags).SetValue(gm, v);
    static Color ColorSuperAdministrador() => (Color)typeof(GameManager).GetField("ColorSuperAdministradorNivel2", FlagsEstaticos).GetValue(null);

    static void PruebaSetupSincronicoAlArrancar()
    {
        var gm = AbrirEscenaFresca();
        var boss = CrearBossDePrueba(gm);
        EscribirVistaSesion(gm, false);

        IniciarEscalada(gm);

        bool enEscalada = LeerEnEscalada(gm);
        bool panelActivo = gm.panelTerminalNivel2.activeSelf;
        bool botonActivo = gm.botonSaltarTerminalNivel2.activeSelf;
        bool superAdminYa = gm.SuperAdministradorActivo;
        Debug.Log($"Tras arrancar la escalada (primera vez, sin tickear más allá del primer yield real): enEscaladaPrivilegiosNivel2={enEscalada} (esperado true), panelTerminalNivel2 activo={panelActivo} (esperado true), botonSaltarTerminalNivel2 activo={botonActivo} (esperado true), SuperAdministradorActivo={superAdminYa} (esperado false — todavía no llegó a la transformación)");
        if (!enEscalada || !panelActivo || !botonActivo || superAdminYa)
            Debug.LogError("FALLÓ: el arranque síncrono de la escalada no dejó el estado esperado.");
        else
            Debug.Log("OK: arrancar la escalada muestra la terminal y el botón de salto de inmediato, sin transformar todavía.");
    }

    /// <summary>"Ya la vi antes" (retry en la misma sesión): el branch entero corre síncrono (no hay ningún yield real antes de su propio yield break), así que la corrutina queda completamente resuelta al invocarla una sola vez — sin necesidad de tickear nada.</summary>
    static void PruebaYaVistaResuelveTodoSincronicamente()
    {
        var gm = AbrirEscenaFresca();
        var boss = CrearBossDePrueba(gm);
        EscribirVistaSesion(gm, true);

        IniciarEscalada(gm);

        bool enEscalada = LeerEnEscalada(gm);
        bool panelActivo = gm.panelTerminalNivel2.activeSelf;
        bool superAdmin = gm.SuperAdministradorActivo;
        Debug.Log($"Tras arrancar la escalada ya vista antes en esta sesión: enEscaladaPrivilegiosNivel2={enEscalada} (esperado false — se resolvió sola), panelTerminalNivel2 activo={panelActivo} (esperado false), SuperAdministradorActivo={superAdmin} (esperado true)");
        if (enEscalada || panelActivo || !superAdmin)
            Debug.LogError("FALLÓ: 'ya la vi antes' debería resolver la transformación entera de una, sin dejar la terminal visible.");
        else
            Debug.Log("OK: un reintento en la misma sesión no vuelve a tragarse la escalada — se resuelve instantánea (pedido explícito: 'a diferencia de la apertura, esta sí es saltable después de la primera vez').");
    }

    static void PruebaSaltoManualTransformaInstantaneo()
    {
        var gm = AbrirEscenaFresca();
        var boss = CrearBossDePrueba(gm);
        EscribirVistaSesion(gm, false);
        IniciarEscalada(gm); // deja enEscaladaPrivilegiosNivel2=true, a mitad de la corrutina real

        gm.SaltarEscaladaPrivilegios();

        bool enEscalada = LeerEnEscalada(gm);
        bool panelActivo = gm.panelTerminalNivel2.activeSelf;
        bool superAdmin = gm.SuperAdministradorActivo;
        bool colorOk = boss.sr != null && boss.sr.color == ColorSuperAdministrador();
        string texto = gm.textoTerminalNivel2 != null ? gm.textoTerminalNivel2.text : "";
        bool contienePlea = texto.Contains("por favor deja en paz este sistema");
        Debug.Log($"Tras SaltarEscaladaPrivilegios(): enEscaladaPrivilegiosNivel2={enEscalada} (esperado false), panelTerminalNivel2 activo={panelActivo} (esperado false), SuperAdministradorActivo={superAdmin} (esperado true), color del boss actualizado={colorOk} (esperado true), texto final contiene la súplica={contienePlea} (esperado true)");
        if (enEscalada || panelActivo || !superAdmin || !colorOk || !contienePlea)
            Debug.LogError("FALLÓ: el salto manual debería dejar todo en el mismo estado final que la secuencia completa, sin tickear tipeo/esperas.");
        else
            Debug.Log("OK: el botón Saltar resuelve la transformación completa al instante.");
    }

    static void PruebaAsegurarSuperAdministradorEsIdempotente()
    {
        var gm = AbrirEscenaFresca();
        var boss = CrearBossDePrueba(gm);
        var escalaOriginal = boss.transform.localScale;
        var metodo = typeof(GameManager).GetMethod("AsegurarSuperAdministradorNivel2", Flags);

        metodo.Invoke(gm, null);
        var escalaTrasUna = boss.transform.localScale;
        metodo.Invoke(gm, null); // segunda llamada — no debería repetir el efecto
        var escalaTrasDos = boss.transform.localScale;

        Debug.Log($"Escala del boss: original={escalaOriginal}, tras 1 llamada={escalaTrasUna} (esperado *1.3), tras 2 llamadas={escalaTrasDos} (esperado IGUAL a la de 1 llamada — idempotente)");
        if (Vector3.Distance(escalaTrasUna, escalaOriginal * 1.3f) > 0.001f || Vector3.Distance(escalaTrasDos, escalaTrasUna) > 0.001f)
            Debug.LogError("FALLÓ: AsegurarSuperAdministradorNivel2 debería aplicar el efecto una sola vez, sin importar cuántas veces se la llame.");
        else
            Debug.Log("OK: la transformación es idempotente — un salto manual justo después del beat real no la duplica.");
    }

    /// <summary>Fase 7/8: sin orbes ni pulsos ni barra de vida una vez que arranca la escalada — "con privilegios elevados, el virus no puede tocarlo" (pedido explícito).</summary>
    static void PruebaOrbesSeApaganDuranteEscaladaYFase2()
    {
        var gm = AbrirEscenaFresca();
        var fp = Object.Instantiate(gm.formaPrecisaPrefab, Vector2.zero, Quaternion.identity);
        Invocar(fp, "Awake");
        typeof(GameManager).GetField("formaPrecisaActiva", Flags).SetValue(gm, fp);
        var boss = CrearBossDePrueba(gm);
        if (gm.panelVidaBossNivel2 != null) gm.panelVidaBossNivel2.SetActive(true);
        var o1 = Object.Instantiate(gm.orbePrefab, new Vector3(3f, 3f, 0f), Quaternion.identity);
        var o2 = Object.Instantiate(gm.orbePrefab, new Vector3(-3f, -3f, 0f), Quaternion.identity);
        Invocar(o1, "Awake"); Invocar(o2, "Awake");

        var metodoActualizar = typeof(GameManager).GetMethod("ActualizarOrbesNivel2", Flags);
        typeof(GameManager).GetField("enEscaladaPrivilegiosNivel2", Flags).SetValue(gm, true);
        metodoActualizar.Invoke(gm, new object[] { 1f });

        int orbesTrasEscalada = Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None).Length;
        Debug.Log($"ActualizarOrbesNivel2 durante la escalada (enEscaladaPrivilegiosNivel2=true): orbes en escena={orbesTrasEscalada} (esperado sin cambios, 2 — no se reponen ni se recolectan)");
        if (orbesTrasEscalada != 2)
            Debug.LogError("FALLÓ: ActualizarOrbesNivel2 no debería hacer nada mientras enEscaladaPrivilegiosNivel2 es true.");
        else
            Debug.Log("OK: el sistema de orbes se congela apenas arranca la escalada.");

        // AsegurarSuperAdministradorNivel2 (la transformación real) además
        // debería LIMPIAR los orbes que quedaron vivos y ocultar la barra.
        // Cuenta por `recolectado`, no por FindObjectsByType en crudo:
        // Destroy() es diferido (no saca el GameObject de la búsqueda hasta
        // terminar el frame), mismo motivo que OrbesNoRecolectados() en
        // VerificarOrbesEnergiaNivel2.cs.
        typeof(GameManager).GetMethod("AsegurarSuperAdministradorNivel2", Flags).Invoke(gm, null);
        int orbesTrasTransformar = Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None).Count(o => !o.recolectado);
        bool barraOculta = gm.panelVidaBossNivel2 == null || !gm.panelVidaBossNivel2.activeSelf;
        Debug.Log($"Tras AsegurarSuperAdministradorNivel2: orbes sin recolectar en escena={orbesTrasTransformar} (esperado 0), panelVidaBossNivel2 oculto={barraOculta} (esperado true)");
        if (orbesTrasTransformar != 0 || !barraOculta)
            Debug.LogError("FALLÓ: la transformación a Super Administrador debería destruir los orbes que quedaban vivos y ocultar la barra de vida.");
        else
            Debug.Log("OK: la Fase 2 arranca sin orbes sueltos ni barra de vida en pantalla.");
    }

    /// <summary>EsperaClampeadaNivel2 (revisión): la última espera del loop de Fase 1 nunca debería empujar tPelea más allá de duracionFase1Nivel2.</summary>
    static void PruebaEsperaClampeadaRespetaElLimiteDeFase1()
    {
        var gm = AbrirEscenaFresca();
        var metodo = typeof(GameManager).GetMethod("EsperaClampeadaNivel2", Flags);
        var campoTPelea = typeof(GameManager).GetField("tPelea", Flags);

        campoTPelea.SetValue(gm, 0f); // lejos del límite — no debería clampear nada
        float esperaLejos = (float)metodo.Invoke(gm, new object[] { 1 });

        campoTPelea.SetValue(gm, gm.duracionFase1Nivel2 - 0.2f); // a 0.2s del límite — EsperaEntrePatrones(5) normalmente da 0.6-0.95s
        float esperaCerca = (float)metodo.Invoke(gm, new object[] { 5 });

        Debug.Log($"EsperaClampeadaNivel2 lejos del límite (tPelea=0, fase 1)={esperaLejos:F2} (esperado en [2.6, 3.4], sin clampear); cerca del límite (tPelea={gm.duracionFase1Nivel2 - 0.2f:F1}, fase 5)={esperaCerca:F2} (esperado <= 0.2)");
        if (esperaLejos < 2.6f || esperaLejos > 3.4f || esperaCerca > 0.2f + 0.001f)
            Debug.LogError("FALLÓ: cerca del límite de Fase 1, la espera debería recortarse para no pasarse de duracionFase1Nivel2 — si no, el arranque del latch pierde hasta ~2 beats de sincronía.");
        else
            Debug.Log("OK: la última espera del loop de patrones nunca empuja tPelea más allá del límite de Fase 1.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
