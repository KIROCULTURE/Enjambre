using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Verificación visual puntual del núcleo círculo relleno y la Muralla Láser (sin Play Mode). No guarda la escena.</summary>
public static class CapturaNucleoYLaser
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Capturar Núcleo y Láser (sin Play)")]
    public static void Capturar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        var panelMenu = raices.First(g => g.name == "Panel Menú");
        var hudGo = raices.First(g => g.name == "HUD");
        panelMenu.SetActive(false);
        hudGo.SetActive(true);

        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        // Núcleo ancla en el origen (para que RafagaMurallaLaser centre las
        // paredes ahí) + un par de fragmentos más chicos alrededor, para
        // ver el círculo relleno nuevo a distintos tamaños.
        var ancla = Object.Instantiate(gm.nucleoPrefab, Vector3.zero, Quaternion.identity);
        ancla.radio = 0.55f;
        Invocar(ancla, "Awake");
        Invocar(ancla, "OnEnable");
        Invocar(ancla, "Update");

        var chico1 = Object.Instantiate(gm.nucleoPrefab, new Vector3(0.5f, 0.3f, 0f), Quaternion.identity);
        chico1.radio = 0.22f;
        Invocar(chico1, "Awake");
        Invocar(chico1, "OnEnable");
        Invocar(chico1, "Update");

        var chico2 = Object.Instantiate(gm.nucleoPrefab, new Vector3(-0.4f, -0.35f, 0f), Quaternion.identity);
        chico2.radio = 0.3f;
        Invocar(chico2, "Awake");
        Invocar(chico2, "OnEnable");
        Invocar(chico2, "Update");

        foreach (var n in new[] { ancla, chico1, chico2 })
        {
            var srCampo = typeof(Nucleo).GetField("sr", Flags);
            var sr = (SpriteRenderer)srCampo.GetValue(n);
            Debug.Log($"Núcleo en {n.transform.position}, escala raíz={n.transform.localScale}, " +
                       $"sprite={(sr.sprite != null ? sr.sprite.name : "null")}, color={sr.color}, " +
                       $"sortingOrder={sr.sortingOrder}, enabled={sr.enabled}");
        }

        typeof(GameManager).GetMethod("RafagaMurallaLaser", Flags).Invoke(gm, null);
        // Las paredes recién muestran su brillo pleno al "disparar" (tras
        // el telegraph, que depende de WaitForSeconds); para la captura
        // se fuerza ese estado ahora mismo así se ve el destello, no solo
        // el pulso tenue del telegraph — igual que LaserHazard.Secuencia()
        // al resolver. Con shader (GameManager.materialHazEnergia), eso es
        // _Progreso=1 en el MaterialPropertyBlock; sin shader, es subir el
        // alpha del sprite horneado (ShapeFactory.Haz) a pleno.
        var muros = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None);
        var campoSr = typeof(LaserHazard).GetField("sr", Flags);
        var campoMpb = typeof(LaserHazard).GetField("mpb", Flags);
        foreach (var m in muros)
        {
            var sr = (SpriteRenderer)campoSr.GetValue(m);
            var mpb = (MaterialPropertyBlock)campoMpb.GetValue(m);
            if (mpb != null)
            {
                mpb.SetFloat("_Progreso", 1f);
                sr.SetPropertyBlock(mpb);
            }
            else
            {
                sr.color = Color.white;
            }
        }

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
        File.WriteAllBytes(Path.Combine(dir, "nucleo_laser_editmode.png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log("CapturaNucleoYLaser: listo.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
