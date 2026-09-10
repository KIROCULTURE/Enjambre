using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verificación puntual del "modo fiesta" del fondo durante el Fever (sin
/// Play Mode). Como el suavizado de nivelFeverSuavizado depende de
/// Time.unscaledDeltaTime (que no avanza fuera de Play Mode), se lo
/// pisa directo por reflection para simular "Fever a tope" en vez de
/// depender del paso real del tiempo. Captura una versión sin Fever y
/// otra con Fever a nivel máximo, mismo encuadre, para comparar. No
/// guarda la escena.
/// </summary>
public static class CapturaModoFiesta
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Capturar Modo Fiesta (sin Play)")]
    public static void Capturar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var camGo = raices.First(g => g.name == "Camera");
        var cam = camGo.GetComponent<Camera>();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        var fondoGo = raices.First(g => g.GetComponent<FondoNebulosa>() != null);
        var fondo = fondoGo.GetComponent<FondoNebulosa>();
        var panelMenu = raices.First(g => g.name == "Panel Menú");
        panelMenu.SetActive(false);

        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");
        Invocar(fondo, "Awake");

        var campoSuavizado = typeof(FondoNebulosa).GetField("nivelFeverSuavizado", Flags);

        campoSuavizado.SetValue(fondo, 1f);
        Invocar(fondo, "Update");
        Guardar(cam, "fiesta_off_editmode.png");

        campoSuavizado.SetValue(fondo, 5f); // nivelFeverMax
        Invocar(fondo, "Update");
        Guardar(cam, "fiesta_on_editmode.png");

        Debug.Log("CapturaModoFiesta: listo.");
    }

    static void Guardar(Camera cam, string nombreArchivo)
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
        File.WriteAllBytes(Path.Combine(dir, nombreArchivo), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
