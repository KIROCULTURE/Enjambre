using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verifica (sin Play Mode) la cutscene de apertura del Nivel 2 (punto 3
/// del rediseño a boss fight). Como el grueso de los beats vive dentro de
/// una corrutina con muchos WaitForSeconds — que en batch mode corre
/// síncrono solo hasta el primer yield, ver el resto del proyecto — esto
/// prueba por separado las partes que SÍ son síncronas de punta a punta:
/// el arranque de EntrarCutsceneNivel2 (boss instanciado y descendiendo),
/// AsegurarFormaPrecisa como función pura/idempotente (el beat más
/// importante, la transformación), y los dos caminos de salto instantáneo
/// (cutsceneAperturaVistaSesion ya en true, y el botón "Saltar" a mitad de
/// la cutscene real) — ambos totalmente síncronos por diseño, así que sí
/// se pueden probar de punta a punta.
///
/// SESIÓN, NO PLAYERPREFS (Fase 1, punto 5 del pedido grande): a
/// diferencia de la cutscene de cierre, la de apertura ya NO usa un flag
/// persistente — "que sea skippeable más no automáticamente skippeable"
/// significa que reintentar tras morir salta directo (mismo GameManager,
/// mismo proceso), pero una sesión nueva siempre la muestra. Por eso acá
/// no hace falta guardar/restaurar PlayerPrefs para esta cutscene: se
/// fuerza el campo de instancia directo por reflexión (ForzarVistaSesion).
/// </summary>
public static class VerificarCutsceneApertura
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Cutscene de Apertura")]
    public static void Verificar() => EjecutarPruebas();

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

    static void ForzarVistaSesion(GameManager gm, bool vista) =>
        typeof(GameManager).GetField("cutsceneAperturaVistaSesion", Flags).SetValue(gm, vista);

    static void EjecutarPruebas()
    {
        // --- A. EntrarCutsceneNivel2, primera vez (no vista) — arranque síncrono ---
        var gmA = AbrirEscenaFresca();
        if (gmA.administradorPrefab == null || gmA.formaPrecisaPrefab == null)
        {
            Debug.LogError("FALLÓ: falta administradorPrefab o formaPrecisaPrefab en GameManager — correr antes 'Crear Administrador del Sistema' y 'Crear Prefab Forma Precisa'.");
            return;
        }
        ForzarVistaSesion(gmA, false);

        gmA.BotonJugarNivel2();
        var campoModo = typeof(GameManager).GetField("modoNivel2", Flags);
        bool modo = (bool)campoModo.GetValue(gmA);
        int bossesA = Object.FindObjectsByType<AdministradorSistema>(FindObjectsSortMode.None).Length;
        Debug.Log($"Tras BotonJugarNivel2() (primera vez): estado={gmA.estado} (esperado Cutscene), modoNivel2={modo} (esperado true), panelCutsceneNivel2 activo={gmA.panelCutsceneNivel2.activeSelf}, botonSaltar activo={gmA.botonSaltarCutscene.activeSelf}, boss instanciados={bossesA} (esperado 1)");
        if (gmA.estado != EstadoJuego.Cutscene || !modo || !gmA.panelCutsceneNivel2.activeSelf || !gmA.botonSaltarCutscene.activeSelf || bossesA != 1)
            Debug.LogError("FALLÓ: el arranque síncrono de la cutscene no dejó el estado esperado.");
        else
            Debug.Log("OK: BotonJugarNivel2() arranca la cutscene real (Cutscene, boss descendiendo, UI mostrada).");

        // Captura de la caja de diálogo — MostrarDialogo() vive después del
        // primer yield (no tickeable acá), así que se fuerza el texto a
        // mano solo para revisar el layout, mismo patrón que
        // CapturaIntensidad.cs usa para estados que no se pueden tickear.
        if (gmA.panelMenu != null) gmA.panelMenu.SetActive(false);
        // El fundido a negro (Fase 1, punto 4) queda encendido tras
        // BotonJugarNivel2() hasta que un Update() lo apague, y acá no se
        // tickea — sacarlo del medio a mano para la captura.
        if (gmA.imagenFundido != null) gmA.imagenFundido.gameObject.SetActive(false);
        gmA.textoDialogoCutscene.text = "¿Qué eres?";
        gmA.textoDialogoCutscene.gameObject.SetActive(true);
        CapturarYGuardar(gmA, "cutscene_nivel2_dialogo.png");

        // --- B. AsegurarFormaPrecisa — la transformación en sí, probada aparte, con idempotencia ---
        var gmB = AbrirEscenaFresca();
        var nucleoTest = Object.Instantiate(gmB.nucleoPrefab, Vector3.zero, Quaternion.identity);
        nucleoTest.radio = 0.5f;
        Invocar(nucleoTest, "Awake");
        Invocar(nucleoTest, "OnEnable");

        var metodoAsegurar = typeof(GameManager).GetMethod("AsegurarFormaPrecisa", Flags);
        var campoNucleosActivos = typeof(GameManager).GetField("nucleosActivos", Flags);
        // HashSet<T> NO implementa la ICollection no-genérica (a diferencia
        // de List<T>) — hace falta el tipo concreto acá, no una interfaz.
        System.Collections.Generic.HashSet<Nucleo> LeerNucleosActivos() => (System.Collections.Generic.HashSet<Nucleo>)campoNucleosActivos.GetValue(gmB);

        metodoAsegurar.Invoke(gmB, new object[] { Vector2.zero });
        var campoFormaPrecisaActiva = typeof(GameManager).GetField("formaPrecisaActiva", Flags);
        var fp1 = (FormaPrecisa)campoFormaPrecisaActiva.GetValue(gmB);
        Debug.Log($"Tras 1er AsegurarFormaPrecisa: nucleosActivos={LeerNucleosActivos().Count}, FormaPrecisaActiva!=null: {fp1 != null}");
        if (LeerNucleosActivos().Count != 0 || fp1 == null)
            Debug.LogError("FALLÓ: la transformación debería vaciar nucleosActivos e instanciar la Forma Precisa.");
        else
            Debug.Log("OK: el enjambre se destruye y la Forma Precisa aparece en su lugar.");

        metodoAsegurar.Invoke(gmB, new object[] { Vector2.zero }); // 2do llamado — debe ser no-op
        int formasActivas = Object.FindObjectsByType<FormaPrecisa>(FindObjectsSortMode.None).Length;
        Debug.Log($"Tras un 2do AsegurarFormaPrecisa (debería ser no-op): instancias de FormaPrecisa en escena={formasActivas} (esperado 1)");
        if (formasActivas != 1)
            Debug.LogError("FALLÓ: AsegurarFormaPrecisa no es idempotente — creó una segunda Forma Precisa.");
        else
            Debug.Log("OK: idempotente, no duplica la transformación.");

        if (gmB.panelMenu != null) gmB.panelMenu.SetActive(false);

        // Limpieza para la captura: el Nucleo de prueba de más arriba ya
        // está lógicamente destruido (nucleosActivos lo perdió), pero
        // Destroy() es diferido — sin tickear un frame real (no pasa en
        // batch mode) seguía RENDERIZANDO como un círculo blanco de
        // sobra encima de la Forma Precisa. Mismo tipo de artefacto que
        // ya se vio con los LaserHazard de VerificarNivel2.cs.
        foreach (var viejo in Object.FindObjectsByType<Nucleo>(FindObjectsSortMode.None))
            Object.DestroyImmediate(viejo.gameObject);

        // Invoca Awake() a mano SOLO para esta captura — Instantiate() en
        // batch mode (sin Play Mode real) no dispara Awake/OnEnable solo,
        // mismo motivo por el que todo el resto de este archivo lo hace a
        // mano (ver el comentario del header). En juego real (Play Mode)
        // GameManager.AsegurarFormaPrecisa() no necesita esto — Awake()
        // corre solo, igual que ya le pasa a CrearFragmento con Nucleo.
        Invocar(fp1, "Awake");
        // El color real (colorApagado/colorVivido según Intensidad) recién
        // se aplica en Update() -> ActualizarVisual(), mismo patrón que
        // Nucleo — se fuerza acá también, solo para que la captura no
        // muestre el blanco default del SpriteRenderer.
        typeof(FormaPrecisa).GetMethod("ActualizarVisual", Flags).Invoke(fp1, new object[] { false });

        // AdministradorSistema no declara Awake() (no tiene setup propio,
        // sus campos ya vienen serializados en el prefab) — a diferencia
        // de FormaPrecisa/Nucleo/Enemigo, acá no hace falta invocarlo.
        if (gmB.administradorPrefab != null)
            Object.Instantiate(gmB.administradorPrefab, new Vector3(0f, gmB.mitadAlto * 0.5f, 0f), Quaternion.identity);

        CapturarYGuardar(gmB, "cutscene_nivel2_transformacion.png");

        // --- C. Ya vista antes EN ESTA SESIÓN — EntrarCutsceneNivel2 resuelve todo de una, sin corrutina ---
        var gmC = AbrirEscenaFresca();
        ForzarVistaSesion(gmC, true);
        gmC.BotonJugarNivel2();
        bool modoC = (bool)campoModo.GetValue(gmC);
        var fpC = (FormaPrecisa)campoFormaPrecisaActiva.GetValue(gmC);
        Debug.Log($"Tras BotonJugarNivel2() (ya vista en sesión): estado={gmC.estado} (esperado Jugando), modoNivel2={modoC}, panelCutsceneNivel2 activo={gmC.panelCutsceneNivel2.activeSelf} (esperado false), FormaPrecisaActiva!=null: {fpC != null}");
        if (gmC.estado != EstadoJuego.Jugando || !modoC || gmC.panelCutsceneNivel2.activeSelf || fpC == null)
            Debug.LogError("FALLÓ: con la cutscene ya vista en la sesión, debería resolverse todo de una, sin pasar por Cutscene.");
        else
            Debug.Log("OK: ya vista en sesión -> salto instantáneo directo a jugable, sin repetir la cutscene entera.");

        // --- D. Botón "Saltar" a mitad de la cutscene real (no vista) ---
        var gmD = AbrirEscenaFresca();
        ForzarVistaSesion(gmD, false);
        gmD.BotonJugarNivel2(); // arranca la corrutina real, stuck tras el primer yield (Descender)
        gmD.SaltarCutscene();
        var fpD = (FormaPrecisa)campoFormaPrecisaActiva.GetValue(gmD);
        Debug.Log($"Tras Saltar() a mitad de la cutscene: estado={gmD.estado} (esperado Jugando), panelCutsceneNivel2 activo={gmD.panelCutsceneNivel2.activeSelf} (esperado false), FormaPrecisaActiva!=null: {fpD != null}");
        if (gmD.estado != EstadoJuego.Jugando || gmD.panelCutsceneNivel2.activeSelf || fpD == null)
            Debug.LogError("FALLÓ: Saltar() debería terminar la cutscene de golpe (transformación incluida), no dejarla a medias.");
        else
            Debug.Log("OK: Saltar() resuelve el resto de la cutscene de una, no la deja colgada.");

        // --- E. Morir y reintentar (mismo GameManager, sin reabrir el juego): la cutscene NO debería repetirse ---
        // Simula BotonReintentar() tal cual lo llama Panel Fin: mismo
        // GameManager de D (ya la saltó una vez en esta sesión), la Forma
        // Precisa "murió" (se destruye a mano, igual que ManejarDerrotaNivel2),
        // y se vuelve a entrar. El pedido explícito era justamente esto:
        // "si el jugador muere en la pelea y reintenta, el reintento entra
        // DIRECTO a la pelea, sin cutscene" — sin que dependa de haber
        // guardado nada en disco.
        if (fpD != null) Object.DestroyImmediate(fpD.gameObject);
        typeof(GameManager).GetField("formaPrecisaActiva", Flags).SetValue(gmD, null);
        gmD.BotonReintentar();
        var fpE = (FormaPrecisa)campoFormaPrecisaActiva.GetValue(gmD);
        Debug.Log($"Tras BotonReintentar() (mismo proceso, ya vista): estado={gmD.estado} (esperado Jugando), panelCutsceneNivel2 activo={gmD.panelCutsceneNivel2.activeSelf} (esperado false), FormaPrecisaActiva!=null: {fpE != null}");
        if (gmD.estado != EstadoJuego.Jugando || gmD.panelCutsceneNivel2.activeSelf || fpE == null)
            Debug.LogError("FALLÓ: reintentar tras morir en la misma sesión debería entrar directo a la pelea, sin repetir la cutscene.");
        else
            Debug.Log("OK: reintentar en la misma sesión saltea la cutscene — nunca se obliga a verla dos veces por progreso.");

        Debug.Log("Verificación de la cutscene de apertura completa.");
    }

    static void CapturarYGuardar(GameManager gm, string nombreArchivo)
    {
        var cam = Camera.main;
        if (cam == null) { Debug.LogError("VerificarCutsceneApertura: no encontré Camera.main para capturar."); return; }

        int w = 960, h = 540;
        var rt = new RenderTexture(w, h, 24);
        var prevRT = cam.targetTexture;
        var prevActive = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        cam.targetTexture = prevRT;
        RenderTexture.active = prevActive;
        Object.DestroyImmediate(rt);

        var dir = Path.Combine(Application.dataPath, "..", "..", "capturas");
        Directory.CreateDirectory(dir);
        var ruta = Path.Combine(dir, nombreArchivo);
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("VerificarCutsceneApertura: captura guardada en " + ruta);
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
