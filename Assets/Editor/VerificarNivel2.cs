using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Verifica (sin Play Mode) los 3 patrones síncronos heredados del viejo
/// Show de Láseres (Barrido/Cruz/Corredor — Abanico usa una corrutina con
/// delay, no verificable acá), que desde el punto 4 son la familia "láser
/// telegrafiado" de la pelea real — siguen generando LaserHazard de
/// verdad como métodos sueltos, así que probarlos acá evita que se rompan
/// silenciosamente. El daño contra la Forma Precisa (agregado en el punto
/// 4) se prueba aparte, en VerificarPeleaNivel2.cs, junto con el resto de
/// la pelea. También captura la pantalla "Niveles" (selector directo
/// desde el menú, ver PantallaNiveles.cs). No guarda la escena.
///
/// El arranque real de la cutscene de apertura (BotonJugarNivel2 ->
/// EntrarCutsceneNivel2) se prueba aparte, en VerificarCutsceneApertura.cs
/// — ese test maneja el flag de sesión en memoria
/// (GameManager.cutsceneAperturaVistaSesion) que decide si se saltea;
/// probarlo acá también hubiera duplicado esa cobertura sin necesidad.
/// </summary>
public static class VerificarNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Nivel 2")]
    public static void Verificar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();

        gm.estado = EstadoJuego.Menu;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        // --- 2. Los 3 patrones síncronos generan LaserHazard ---
        void ProbarPatron(string metodo)
        {
            int antes = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Length;
            typeof(GameManager).GetMethod(metodo, Flags).Invoke(gm, null);
            int despues = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Length;
            Debug.Log($"{metodo}: LaserHazard antes={antes} después={despues}");
            if (despues <= antes)
                Debug.LogError($"FALLÓ: {metodo} no generó ningún LaserHazard.");
            else
                Debug.Log($"OK: {metodo} genera {despues - antes} LaserHazard.");
        }
        ProbarPatron("PatronBarridoSimple");
        ProbarPatron("PatronCruz");
        ProbarPatron("PatronCorredor");

        // --- 3. Los spawners normales de Nivel 1 NO corren en modoNivel2 ---
        // El gate vive en Update() (separa el bloque modoNivel2 — reloj
        // propio de la pelea, ver GameManager.tPelea — del de los
        // spawners de gemas/enemigos de Nivel 1) y no es tickeable en
        // batch mode; confirmarlo alcanza con leer el código.
        Debug.Log("(El gate de Update() que separa modoNivel2 de los spawners normales no se puede tickear en batch mode — revisado a mano en el código.)");

        // Limpieza: los LaserHazard de las pruebas de patrón de arriba
        // (telegraph translúcido) quedarían pintando franjas de color
        // encima del menú en la captura de abajo si no se destruyen antes.
        foreach (var hazard in Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None))
            Object.DestroyImmediate(hazard.gameObject);

        // --- 4. Elegir un nivel desde "Niveles" tiene que cerrar ESE panel ---
        // Bug real reportado: EmpezarPartida()/EntrarCutsceneNivel2() apagaban
        // panelMenu/panelFin/hudRoot pero no panelNiveles — así que quedaba
        // dibujado encima del juego después de elegir cualquiera de los dos
        // niveles desde el selector del menú.
        gm.BotonVolverAlMenu();
        gm.AbrirNiveles();
        gm.BotonJugar();
        Debug.Log($"Tras AbrirNiveles() -> BotonJugar(): panelNiveles activo={gm.panelNiveles.activeSelf} (esperado false)");
        if (gm.panelNiveles.activeSelf)
            Debug.LogError("FALLÓ: BotonJugar() debería cerrar panelNiveles, se quedó abierto encima del juego.");
        else
            Debug.Log("OK: elegir Nivel 1 desde el selector cierra panelNiveles.");

        gm.BotonVolverAlMenu();
        gm.AbrirNiveles();
        gm.BotonJugarNivel2();
        Debug.Log($"Tras AbrirNiveles() -> BotonJugarNivel2(): panelNiveles activo={gm.panelNiveles.activeSelf} (esperado false)");
        if (gm.panelNiveles.activeSelf)
            Debug.LogError("FALLÓ: BotonJugarNivel2() debería cerrar panelNiveles, se quedó abierto encima de la cutscene.");
        else
            Debug.Log("OK: elegir Nivel 2 desde el selector cierra panelNiveles.");

        // --- 5. Captura de la pantalla "Niveles" (selector directo, ver PantallaNiveles.cs) ---
        gm.BotonVolverAlMenu();
        gm.AbrirNiveles();

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
        var ruta = Path.Combine(dir, "pantalla_niveles.png");
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("VerificarNivel2: captura guardada en " + ruta);
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
