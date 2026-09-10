using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Herramienta de verificación puntual: activa Panel Fin con texto de
/// ejemplo (multilinea, como lo deja TerminarPartida real) para revisar
/// el layout sin tener que jugar una partida completa. No guarda la escena.
/// </summary>
public static class CapturaFinPrueba
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Capturar Fin con datos de prueba (sin Play)")]
    public static void Capturar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();
        var menu = raices.First(g => g.name == "Panel Menú");
        var fin = raices.First(g => g.name == "Panel Fin");
        menu.SetActive(false);
        fin.SetActive(true);

        var resultado = fin.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "T_Resultado");
        var record = fin.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "T_Record");
        var esencia = fin.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "T_Esencia");
        resultado.text = "Sobreviviste 47.3 s\nCombo máximo: 23\nNúcleos máximo: 6";
        record.text = "Nuevo récord de tiempo\nRécord combo: 31\nRécord núcleos: 8";
        if (esencia != null) esencia.text = "+92 Esencia";

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
        var ruta = Path.Combine(dir, "fin_prueba.png");
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("CapturaFinPrueba: guardada en " + ruta);
    }
}
