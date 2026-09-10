using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Captura Sentencia Final (beat 1: Tela Radial amplificada, y beat 2: primera oleada de la Espiral amplificada), forzadas a "ya resuelto" — mismo truco que las otras capturas de Fase 2.</summary>
public static class CapturaSentenciaFinalNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Capturar Sentencia Final Nivel 2")]
    public static void Capturar()
    {
        var (gm, cam) = Preparar();
        Invocar(gm, "PatronSentenciaFinalNivel2"); // beat 0 (sin rayos) corre síncrono, se detiene ahí
        Invocar(gm, "DispararTelaRadialNivel2", gm.rayosSentenciaFinalNivel2); // fuerza el beat 1 a mano, sin esperar el WaitForSeconds real
        ForzarResueltoYGuardar(gm, cam, "sentencia_final_tela_radial.png");

        var (gm2, cam2) = Preparar();
        var metodo = typeof(GameManager).GetMethod("SecuenciaEspiralGiratoriaFase2", Flags);
        gm2.StartCoroutine((System.Collections.IEnumerator)metodo.Invoke(gm2,
            new object[] { gm2.brazosSentenciaFinalNivel2, gm2.oleadasSentenciaFinalNivel2, gm2.pasoAnguloSentenciaFinalNivel2, gm2.esperaEntreOleadasSentenciaFinalNivel2 }));
        ForzarResueltoYGuardar(gm2, cam2, "sentencia_final_espiral.png");
    }

    static (GameManager, Camera) Preparar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        var cam = raices.First(g => g.name == "Camera").GetComponent<Camera>();
        gm.estado = EstadoJuego.Menu;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");

        typeof(GameManager).GetField("cutsceneAperturaVistaSesion", Flags).SetValue(gm, true);
        gm.BotonJugarNivel2();
        if (gm.imagenFundido != null) gm.imagenFundido.gameObject.SetActive(false);
        if (gm.panelMenu != null) gm.panelMenu.SetActive(false);

        var fp = (FormaPrecisa)typeof(GameManager).GetField("formaPrecisaActiva", Flags).GetValue(gm);
        if (fp != null)
        {
            Invocar(fp, "Awake");
            typeof(FormaPrecisa).GetMethod("ActualizarVisual", Flags).Invoke(fp, new object[] { false });
            fp.transform.position = new Vector3(-0.6f, -0.4f, 0f);
        }

        Invocar(gm, "AsegurarSuperAdministradorNivel2");
        if (gm.panelTerminalNivel2 != null) gm.panelTerminalNivel2.SetActive(false);
        if (gm.materialCorrupcionGlitchNivel2 != null)
        {
            var boss = Object.FindFirstObjectByType<AdministradorSistema>();
            if (boss != null && boss.sr != null)
            {
                var mpb = new MaterialPropertyBlock();
                boss.sr.GetPropertyBlock(mpb);
                mpb.SetFloat(Shader.PropertyToID("_Intensidad"), 0.35f);
                boss.sr.SetPropertyBlock(mpb);
            }
        }
        return (gm, cam);
    }

    static void ForzarResueltoYGuardar(GameManager gm, Camera cam, string archivo)
    {
        foreach (var muro in Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None))
        {
            var srMuro = (SpriteRenderer)typeof(LaserHazard).GetField("sr", Flags).GetValue(muro);
            var mpbMuro = (MaterialPropertyBlock)typeof(LaserHazard).GetField("mpb", Flags).GetValue(muro);
            if (mpbMuro != null) { mpbMuro.SetFloat("_Progreso", 1f); srMuro.SetPropertyBlock(mpbMuro); }
            else if (srMuro != null) srMuro.color = Color.white;
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
        var ruta = Path.Combine(dir, archivo);
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Debug.Log($"CapturaSentenciaFinalNivel2: captura guardada en {ruta}");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);

    static void Invocar(object obj, string metodo, object arg) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, new[] { arg });
}
