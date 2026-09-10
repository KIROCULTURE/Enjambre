using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verificación visual puntual del bloom (sin Play Mode): fuerza
/// Intensidad=1 (colores HDR "vívido" al máximo) en un núcleo y una gema, y
/// confirma que el texto de la UI (blanco plano, no HDR) se ve limpio al
/// lado sin halo. No guarda la escena.
/// </summary>
public static class CapturaBloom
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Capturar Bloom (sin Play)")]
    public static void Capturar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        var panelMenu = raices.First(g => g.name == "Panel Menú");
        var hudGo = raices.First(g => g.name == "HUD");
        panelMenu.SetActive(false);
        hudGo.SetActive(true);

        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        // Fuerza tiempo al clímax (Intensidad=1) — así colorVivido (HDR) se
        // aplica al 100%, no una mezcla apagado/vívido a mitad de camino.
        typeof(GameManager).GetField("tiempo", Flags).SetValue(gm, gm.climaxSegundos);

        var nucleo = Object.Instantiate(gm.nucleoPrefab, new Vector3(-1f, 0.3f, 0f), Quaternion.identity);
        nucleo.radio = 0.55f;
        Invocar(nucleo, "Awake");
        Invocar(nucleo, "OnEnable");
        Invocar(nucleo, "Update");

        var orbe = Object.Instantiate(gm.orbePrefab, new Vector3(1f, 0.3f, 0f), Quaternion.identity);
        Invocar(orbe, "Awake");
        Invocar(orbe, "Update");

        hud.Actualizar(gm.climaxSegundos, 1, 0f, 1);

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
        File.WriteAllBytes(Path.Combine(dir, "bloom_editmode.png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("CapturaBloom: listo.");
    }

    static HudController hud => Object.FindFirstObjectByType<HudController>();

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
