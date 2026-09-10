using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Captura una imagen de la cámara SIN entrar a Play Mode (evita el
/// cuelgue del indexador de Unity Search al entrar a Play en este
/// entorno). Como los Canvas ahora son Screen Space - Camera, esto
/// también muestra el HUD/menú/paneles, no solo el mundo 3D/2D.
/// </summary>
public static class CapturaEdit
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Capturar (sin Play)")]
    public static void Capturar() => CapturarInterno("menu_editmode");

    [MenuItem("Enjambre/Capturar ancha 16-9 (sin Play)")]
    public static void CapturarAncha() => CapturarInterno("menu_ancho", w: 1200, h: 560);

    [MenuItem("Enjambre/Capturar Fin (sin Play)")]
    public static void CapturarFin()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raiz = scene.GetRootGameObjects();
        var menu = System.Array.Find(raiz, g => g.name == "Panel Menú");
        var fin = System.Array.Find(raiz, g => g.name == "Panel Fin");
        if (menu != null) menu.SetActive(false);
        if (fin != null) fin.SetActive(true);
        CapturarInterno("fin_editmode", abrirEscena: false);
    }

    [MenuItem("Enjambre/Capturar Créditos (sin Play)")]
    public static void CapturarCreditos()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raiz = scene.GetRootGameObjects();
        var menu = System.Array.Find(raiz, g => g.name == "Panel Menú");
        var creditos = System.Array.Find(raiz, g => g.name == "Panel Créditos");
        if (menu != null) menu.SetActive(false);
        if (creditos != null) creditos.SetActive(true);
        CapturarInterno("creditos_editmode", abrirEscena: false);
    }

    [MenuItem("Enjambre/Capturar Instrucciones (sin Play)")]
    public static void CapturarInstrucciones()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raiz = scene.GetRootGameObjects();
        var menu = System.Array.Find(raiz, g => g.name == "Panel Menú");
        var instrucciones = System.Array.Find(raiz, g => g.name == "Panel Instrucciones");
        if (menu != null) menu.SetActive(false);
        if (instrucciones != null) instrucciones.SetActive(true);
        CapturarInterno("instrucciones_editmode", abrirEscena: false);
    }

    [MenuItem("Enjambre/Capturar Pausa (sin Play)")]
    public static void CapturarPausa()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raiz = scene.GetRootGameObjects();
        var menu = System.Array.Find(raiz, g => g.name == "Panel Menú");
        var pausa = System.Array.Find(raiz, g => g.name == "Panel Pausa");
        var hud = System.Array.Find(raiz, g => g.name == "HUD");
        if (menu != null) menu.SetActive(false);
        if (hud != null) hud.SetActive(true);
        if (pausa != null) pausa.SetActive(true);
        CapturarInterno("pausa_editmode", abrirEscena: false);
    }

    // 16:9 real (960x540), no 800x600 (4:3-ish): a esa resolución vieja la
    // altura efectiva del Canvas quedaba más generosa que en una ventana
    // ancha de verdad, y un panel que se veía bien ahí terminaba cortado
    // arriba/abajo jugando de verdad. Probar en 4:3 escondía justo el tipo
    // de problema que esto existe para atrapar.
    static void CapturarInterno(string nombreArchivo, bool abrirEscena = true, int w = 960, int h = 540)
    {
        if (abrirEscena) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var cam = GameObject.Find("Camera").GetComponent<Camera>();

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
        var ruta = Path.Combine(dir, nombreArchivo + ".png");
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("CapturaEdit: guardada en " + ruta);
    }
}
