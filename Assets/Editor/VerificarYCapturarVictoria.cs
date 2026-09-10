using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Verifica (sin Play Mode) lo que SÍ se puede comprobar sin tickear
/// frames reales de la secuencia de victoria: que DispararVictoria() deja
/// `estado` en Jugando (a propósito — el jugador tiene que poder seguir
/// moviéndose para entrar al portal) y prende secuenciaVictoriaActiva. La
/// onda escalonada (cada objeto se destruye en su propia corrutina con un
/// delay según distancia) y la animación del portal/espera de entrada NO
/// se pueden probar acá — todas tienen al menos un WaitForSeconds/yield
/// antes de hacer nada, y batch mode sin Play Mode no tickea esos frames;
/// esa parte se valida jugando de verdad. También captura el panel
/// placeholder de la cutscene del Nivel 2 para revisar el layout. No
/// guarda la escena.
/// </summary>
public static class VerificarYCapturarVictoria
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar y Capturar Victoria")]
    public static void Verificar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();

        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        var nucleo = Object.Instantiate(gm.nucleoPrefab, Vector3.zero, Quaternion.identity);
        nucleo.radio = 0.5f;
        Invocar(nucleo, "Awake");
        Invocar(nucleo, "OnEnable");

        var enemigo = Object.Instantiate(gm.enemigoPrefab, new Vector3(2f, 0f, 0f), Quaternion.identity);
        Invocar(enemigo, "Awake");
        Invocar(enemigo, "OnEnable"); // registra en enemigosActivos — sin esto SecuenciaVictoria() nunca lo encuentra

        var orbe = Object.Instantiate(gm.orbePrefab, new Vector3(1f, 1f, 0f), Quaternion.identity);
        Invocar(orbe, "Awake");

        int nucleosAntesDeVictoria = Object.FindObjectsByType<Nucleo>(FindObjectsSortMode.None).Length;

        // Dispara la victoria — StartCoroutine corre síncrono hasta el
        // primer "yield" en el mismo Invoke (mismo comportamiento que ya
        // se confirmó con RafagaRadial en una verificación anterior).
        typeof(GameManager).GetMethod("DispararVictoria", Flags).Invoke(gm, null);

        var campoDisparada = typeof(GameManager).GetField("victoriaDisparada", Flags);
        var campoSecuenciaActiva = typeof(GameManager).GetField("secuenciaVictoriaActiva", Flags);
        bool disparada = (bool)campoDisparada.GetValue(gm);
        bool secuenciaActiva = (bool)campoSecuenciaActiva.GetValue(gm);
        Debug.Log($"Tras DispararVictoria(): estado={gm.estado} (esperado Jugando — el jugador debe poder seguir moviéndose), victoriaDisparada={disparada}, secuenciaVictoriaActiva={secuenciaActiva}");
        if (gm.estado != EstadoJuego.Jugando)
            Debug.LogError($"FALLÓ: esperaba que `estado` se quedara en Jugando (para no bloquear el movimiento mientras se acerca al portal), quedó en {gm.estado}.");
        else if (!disparada || !secuenciaActiva)
            Debug.LogError("FALLÓ: victoriaDisparada/secuenciaVictoriaActiva deberían quedar en true.");
        else
            Debug.Log("OK: DispararVictoria() arma los guards sin bloquear el movimiento del jugador.");

        int nucleosRestantes = Object.FindObjectsByType<Nucleo>(FindObjectsSortMode.None).Length;
        Debug.Log($"núcleos tras DispararVictoria()={nucleosRestantes} (esperado {nucleosAntesDeVictoria}, la onda no debería tocarlos apenas se dispara)");
        if (nucleosRestantes != nucleosAntesDeVictoria)
            Debug.LogError($"FALLÓ: los núcleos cambiaron ({nucleosAntesDeVictoria} -> {nucleosRestantes}) apenas se disparó la victoria, no deberían.");
        else
            Debug.Log("OK: los núcleos quedan intactos.");

        // Limpieza: el núcleo/enemigo/orbe de prueba de arriba nunca se
        // destruyen de verdad (SecuenciaVictoria los tira vía
        // DestruirConRetraso, una corrutina con delay que no tickea en
        // batch mode) — quedarían pintados detrás del panel de la captura
        // de abajo si no se sacan a mano (mismo tipo de artefacto ya visto
        // con los LaserHazard de VerificarNivel2.cs).
        foreach (var n in Object.FindObjectsByType<Nucleo>(FindObjectsSortMode.None)) Object.DestroyImmediate(n.gameObject);
        foreach (var e in Object.FindObjectsByType<Enemigo>(FindObjectsSortMode.None)) Object.DestroyImmediate(e.gameObject);
        foreach (var o in Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None)) Object.DestroyImmediate(o.gameObject);

        // --- Captura del panel placeholder de la cutscene del Nivel 2 ---
        // (la corrutina real llega ahí recién después de la animación del
        // portal, vía EntrarCutsceneNivel2() — para la captura se salta
        // directo a lo que deja armado al final, que es lo único que se
        // puede revisar sin Play Mode).
        var panelMenu = raices.First(g => g.name == "Panel Menú");
        panelMenu.SetActive(false);
        gm.panelCutsceneNivel2.SetActive(true);

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
        var ruta = Path.Combine(dir, "cutscene_nivel2_stub.png");
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("VerificarYCapturarVictoria: captura guardada en " + ruta);
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
