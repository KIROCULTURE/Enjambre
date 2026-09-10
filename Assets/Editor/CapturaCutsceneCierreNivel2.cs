using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Captura una composición representativa de la cutscene de cierre del
/// Nivel 2 (punto 5): boss y forma de Nivel 3 (placeholder) ya
/// reposicionados a esquinas opuestas por el campo de fuerza, con el
/// láser final (dorado, nunca antes usado) cruzando por la esquina del
/// jugador — el beat más grande del cierre. Armado a mano en vez de
/// tickeando CutsceneCierreNivel2() de verdad (no tickeable en batch mode
/// más allá del primer yield, ver VerificarPeleaNivel2.cs). No guarda la
/// escena ni toca PlayerPrefs reales más que lo que ya hace
/// BotonJugarNivel2 (protegido igual que el resto de los debug de Nivel 2).
/// </summary>
public static class CapturaCutsceneCierreNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string ClaveCutsceneCierreVista = "enjambre_cutscene_cierre_vista";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Capturar Cutscene de Cierre Nivel 2")]
    public static void Capturar()
    {
        bool claveCierreExistia = PlayerPrefs.HasKey(ClaveCutsceneCierreVista);
        int valorCierreOriginal = claveCierreExistia ? PlayerPrefs.GetInt(ClaveCutsceneCierreVista) : 0;
        try
        {
            EjecutarCaptura();
        }
        finally
        {
            if (claveCierreExistia) PlayerPrefs.SetInt(ClaveCutsceneCierreVista, valorCierreOriginal);
            else PlayerPrefs.DeleteKey(ClaveCutsceneCierreVista);
            PlayerPrefs.Save();
            MetaProgreso.Cargar();
        }
    }

    static void EjecutarCaptura()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();
        gm.estado = EstadoJuego.Menu;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        // Apertura: flag de SESIÓN (no PlayerPrefs, ver
        // GameManager.cutsceneAperturaVistaSesion). Cierre: sigue siendo
        // PlayerPrefs real, restaurado en el finally de arriba.
        typeof(GameManager).GetField("cutsceneAperturaVistaSesion", Flags).SetValue(gm, true);
        PlayerPrefs.SetInt(ClaveCutsceneCierreVista, 0);
        PlayerPrefs.Save();
        MetaProgreso.Cargar();

        gm.BotonJugarNivel2(); // salta la cutscene de apertura (ya "vista" en sesión) directo a la pelea: boss + Forma Precisa en escena

        // Ver el mismo comentario en CapturaPeleaNivel2.cs: el fundido a
        // negro (Fase 1, punto 4) queda encendido tras BotonJugarNivel2()
        // hasta que un Update() lo apague, y acá no se tickea.
        if (gm.imagenFundido != null) gm.imagenFundido.gameObject.SetActive(false);

        var campoFormaPrecisaActiva = typeof(GameManager).GetField("formaPrecisaActiva", Flags);
        var boss = Object.FindFirstObjectByType<AdministradorSistema>();

        Vector2 esquinaBoss = new Vector2(-gm.mitadAncho * 0.75f, gm.mitadAlto * 0.75f);
        Vector2 esquinaJugador = new Vector2(gm.mitadAncho * 0.75f, -gm.mitadAlto * 0.75f);

        // Reposiciona al boss a su esquina — mismo destino final que
        // ReposicionarACorners, sin tickear el tween.
        if (boss != null) boss.transform.position = esquinaBoss;

        // El láser final — CrearHazVisual (privado) ya deja el sprite en
        // su estado de brillo pleno, sin telegraph que tickear.
        var metodoHaz = typeof(GameManager).GetMethod("CrearHazVisual", Flags);
        const float grosor = 1.4f;
        metodoHaz.Invoke(gm, new object[] { new Rect(esquinaJugador.x - grosor * 0.5f, -gm.mitadAlto * 1.3f, grosor, gm.mitadAlto * 2.6f), ColorFinalNivel3(gm), 5f });
        metodoHaz.Invoke(gm, new object[] { new Rect(-gm.mitadAncho * 1.3f, esquinaJugador.y - grosor * 0.5f, gm.mitadAncho * 2.6f, grosor), ColorFinalNivel3(gm), 5f });

        // La transformación en sí — destruye la Forma Precisa y deja el
        // placeholder de Nivel 3 en la esquina del jugador.
        var metodoAsegurar = typeof(GameManager).GetMethod("AsegurarFormaNivel3", Flags);
        metodoAsegurar.Invoke(gm, new object[] { esquinaJugador });

        if (gm.panelMenu != null) gm.panelMenu.SetActive(false);
        if (gm.panelCutsceneNivel2 != null) gm.panelCutsceneNivel2.SetActive(false);

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
        var ruta = Path.Combine(dir, "cutscene_nivel2_cierre.png");
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("CapturaCutsceneCierreNivel2: captura guardada en " + ruta);
    }

    static Color ColorFinalNivel3(GameManager gm) =>
        (Color)typeof(GameManager).GetField("ColorFinalNivel3", Flags | BindingFlags.Static).GetValue(null);

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
