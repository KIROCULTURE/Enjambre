using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Captura el momento del colapso del boss (Prioridad 1b, revisión nocturna 2026-09-10) — popup "¡COLAPSA!" + glitch bajo y constante.</summary>
public static class CapturaColapsoAgonicoNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Capturar Colapso Agónico Nivel 2")]
    public static void Capturar()
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

        typeof(GameManager).GetField("vidaBossNivel2", Flags).SetValue(gm, gm.umbralColapsoBossNivel2 + 1f);
        typeof(GameManager).GetField("tPelea", Flags).SetValue(gm, 47.5f);
        typeof(GameManager).GetMethod("AplicarDanoBossNivel2", Flags).Invoke(gm, new object[] { 999f });

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
        var ruta = Path.Combine(dir, "colapso_agonico_nivel2.png");
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log($"CapturaColapsoAgonicoNivel2: captura guardada en {ruta}");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
