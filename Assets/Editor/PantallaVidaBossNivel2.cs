using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Barra de vida del boss estilo Mega Man X3 (Fase 4 del rediseño grande a
/// boss fight): vertical, arriba a la derecha — el jugador mueve con
/// izquierda/derecha y mira los proyectiles al centro, así que una barra
/// lateral no compite con lo que hay que leer. Fondo oscuro fijo +
/// relleno (Image.Type.Filled, FillMethod.Vertical, FillOrigin.Bottom) que
/// GameManager anima — "llena de 0 a 100 antes de la pelea, después baja
/// con el daño, nunca al revés" (ver GameManager.PresentacionVidaBossNivel2).
///
/// Mismo patrón que el resto de las pantallas de este proyecto: Canvas
/// propio en ScreenSpaceCamera, reusa el root si ya existe. Arranca
/// inactivo — GameManager lo prende él mismo al empezar la pelea.
/// </summary>
public static class PantallaVidaBossNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly Color ColorFondoBarra = new Color(0.05f, 0.05f, 0.06f, 0.85f);
    // Rojo/carmesí — coherente con el resto de la identidad del
    // Administrador en esta pelea (ver FondoNebulosa.colorVividoNivel2).
    static readonly Color ColorRelleno = new Color(0.85f, 0.15f, 0.22f);
    static readonly Color ColorEtiqueta = new Color(0.85f, 0.3f, 0.35f);

    static TMP_FontAsset fSistema;

    [MenuItem("Enjambre/Agregar Pantalla de Vida del Boss (Nivel 2)")]
    public static void Agregar()
    {
        fSistema = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (fSistema == null) { Debug.LogError("PantallaVidaBossNivel2: falta LiberationSans SDF."); return; }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var camGo = raices.FirstOrDefault(g => g.name == "Camera");
        var gmGo = raices.FirstOrDefault(g => g.name == "GameManager");
        if (camGo == null || gmGo == null) { Debug.LogError("PantallaVidaBossNivel2: no encontré Camera/GameManager."); return; }
        var cam = camGo.GetComponent<Camera>();
        var gm = gmGo.GetComponent<GameManager>();

        var raiz = raices.FirstOrDefault(g => g.name == "Panel Vida Boss Nivel2");
        if (raiz == null)
        {
            raiz = new GameObject("Panel Vida Boss Nivel2");
            var canvas = raiz.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 12; // por debajo de la cutscene (15) y el fundido (30), por encima de lo demás
            var scaler = raiz.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 600);
            scaler.matchWidthOrHeight = 0.5f;
        }
        else
        {
            for (int i = raiz.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(raiz.transform.GetChild(i).gameObject);
        }
        raiz.SetActive(true);

        var etiqueta = CrearTexto("Etiqueta", raiz.transform, "ADMINISTRADOR", 12, ColorEtiqueta,
            TextAlignmentOptions.Center, Vector2.zero, new Vector2(140, 20));
        var rtEtiqueta = etiqueta.rectTransform;
        rtEtiqueta.anchorMin = rtEtiqueta.anchorMax = new Vector2(1f, 1f);
        rtEtiqueta.anchoredPosition = new Vector2(-75, -22);
        etiqueta.fontStyle = FontStyles.Bold;
        etiqueta.characterSpacing = 1.5f;

        var fondo = CrearImagen("Fondo", raiz.transform, ColorFondoBarra);
        var rtFondo = fondo.rectTransform;
        rtFondo.anchorMin = rtFondo.anchorMax = new Vector2(1f, 1f);
        rtFondo.pivot = new Vector2(0.5f, 1f);
        rtFondo.anchoredPosition = new Vector2(-75, -40);
        rtFondo.sizeDelta = new Vector2(22, 180);

        var relleno = CrearImagen("Relleno", fondo.transform, ColorRelleno);
        relleno.type = Image.Type.Filled;
        relleno.fillMethod = Image.FillMethod.Vertical;
        relleno.fillOrigin = (int)Image.OriginVertical.Bottom;
        relleno.fillAmount = 0f; // GameManager lo sube — ver PresentacionVidaBossNivel2
        var rtRelleno = relleno.rectTransform;
        rtRelleno.anchorMin = Vector2.zero;
        rtRelleno.anchorMax = Vector2.one;
        rtRelleno.offsetMin = new Vector2(2, 2);
        rtRelleno.offsetMax = new Vector2(-2, -2);

        gm.panelVidaBossNivel2 = raiz;
        gm.barraVidaBossFillNivel2 = relleno;
        EditorUtility.SetDirty(gm);

        raiz.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PantallaVidaBossNivel2: listo.");
    }

    static Image CrearImagen(string nombre, Transform padre, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static TMP_Text CrearTexto(string nombre, Transform padre, string texto, float tam, Color color, TextAlignmentOptions align, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.font = fSistema;
        tmp.fontSize = tam;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }
}
