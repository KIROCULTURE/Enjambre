using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Captura el portal de Victoria de Nivel 1 con el nuevo shader (punto 6
/// — pedido explícito: "el player se mete debajo de un sprite gigante
/// nada más"). SecuenciaVictoria() no es tickeable en batch mode más
/// allá del primer yield (la onda expansiva corre antes de que el portal
/// siquiera se cree), así que esto arma el mismo GameObject a mano —
/// mismo código que esa corrutina, solo para poder verlo en una captura
/// — con un enjambre chico ya parado ENCIMA del portal, para que se note
/// que ahora se lo ve a través de los huecos del vórtice en vez de
/// desaparecer detrás de un disco opaco. No guarda la escena.
/// </summary>
public static class CapturaPortalVortice
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Capturar Portal")]
    public static void Capturar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();
        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        // Enjambre chico ya parado sobre el centro del portal — si el
        // vórtice tapara todo iguel que el disco viejo, esto quedaría
        // invisible detrás.
        Vector2 centro = Vector2.zero;
        for (int i = 0; i < 6; i++)
        {
            float ang = i * Mathf.PI * 2f / 6;
            var pos = centro + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.35f;
            var n = Object.Instantiate(gm.nucleoPrefab, pos, Quaternion.identity);
            n.radio = 0.28f;
            Invocar(n, "Awake");
            Invocar(n, "OnEnable");
            Invocar(n, "Update");
        }

        // El portal en sí — mismo código que GameManager.SecuenciaVictoria,
        // congelado a mitad de apertura (p=0.7) para la captura.
        var portalGo = new GameObject("Portal");
        portalGo.transform.position = centro;
        var srPortal = portalGo.AddComponent<SpriteRenderer>();
        srPortal.sortingOrder = 20;
        if (gm.materialPortal != null)
        {
            srPortal.sprite = ShapeFactory.Cuadrado(8, Color.white);
            srPortal.sharedMaterial = gm.materialPortal;
        }
        else
        {
            srPortal.sprite = ShapeFactory.Ficha(64, Color.white, Color.white, 0);
        }

        float escalaFinal = Mathf.Min(gm.mitadAncho, gm.mitadAlto) * 1.5f;
        const float p = 0.7f;
        float escala = Mathf.Lerp(0f, escalaFinal, p * p);
        portalGo.transform.localScale = new Vector3(escala, escala, 1f);
        srPortal.color = Color.Lerp(new Color(0.3f, 0.1f, 0.6f), new Color(2.5f, 1.8f, 3.2f), p);

        if (gm.panelMenu != null) gm.panelMenu.SetActive(false);

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
        var ruta = Path.Combine(dir, "portal_vortice.png");
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("CapturaPortalVortice: captura guardada en " + ruta);
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
