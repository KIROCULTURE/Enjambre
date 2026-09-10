using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Captura el momento posterior a un pulso POTENTE (Fase 5): el boss
/// rodeado de los orbes generosos que soltó (GenerarOrbesDelBossNivel2),
/// invitando al jugador a arriesgarse a acercarse para encadenar otro
/// combo. Llama al método real en vez de tickear la corrutina completa de
/// la pelea. No guarda la escena.
/// </summary>
public static class CapturaPulsoPotenteNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Capturar Pulso Potente Nivel 2")]
    public static void Capturar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();
        gm.estado = EstadoJuego.Menu;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        var boss = Object.Instantiate(gm.administradorPrefab, new Vector3(0f, gm.mitadAlto * 0.5f, 0f), Quaternion.identity);
        typeof(GameManager).GetField("administradorActivo", Flags).SetValue(gm, boss);

        var fp = Object.Instantiate(gm.formaPrecisaPrefab, new Vector3(0f, -gm.mitadAlto * 0.6f, 0f), Quaternion.identity);
        Invocar(fp, "Awake");
        typeof(FormaPrecisa).GetMethod("ActualizarVisual", Flags).Invoke(fp, new object[] { false });
        typeof(GameManager).GetField("formaPrecisaActiva", Flags).SetValue(gm, fp);

        typeof(GameManager).GetMethod("GenerarOrbesDelBossNivel2", Flags).Invoke(gm, null);
        foreach (var o in Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None)) Invocar(o, "Awake");

        if (gm.panelMenu != null) gm.panelMenu.SetActive(false);
        if (gm.hudRoot != null) gm.hudRoot.SetActive(false);

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
        var ruta = Path.Combine(dir, "pulso_potente_nivel2.png");
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("CapturaPulsoPotenteNivel2: captura guardada en " + ruta);
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
