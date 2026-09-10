using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Verificación visual puntual: capturas del Menú principal (con el nuevo
/// grid 2x2 de botones secundarios, incluido Mejoras) y del panel Mejoras
/// con valores de ejemplo. FondoNebulosa (nebulosa + estrellas titilando
/// detrás del menú) es un MonoBehaviour de escena cuyo Awake() no corre
/// solo en batch mode (mismo gotcha de siempre en este proyecto) — se
/// invoca a mano acá para que la captura muestre el fondo real, no un
/// rectángulo plano. No guarda la escena.
/// </summary>
public static class CapturaMenuYMejoras
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Capturar Menú y Mejoras (sin Play)")]
    public static void Capturar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();
        var menu = raices.First(g => g.name == "Panel Menú");
        var mejoras = raices.First(g => g.name == "Panel Mejoras");
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();

        gm.estado = EstadoJuego.Menu;
        Invocar(gm, "Awake");

        var fondo = Object.FindObjectsByType<FondoNebulosa>(FindObjectsSortMode.None).FirstOrDefault();
        if (fondo != null)
        {
            Invocar(fondo, "Awake");
            Invocar(fondo, "Update");
        }

        menu.SetActive(true);
        mejoras.SetActive(false);
        Guardar(cam, "menu_con_mejoras.png");

        menu.SetActive(false);
        mejoras.SetActive(true);
        // Texto de ejemplo puesto directo (sin tocar MetaProgreso real —
        // esto es solo una captura de layout, no debe alterar el save del
        // jugador), para ver el panel con números reales en vez de "0/0".
        var controlador = mejoras.GetComponent<ControladorMejoras>();
        controlador.textoEsencia.text = "Esencia: 245";
        controlador.textoNivelNucleo.text = "Nivel 2/5";
        controlador.textoCostoNucleo.text = "100 Esencia";
        controlador.textoNivelCrecimiento.text = "Nivel 0/5";
        controlador.textoCostoCrecimiento.text = "30 Esencia";
        controlador.textoNivelFever.text = "Nivel 5/5";
        controlador.textoCostoFever.text = "MÁXIMO";
        controlador.textoNivelGemas.text = "Nivel 1/5";
        controlador.textoCostoGemas.text = "50 Esencia";
        Guardar(cam, "panel_mejoras.png");

        menu.SetActive(true);
        mejoras.SetActive(false);
    }

    static void Guardar(Camera cam, string nombreArchivo)
    {
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
        var ruta = Path.Combine(dir, nombreArchivo);
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log($"CapturaMenuYMejoras: guardada {nombreArchivo} en " + ruta);
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
