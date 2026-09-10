using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Captura una composición representativa de la pelea real del Nivel 2:
/// boss (con su barra de vida estilo Mega Man X3 forzada a un valor
/// representativo, ver Fase 4) + Forma Precisa + una Flor Giratoria a
/// mitad de floración (balas curvas) + una pared láser YA RESUELTA con el
/// sprite estilizado (ShapeFactory.Haz) — todo armado a mano en vez
/// de tickeando PeleaNivel2()/las corrutinas de cada patrón de verdad (no
/// tickeables en batch mode más allá del primer yield, ver
/// VerificarPeleaNivel2.cs), solo para que la captura muestre los
/// elementos nuevos en pantalla a la vez. No guarda la escena ni toca
/// PlayerPrefs reales más que lo que ya hace BotonJugarNivel2 (protegido
/// igual que el resto de los debug de Nivel 2).
/// </summary>
public static class CapturaPeleaNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static readonly Color ColorProyectil = new Color(2f, 0.4f, 2.3f);

    [MenuItem("Enjambre/Debug/Capturar Pelea Nivel 2")]
    public static void Capturar() => EjecutarCaptura();

    static void EjecutarCaptura()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();
        gm.estado = EstadoJuego.Menu;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        // Salta la cutscene de apertura (flag de SESIÓN, no PlayerPrefs —
        // ver GameManager.cutsceneAperturaVistaSesion) directo a la pelea:
        // boss + Forma Precisa en escena, PeleaActiva=true.
        typeof(GameManager).GetField("cutsceneAperturaVistaSesion", Flags).SetValue(gm, true);

        gm.BotonJugarNivel2();

        // El fundido a negro (Fase 1, punto 4) queda ENCENDIDO tras
        // BotonJugarNivel2() — se apaga solo, animado, en un Update() que
        // acá no se tickea (fuera de Play Mode). Para la captura, sacarlo
        // del medio a mano — mismo criterio que el resto de este archivo
        // con lo que vive detrás de un yield.
        if (gm.imagenFundido != null) gm.imagenFundido.gameObject.SetActive(false);

        var campoFormaPrecisaActiva = typeof(GameManager).GetField("formaPrecisaActiva", Flags);
        var fp = (FormaPrecisa)campoFormaPrecisaActiva.GetValue(gm);
        var boss = Object.FindFirstObjectByType<AdministradorSistema>();

        // Fidelidad de captura únicamente (ver mismo comentario en
        // VerificarCutsceneApertura.cs): Instantiate() en batch mode no
        // dispara Awake()/OnEnable() solos.
        if (fp != null)
        {
            Invocar(fp, "Awake");
            typeof(FormaPrecisa).GetMethod("ActualizarVisual", Flags).Invoke(fp, new object[] { false });
            fp.transform.position = new Vector3(-1.5f, -1.8f, 0f);
        }

        // Flor Giratoria a mitad de floración: en vez de tickear la
        // corrutina real (SecuenciaFlorGiratoria vive detrás de un
        // yield), se recorre la MISMA matemática de curvatura a mano
        // para ubicar cada bala donde ya estaría tras un rato de vuelo —
        // una foto fiel del patrón, no una aproximación.
        Vector2 origenFlor = boss != null ? (Vector2)boss.transform.position : new Vector2(0f, gm.mitadAlto * 0.6f);
        const int brazos = 10;
        const int puntosPorBrazo = 5;
        const float velocidadBase = 1.85f;
        const float curvatura = 55f; // grados/seg — igual que SecuenciaFlorGiratoria
        const float pasoTiempo = 0.11f;
        for (int b = 0; b < brazos; b++)
        {
            float angBase = b * Mathf.PI * 2f / brazos;
            float signo = (b % 2 == 0) ? 1f : -1f;
            Vector2 pos = origenFlor;
            Vector2 vel = new Vector2(Mathf.Cos(angBase), Mathf.Sin(angBase)) * velocidadBase;
            for (int p = 0; p < puntosPorBrazo; p++)
            {
                pos += vel * pasoTiempo;
                vel = RotarGrados(vel, curvatura * signo * pasoTiempo);
                gm.DispararProyectil(pos, vel, ColorProyectil, velocidadAngular: curvatura * signo);
            }
        }

        // Pared láser YA RESUELTA (brillo pleno, sprite Haz estilizado) en
        // el borde derecho — misma función que usan los 4 patrones
        // heredados, forzada al estado post-impacto para que la captura
        // muestre el resultado, no solo el pulso tenue del telegraph
        // (mismo truco que CapturaNucleoYLaser.cs).
        var metodoMuro = typeof(GameManager).GetMethod("CrearMuroLaser", Flags, null,
            new[] { typeof(Rect), typeof(float), typeof(float) }, null);
        metodoMuro.Invoke(gm, new object[] { new Rect(gm.mitadAncho * 0.5f, -gm.mitadAlto * 1.3f, gm.mitadAncho * 0.2f, gm.mitadAlto * 2.6f), 999f, 3f });
        var muro = Object.FindFirstObjectByType<LaserHazard>();
        if (muro != null)
        {
            var srMuro = (SpriteRenderer)typeof(LaserHazard).GetField("sr", Flags).GetValue(muro);
            var mpbMuro = (MaterialPropertyBlock)typeof(LaserHazard).GetField("mpb", Flags).GetValue(muro);
            if (mpbMuro != null) { mpbMuro.SetFloat("_Progreso", 1f); srMuro.SetPropertyBlock(mpbMuro); }
            else srMuro.color = Color.white;
        }

        if (gm.panelMenu != null) gm.panelMenu.SetActive(false);

        // Barra de vida del boss (Fase 4) — el prefijo síncrono de
        // PresentacionVidaBossNivel2 la deja a mitad de un tick escalonado
        // de 0 a 100 (ver GameManager.IniciarPeleaNivel2); para la captura
        // se fuerza a un valor representativo de "en pelea, ya recibió
        // algo de daño" en vez de mostrar ese estado transitorio.
        typeof(GameManager).GetField("animandoPresentacionVidaBossNivel2", Flags).SetValue(gm, false);
        if (gm.barraVidaBossFillNivel2 != null) gm.barraVidaBossFillNivel2.fillAmount = 0.65f;

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

        var dir2 = Path.Combine(Application.dataPath, "..", "..", "capturas");
        Directory.CreateDirectory(dir2);
        var ruta = Path.Combine(dir2, "pelea_nivel2.png");
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("CapturaPeleaNivel2: captura guardada en " + ruta);
    }

    static Vector2 RotarGrados(Vector2 v, float grados)
    {
        float rad = grados * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
