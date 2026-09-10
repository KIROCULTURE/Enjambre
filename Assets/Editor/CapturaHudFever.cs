using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verificación visual puntual del HUD rediseñado (núcleos grande/azul,
/// Fever en Bangers animado) sin Play Mode. No guarda la escena.
/// </summary>
public static class CapturaHudFever
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Capturar HUD Fever (sin Play)")]
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

        var hud = hudGo.GetComponentInChildren<HudController>(true);
        hud.Actualizar(37.4f, 5, 52.1f, 4);

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
        File.WriteAllBytes(Path.Combine(dir, "hud_fever_editmode.png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("CapturaHudFever: listo.");
    }
}
