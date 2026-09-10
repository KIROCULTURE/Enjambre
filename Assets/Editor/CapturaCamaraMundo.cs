using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verificación puntual de la cámara-estilo-Agario (sin Play Mode): calcula
/// los límites reales del mundo, coloca núcleos lejos del centro, fuerza la
/// cámara a la esquina más lejana permitida por el clamp y captura — para
/// confirmar visualmente que el fondo cubre esa esquina y que el HUD sigue
/// bien anclado a pantalla (Screen Space - Camera no debería moverse aunque
/// la cámara sí lo haga en XY). No guarda la escena.
/// </summary>
public static class CapturaCamaraMundo
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Capturar Cámara Mundo (sin Play)")]
    public static void Capturar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var camGo = raices.First(g => g.name == "Camera");
        var cam = camGo.GetComponent<Camera>();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        var panelMenu = raices.First(g => g.name == "Panel Menú");
        var panelFin = raices.First(g => g.name == "Panel Fin");
        var hudGo = raices.First(g => g.name == "HUD");
        panelMenu.SetActive(false);
        panelFin.SetActive(false);
        hudGo.SetActive(true);
        gm.estado = EstadoJuego.Jugando;

        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        Debug.Log($"Mundo: mitadAncho(viewport)={gm.mitadAncho:F2} mitadAlto={gm.mitadAlto:F2} " +
                   $"mitadMundoAncho={gm.mitadMundoAncho:F2} mitadMundoAlto={gm.mitadMundoAlto:F2} " +
                   $"factorMundo={gm.factorMundo:F2}");

        // Tres núcleos repartidos lejos del origen, cerca del borde del
        // mundo grande — para que CentroDeMasa() quede desplazado y se
        // vea contenido en pantalla incluso cuando la cámara está pegada
        // a la esquina más lejana que el clamp le permite.
        Vector2[] posiciones = {
            new Vector2(gm.mitadMundoAncho * 0.62f, gm.mitadMundoAlto * 0.55f),
            new Vector2(gm.mitadMundoAncho * 0.7f, gm.mitadMundoAlto * 0.4f),
            new Vector2(gm.mitadMundoAncho * 0.55f, gm.mitadMundoAlto * 0.68f),
        };
        foreach (var p in posiciones)
        {
            var n = Object.Instantiate(gm.nucleoPrefab, p, Quaternion.identity);
            Invocar(n, "Awake");
            Invocar(n, "OnEnable");
        }

        Vector2 centro = gm.CentroDeMasa();
        Debug.Log($"CentroDeMasa()={centro}");

        var punch = camGo.GetComponent<CameraPunch>();
        Invocar(punch, "Awake");

        float limX = Mathf.Max(0f, gm.mitadMundoAncho - gm.mitadAncho);
        float limY = Mathf.Max(0f, gm.mitadMundoAlto - gm.mitadAlto);
        Vector2 objetivoClamp = new Vector2(Mathf.Clamp(centro.x, -limX, limX), Mathf.Clamp(centro.y, -limY, limY));
        Debug.Log($"Clamp de cámara: limX={limX:F2} limY={limY:F2} objetivoClamp={objetivoClamp}");

        // Salteamos el suavizado (SmoothDamp depende de Time.deltaTime,
        // que fuera de Play Mode no avanza) y posicionamos la cámara
        // directo en destino para poder verificar visualmente el caso
        // límite: la esquina más lejana que el juego real le permitiría.
        var campoZ = typeof(CameraPunch).GetField("zBase", BindingFlags.NonPublic | BindingFlags.Instance);
        float zBase = (float)campoZ.GetValue(punch);
        var campoPosBase = typeof(CameraPunch).GetField("posBase", BindingFlags.NonPublic | BindingFlags.Instance);
        campoPosBase.SetValue(punch, new Vector3(objetivoClamp.x, objetivoClamp.y, zBase));
        camGo.transform.localPosition = new Vector3(objetivoClamp.x, objetivoClamp.y, zBase);

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
        var ruta = Path.Combine(dir, "camara_mundo_editmode.png");
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("CapturaCamaraMundo: guardada en " + ruta);
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
            .Invoke(obj, null);
}
