using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Verificación puntual del destello "golpeado" (naranja) del núcleo. No guarda la escena.</summary>
public static class CapturaFlashGolpe
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Capturar Flash de Golpe (sin Play)")]
    public static void Capturar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        var panelMenu = raices.First(g => g.name == "Panel Menú");
        var panelFin = raices.First(g => g.name == "Panel Fin");
        var hudGo = raices.First(g => g.name == "HUD");
        panelMenu.SetActive(false);
        panelFin.SetActive(false);
        hudGo.SetActive(true);
        gm.estado = EstadoJuego.Jugando;
        typeof(GameManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, null);

        var nucleo = Object.Instantiate(gm.nucleoPrefab, Vector3.zero, Quaternion.identity);
        typeof(Nucleo).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(nucleo, null);
        typeof(Nucleo).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(nucleo, null);
        nucleo.MostrarGolpeado(0.3f);
        typeof(Nucleo).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(nucleo, null);

        int w = 500, h = 400;
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
        var ruta = Path.Combine(dir, "flash_golpe.png");
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("CapturaFlashGolpe: guardada en " + ruta);
    }
}
