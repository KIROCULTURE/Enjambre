using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Captura la terminal de escalada de privilegios (Fase 7) en dos momentos
/// representativos, a mano en vez de tickeando la corrutina real (no
/// tickeable en batch mode más allá de su primer yield): (1) a mitad de la
/// súplica del Administrador, terminal encendida sobre la pelea; (2) justo
/// tras la transformación a Super Administrador (AsegurarSuperAdministradorNivel2
/// SÍ es síncrona de punta a punta, así que esta segunda foto es 100% fiel).
/// </summary>
public static class CapturaTerminalNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Capturar Terminal Nivel 2 (Fase 7)")]
    public static void Capturar()
    {
        CapturarTerminal();
        CapturarTransformacion();
        CapturarGlitchForzado();
    }

    static (GameManager gm, Camera cam) PrepararEscenaEnPelea()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();
        gm.estado = EstadoJuego.Menu;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        typeof(GameManager).GetField("cutsceneAperturaVistaSesion", Flags).SetValue(gm, true);
        gm.BotonJugarNivel2();
        if (gm.imagenFundido != null) gm.imagenFundido.gameObject.SetActive(false);

        var fp = (FormaPrecisa)typeof(GameManager).GetField("formaPrecisaActiva", Flags).GetValue(gm);
        if (fp != null)
        {
            Invocar(fp, "Awake");
            typeof(FormaPrecisa).GetMethod("ActualizarVisual", Flags).Invoke(fp, new object[] { false });
            fp.transform.position = new Vector3(-1.5f, -1.8f, 0f);
        }
        if (gm.panelMenu != null) gm.panelMenu.SetActive(false);
        return (gm, cam);
    }

    static void CapturarTerminal()
    {
        var (gm, cam) = PrepararEscenaEnPelea();

        // Arranca la escalada real (síncrono hasta su primer yield real) y
        // fuerza el texto al estado "súplica sostenida" — el beat más
        // importante de la secuencia, el que tiene que mostrarse en la
        // captura (ver el comentario de header de la sección Fase 7 en
        // GameManager.cs sobre por qué NO se tipea carácter a carácter).
        typeof(GameManager).GetMethod("IniciarEscaladaPrivilegiosNivel2", Flags).Invoke(gm, null);
        if (gm.textoTerminalNivel2 != null)
            gm.textoTerminalNivel2.text = "$ sudo escalate --scope=root\n[sistema] acceso concedido: root\n\n> No sé quién es tu creador, ni qué quieres de nosotros, pero por favor deja en paz este sistema.";

        GuardarCaptura(cam, "terminal_nivel2.png");
    }

    static void CapturarTransformacion()
    {
        var (gm, cam) = PrepararEscenaEnPelea();

        // AsegurarSuperAdministradorNivel2 es 100% síncrona (ver su
        // comentario) — invocarla directo da una foto fiel del resultado
        // real, sin necesidad de forzar nada a mano.
        typeof(GameManager).GetMethod("AsegurarSuperAdministradorNivel2", Flags).Invoke(gm, null);
        if (gm.panelTerminalNivel2 != null) gm.panelTerminalNivel2.SetActive(false);

        GuardarCaptura(cam, "super_administrador_nivel2.png");
    }

    /// <summary>
    /// Punto 9: prueba de verdad de que el paso del shader CORRE (no solo
    /// que compila — el sweep ya grepea "Shader error" para eso, pero un
    /// shader que compila y no dibuja nada no lo detecta, pedido vía
    /// revisión). AnimarGlitchTransformacionNivel2 es puramente cosmético
    /// (Time.deltaTime no es controlable en batch mode) y en la práctica
    /// queda con _Intensidad≈0 en esta misma corrida por eso — así que acá
    /// se fuerza _Intensidad=1 a mano vía el MISMO MaterialPropertyBlock
    /// que usa la corrutina real, para una captura que sí demuestra que el
    /// paso dibuja algo distinto del sprite normal.
    /// </summary>
    static void CapturarGlitchForzado()
    {
        var (gm, cam) = PrepararEscenaEnPelea();
        typeof(GameManager).GetMethod("AsegurarSuperAdministradorNivel2", Flags).Invoke(gm, null);
        if (gm.panelTerminalNivel2 != null) gm.panelTerminalNivel2.SetActive(false);

        var boss = Object.FindFirstObjectByType<AdministradorSistema>();
        if (boss != null && boss.sr != null && gm.materialCorrupcionGlitchNivel2 != null)
        {
            var mpb = new MaterialPropertyBlock();
            boss.sr.GetPropertyBlock(mpb);
            mpb.SetFloat(Shader.PropertyToID("_Intensidad"), 1f);
            boss.sr.SetPropertyBlock(mpb);
        }
        else
        {
            Debug.LogWarning("CapturaTerminalNivel2: materialCorrupcionGlitchNivel2 sin asignar — correr antes 'Enjambre/Crear Materiales de Shaders'. Esta captura va a salir igual a super_administrador_nivel2.png.");
        }

        GuardarCaptura(cam, "super_administrador_glitch_forzado.png");
    }

    static void GuardarCaptura(Camera cam, string archivo)
    {
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
        var ruta = Path.Combine(dir, archivo);
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log($"CapturaTerminalNivel2: captura guardada en {ruta}");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
