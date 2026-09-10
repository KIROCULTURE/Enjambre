using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Captura tres FormaPrecisa lado a lado con distinta carga de energía
/// (0%, 50%, 100% — ver GameManager.ProcesarPickupOrbeNivel2/
/// FormaPrecisa.ActualizarCargaEnergia), para verificar a simple vista el
/// pedido explícito de Fase 3: "el jugador tiene la vista clavada en los
/// proyectiles, que la carga se lea en el jugador mismo, el HUD es
/// secundario". No tickea la pelea real ni toca orbes — llama
/// ActualizarCargaEnergia directo, mismo criterio que el resto de las
/// capturas de este proyecto para estados que dependen de una corrutina o
/// un montón de pickups reales. No guarda la escena.
/// </summary>
public static class CapturaEnergiaNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Capturar Energía Nivel 2")]
    public static void Capturar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();
        gm.estado = EstadoJuego.Menu;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        float[] fracciones = { 0f, 0.5f, 1f };
        float espaciado = 1.1f;
        for (int i = 0; i < fracciones.Length; i++)
        {
            float x = (i - 1) * espaciado;
            var fp = Object.Instantiate(gm.formaPrecisaPrefab, new Vector3(x, 0f, 0f), Quaternion.identity);
            Invocar(fp, "Awake");
            typeof(FormaPrecisa).GetMethod("ActualizarVisual", Flags).Invoke(fp, new object[] { false });
            fp.ActualizarCargaEnergia(fracciones[i]);
        }

        if (gm.panelMenu != null) gm.panelMenu.SetActive(false);
        if (gm.hudRoot != null) gm.hudRoot.SetActive(false);

        // Cámara acercada — a la escala normal del arena completa el aura
        // se ve como un puntito (mismo motivo que la 2da captura de
        // VerificarFormaPrecisa.cs). No se restaura: esta captura no
        // reusa la cámara para nada más después.
        bool eraOrtografica = cam.orthographic;
        float sizeOriginal = cam.orthographicSize;
        cam.orthographic = true;
        cam.orthographicSize = 1.1f;

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
        var ruta = Path.Combine(dir, "energia_nivel2.png");
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("CapturaEnergiaNivel2: captura guardada en " + ruta);
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
