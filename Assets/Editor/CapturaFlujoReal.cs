using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verificación puntual: a diferencia de CapturaIntensidad (que fuerza
/// estado/HUD a mano para simular un instante), esto llama al flujo real
/// GameManager.BotonJugar() → EmpezarPartida(), igual que si el jugador
/// hubiese tocado "Jugar", para confirmar que el HUD/estado real quedan
/// bien sin depender de ningún atajo de prueba. No guarda la escena.
/// </summary>
public static class CapturaFlujoReal
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Capturar Flujo Real de Jugar (sin Play)")]
    public static void Capturar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();

        typeof(GameManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, null);

        // Exactamente lo que dispara el botón "Jugar" del menú — nada de
        // tocar hudRoot/estado a mano.
        gm.BotonJugar();

        // Instantiate en Edit Mode no siempre corre Awake() sincrónico (ver
        // CapturaIntensidad.cs); se fuerza acá también para que los
        // sprites del núcleo/orbes recién creados no queden en blanco por
        // sr.sprite==null, y un Update() de GameManager para que el HUD
        // deje de mostrar el placeholder "Record" tipeado a mano.
        foreach (var n in Object.FindObjectsByType<Nucleo>(FindObjectsSortMode.None))
        {
            typeof(Nucleo).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(n, null);
            typeof(Nucleo).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(n, null);
        }
        foreach (var o in Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None))
            typeof(Orbe).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, null);
        typeof(GameManager).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, null);

        int w = 960, h = 600;
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
        var ruta = Path.Combine(dir, "flujo_real_jugar.png");
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("CapturaFlujoReal: guardada en " + ruta + $" (hudRoot activo={gm.hudRoot.activeInHierarchy})");
    }
}
