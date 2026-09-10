using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Simula un instante puntual de una partida (t=3s, t=40s, etc.) y
/// renderiza un frame, sin entrar a Play Mode (evita el cuelgue del
/// indexador de Unity Search de este entorno al entrar a Play). Nunca
/// guarda la escena: todo lo que arma acá (núcleo/enemigos/orbes de
/// prueba, tiempo forzado) es descartable, solo para la captura.
///
/// Nucleo/Enemigo/Orbe/FondoNebulosa leen GameManager.Instancia.Intensidad
/// dentro de su propio Update() para pintarse, pero Update() no corre en
/// Edit Mode salvo que el motor esté en Play — así que después de
/// instanciar todo, se fuerza un Update() de cada uno por reflexión para
/// que el color/posición reflejen el tiempo simulado antes de capturar.
/// </summary>
public static class CapturaIntensidad
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Capturar Intensidad Baja (t=3s, sin Play)")]
    public static void CapturarBaja() => Capturar(3f, "intensidad_baja_3s");

    [MenuItem("Enjambre/Capturar Intensidad Alta (t=40s, sin Play)")]
    public static void CapturarAlta() => Capturar(40f, "intensidad_alta_40s");

    [MenuItem("Enjambre/Capturar Fever x3 (sin Play)")]
    public static void CapturarFever() => Capturar(25f, "fever_x3", nivelFeverSimulado: 3);

    static void Capturar(float tiempoSimulado, string nombreArchivo, int nivelFeverSimulado = 1)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        // Instantiate en Edit Mode no siempre corre Awake() de forma
        // sincrónica; sin esto, GameManager.Instancia (que Nucleo/Enemigo/
        // Orbe leen dentro de su propio Update() para el color) puede
        // seguir null y todo se queda con el blanco por defecto del sprite.
        ForzarMetodo(gm, "Awake");
        var panelMenu = raices.First(g => g.name == "Panel Menú");
        var panelFin = raices.First(g => g.name == "Panel Fin");
        var hudGo = raices.First(g => g.name == "HUD");

        panelMenu.SetActive(false);
        panelFin.SetActive(false);
        hudGo.SetActive(true);
        gm.estado = EstadoJuego.Jugando;

        typeof(GameManager).GetField("tiempo", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, tiempoSimulado);
        typeof(GameManager).GetField("nivelFever", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(gm, nivelFeverSimulado);

        var nucleo = Object.Instantiate(gm.nucleoPrefab, Vector3.zero, Quaternion.identity);
        ForzarMetodo(nucleo, "Awake");
        nucleo.radio = gm.radioInicial;

        var posicionesEnemigos = new[]
        {
            new Vector3(-2.6f, 1.1f, 0), new Vector3(2.3f, -0.8f, 0),
            new Vector3(0.4f, 1.7f, 0), new Vector3(-1.6f, -1.5f, 0),
        };
        foreach (var pos in posicionesEnemigos) ForzarMetodo(Object.Instantiate(gm.enemigoPrefab, pos, Quaternion.identity), "Awake");

        var posicionesOrbes = new[] { new Vector3(1.4f, 0.6f, 0), new Vector3(-0.9f, 1.2f, 0), new Vector3(2.0f, 1.4f, 0) };
        foreach (var pos in posicionesOrbes) ForzarMetodo(Object.Instantiate(gm.orbePrefab, pos, Quaternion.identity), "Awake");

        // OnEnable registra el núcleo en GameManager (para CentroDeMasa());
        // en Play Mode esto es automático al instanciar, en batch a veces no.
        ForzarMetodo(nucleo, "OnEnable");

        var fondoGo = GameObject.Find("FondoNebulosa");
        var fondo = fondoGo != null ? fondoGo.GetComponent<FondoNebulosa>() : null;
        ForzarMetodo(fondo, "Awake");
        ForzarMetodo(fondo, "Update");
        foreach (var n in Object.FindObjectsByType<Nucleo>(FindObjectsSortMode.None)) ForzarMetodo(n, "Update");
        foreach (var e in Object.FindObjectsByType<Enemigo>(FindObjectsSortMode.None)) ForzarMetodo(e, "Update");
        foreach (var o in Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None)) ForzarMetodo(o, "Update");

        hudGo.GetComponentInChildren<HudController>(true)?.Actualizar(tiempoSimulado, 1, 0f, gm.NivelFever);

        // Un Start() (MostrarMenu) tardío/asincrónico de GameManager, o
        // cualquier otro lifecycle diferido, podría haber pisado estos
        // SetActive() de más arriba entre medio — se reafirman recién
        // antes de renderizar, no antes.
        panelMenu.SetActive(false);
        panelFin.SetActive(false);
        hudGo.SetActive(true);

        RenderYGuardar(cam, nombreArchivo);
        Debug.Log($"CapturaIntensidad: {nombreArchivo} (Intensidad={gm.Intensidad:F2})");
    }

    // Instantiate en Edit Mode no siempre corre Awake()/OnEnable() de forma
    // sincrónica (a diferencia de Play Mode), así que estos se fuerzan por
    // reflexión igual que Update(), en vez de asumir que ya corrieron.
    static void ForzarMetodo(Component c, string nombre)
    {
        if (c == null) return;
        var m = c.GetType().GetMethod(nombre, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (m == null) return;
        try { m.Invoke(c, null); }
        catch (TargetInvocationException ex)
        {
            var campos = c.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Select(f => $"{f.Name}={f.GetValue(c) ?? "null"}");
            Debug.LogError($"ForzarMetodo({nombre}) falló en {c.GetType().Name} '{c.name}': {ex.InnerException}\nCampos privados: {string.Join(", ", campos)}");
        }
    }

    static void RenderYGuardar(Camera cam, string nombreArchivo)
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
        var ruta = Path.Combine(dir, nombreArchivo + ".png");
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("CapturaIntensidad: guardada en " + ruta);
    }
}
