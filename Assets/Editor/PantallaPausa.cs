using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Pantalla de pausa (Escape durante la partida): Reanudar, Instrucciones,
/// Menú. Mismo lenguaje visual plano que EstiloMinimal.cs/
/// PantallaInstrucciones.cs — panel chico, sin necesidad de acercarse al
/// límite de alto seguro del Canvas (ver PantallaInstrucciones.cs).
/// </summary>
public static class PantallaPausa
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    static readonly Color ColorTexto = new Color(0.90f, 0.90f, 0.88f);
    static readonly Color PanelBg = new Color(0.05f, 0.05f, 0.06f, 0.82f);
    static readonly Color FondoOscuro = new Color(0.02f, 0.02f, 0.03f, 0.78f);
    static readonly Color BotonFill = new Color(0.86f, 0.86f, 0.83f);
    static readonly Color BotonTexto = new Color(0.08f, 0.08f, 0.08f);

    static TMP_FontAsset fSistema;

    [MenuItem("Enjambre/Agregar Pantalla de Pausa")]
    public static void Agregar()
    {
        fSistema = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (fSistema == null) { Debug.LogError("PantallaPausa: falta LiberationSans SDF."); return; }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var camGo = raices.FirstOrDefault(g => g.name == "Camera");
        var gmGo = raices.FirstOrDefault(g => g.name == "GameManager");
        if (camGo == null || gmGo == null) { Debug.LogError("PantallaPausa: no encontré Camera/GameManager."); return; }
        var cam = camGo.GetComponent<Camera>();
        var gm = gmGo.GetComponent<GameManager>();

        var existente = raices.FirstOrDefault(g => g.name == "Panel Pausa");
        if (existente != null) Object.DestroyImmediate(existente);

        var raiz = new GameObject("Panel Pausa");
        var canvas = raiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 5f;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 10;
        var scaler = raiz.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 600);
        scaler.matchWidthOrHeight = 0.5f;
        raiz.AddComponent<GraphicRaycaster>();
        raiz.SetActive(true); // ver nota en PantallaInstrucciones.cs: se arma activa, se apaga recién antes de guardar

        var fondo = CrearImagen("Fondo", raiz.transform, FondoOscuro);
        var fondoRt = fondo.rectTransform;
        fondoRt.anchorMin = Vector2.zero; fondoRt.anchorMax = Vector2.one;
        fondoRt.offsetMin = Vector2.zero; fondoRt.offsetMax = Vector2.zero;

        var caja = CrearImagen("Caja", raiz.transform, PanelBg);
        var cajaRt = caja.rectTransform;
        cajaRt.anchorMin = cajaRt.anchorMax = new Vector2(0.5f, 0.5f);
        cajaRt.anchoredPosition = Vector2.zero;
        cajaRt.sizeDelta = new Vector2(360, 260);

        var titulo = CrearTexto("Titulo", caja.transform, "PAUSA", 28, ColorTexto,
            TextAlignmentOptions.Center, new Vector2(0, 85), new Vector2(300, 42));
        titulo.fontStyle = FontStyles.Bold;
        titulo.characterSpacing = 6;

        var reanudar = CrearBoton("BotonReanudar", caja.transform, new Vector2(0, 15), new Vector2(200, 40), "Reanudar", 16);
        UnityEventTools.AddPersistentListener(reanudar.onClick, gm.ReanudarPartida);

        var instrucciones = CrearBoton("BotonInstrucciones", caja.transform, new Vector2(-80, -45), new Vector2(150, 32), "Instrucciones", 14);
        UnityEventTools.AddPersistentListener(instrucciones.onClick, gm.AbrirInstrucciones);

        var menu = CrearBoton("BotonMenu", caja.transform, new Vector2(80, -45), new Vector2(150, 32), "Menú", 14);
        UnityEventTools.AddPersistentListener(menu.onClick, gm.BotonVolverAlMenu);

        gm.panelPausa = raiz;
        EditorUtility.SetDirty(gm);

        raiz.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PantallaPausa: listo.");
    }

    static Button CrearBoton(string nombre, Transform padre, Vector2 pos, Vector2 tam, string texto, float fontSize)
    {
        var img = CrearImagen(nombre, padre, BotonFill);
        img.sprite = ShapeFactory.Pastilla(64, 20, Color.white);
        img.type = Image.Type.Sliced;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        var boton = img.gameObject.AddComponent<Button>();
        boton.targetGraphic = img;

        var label = CrearTexto("Texto", rt, texto, fontSize, BotonTexto, TextAlignmentOptions.Center, Vector2.zero, tam);
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        return boton;
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
