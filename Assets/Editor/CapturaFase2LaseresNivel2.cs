using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Captura composiciones representativas de la Fase 2 (Super
/// Administrador, punto 8): el boss ya transformado + patrones forzados al
/// estado YA RESUELTO (brillo pleno) en vez del telegraph tenue — mismo
/// truco que CapturaPeleaNivel2.cs. No tickea FaseLaseresYVictoriaNivel2()
/// (corrutina con while, no tickeable más allá del primer yield en batch
/// mode): arma los muros a mano llamando directo a los patrones reales.
/// Tres variantes: los 4 heredados (Cruz+Corredor), y los 2 propios del
/// boss agregados vía revisión (Tela Radial, Espiral Giratoria).
///
/// UNA sola apertura de escena para las tres (a propósito — bug real
/// encontrado armando este mismo archivo): ShapeFactory cachea sus Sprite/
/// Texture2D en un diccionario static que sobrevive entre llamadas DENTRO
/// del mismo proceso batch, pero los objetos Unity que cachea NO sobreviven
/// un EditorSceneManager.OpenScene() — abrir la escena de nuevo los deja
/// "fake null" (la referencia C# sigue sin ser null, pero el objeto nativo
/// ya no existe), y sr.sprite queda vacío sin ningún error ni excepción,
/// solo un LaserHazard invisible. Nunca pasa en una partida real (ahí la
/// escena nunca se reabre desde cero) — es puramente un artefacto de este
/// script capturando varias veces en el mismo proceso.
/// </summary>
public static class CapturaFase2LaseresNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Capturar Fase 2 Láseres Nivel 2 (Fase 8)")]
    public static void Capturar()
    {
        var (gm, cam) = PrepararEscenaFase2();

        CapturarConPatrones(gm, cam, "fase2_laseres_nivel2.png", () =>
        {
            Invocar(gm, "PatronCruz");
            Invocar(gm, "PatronCorredor");
        });
        CapturarConPatrones(gm, cam, "fase2_tela_radial_nivel2.png", () => Invocar(gm, "PatronTelaRadialFase2"));
        CapturarConPatrones(gm, cam, "fase2_espiral_giratoria_nivel2.png", () => Invocar(gm, "PatronEspiralGiratoriaFase2"));
        CapturarConPatrones(gm, cam, "fase2_nodos_itinerantes_nivel2.png", () => Invocar(gm, "PatronNodosItinerantesFase2"));
    }

    static (GameManager, Camera) PrepararEscenaFase2()
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
        if (gm.panelMenu != null) gm.panelMenu.SetActive(false);

        var fp = (FormaPrecisa)typeof(GameManager).GetField("formaPrecisaActiva", Flags).GetValue(gm);
        if (fp != null)
        {
            Invocar(fp, "Awake");
            typeof(FormaPrecisa).GetMethod("ActualizarVisual", Flags).Invoke(fp, new object[] { false });
            fp.transform.position = new Vector3(-0.6f, -0.4f, 0f);
        }

        // La transformación real (100% síncrona) — deja el boss recoloreado/
        // agrandado, sin barra de vida ni orbes, tal cual se ve en la Fase 2.
        Invocar(gm, "AsegurarSuperAdministradorNivel2");
        if (gm.panelTerminalNivel2 != null) gm.panelTerminalNivel2.SetActive(false);
        // Fuerza el glitch a un valor visible (ver CapturaTerminalNivel2.CapturarGlitchForzado
        // — la corrutina real casi no avanza en batch mode) para que estas
        // capturas también muestren el "más amenazante" sostenido de la
        // Fase 2, no solo el sprite recoloreado.
        if (gm.materialCorrupcionGlitchNivel2 != null)
        {
            var boss = Object.FindFirstObjectByType<AdministradorSistema>();
            if (boss != null && boss.sr != null)
            {
                var mpbGlitch = new MaterialPropertyBlock();
                boss.sr.GetPropertyBlock(mpbGlitch);
                mpbGlitch.SetFloat(Shader.PropertyToID("_Intensidad"), 0.35f);
                boss.sr.SetPropertyBlock(mpbGlitch);
            }
        }

        return (gm, cam);
    }

    static void CapturarConPatrones(GameManager gm, Camera cam, string archivo, System.Action dispararPatrones)
    {
        // Limpia los muros de la captura anterior — misma escena, mismo
        // GameManager, así que si no se limpian se acumulan y cada
        // captura siguiente sale más cargada que la anterior.
        foreach (var viejo in Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None))
            Object.DestroyImmediate(viejo.gameObject);

        dispararPatrones();
        foreach (var muro in Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None))
        {
            var srMuro = (SpriteRenderer)typeof(LaserHazard).GetField("sr", Flags).GetValue(muro);
            var mpbMuro = (MaterialPropertyBlock)typeof(LaserHazard).GetField("mpb", Flags).GetValue(muro);
            if (mpbMuro != null) { mpbMuro.SetFloat("_Progreso", 1f); srMuro.SetPropertyBlock(mpbMuro); }
            else if (srMuro != null) srMuro.color = Color.white;
        }

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
        Debug.Log($"CapturaFase2LaseresNivel2: captura guardada en {ruta}");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
