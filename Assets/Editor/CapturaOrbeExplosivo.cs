using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Verificación visual puntual del orbe explosivo (sin Play Mode). No guarda la escena.</summary>
public static class CapturaOrbeExplosivo
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Capturar Orbe Explosivo (sin Play)")]
    public static void Capturar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        var panelMenu = raices.First(g => g.name == "Panel Menú");
        panelMenu.SetActive(false);

        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");

        var normal = Object.Instantiate(gm.orbePrefab, new Vector3(-0.9f, 0.4f, 0f), Quaternion.identity);
        Invocar(normal, "Awake");

        var velocidad = Object.Instantiate(gm.orbePrefab, new Vector3(0f, 0.4f, 0f), Quaternion.identity);
        Invocar(velocidad, "Awake");
        velocidad.MarcarComoVelocidad();

        var explosivo = Object.Instantiate(gm.orbePrefab, new Vector3(0.9f, 0.4f, 0f), Quaternion.identity);
        Invocar(explosivo, "Awake");
        explosivo.MarcarComoExplosivo();

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
        File.WriteAllBytes(Path.Combine(dir, "orbe_explosivo_editmode.png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("CapturaOrbeExplosivo: listo.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
