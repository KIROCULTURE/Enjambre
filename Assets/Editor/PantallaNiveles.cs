using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Pantalla "Niveles": selector directo desde el menú principal, sin
/// depender del desbloqueo real de Nivel 2 (MetaProgreso.NivelDosDesbloqueado)
/// — a propósito, es un atajo de testeo mientras se sigue construyendo el
/// juego, no un cambio al progreso real (ver el comentario en
/// GameManager.panelNiveles). Mismo lenguaje visual plano que
/// PantallaInstrucciones.cs.
/// </summary>
public static class PantallaNiveles
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    static readonly Color ColorTexto = new Color(0.90f, 0.90f, 0.88f);
    static readonly Color ColorSub = new Color(0.55f, 0.56f, 0.60f);
    static readonly Color PanelBg = new Color(0.05f, 0.05f, 0.06f, 0.82f);
    static readonly Color FondoOscuro = new Color(0.02f, 0.02f, 0.03f, 0.78f);
    static readonly Color BotonFill = new Color(0.86f, 0.86f, 0.83f);
    static readonly Color BotonTexto = new Color(0.08f, 0.08f, 0.08f);

    static TMP_FontAsset fSistema;

    [MenuItem("Enjambre/Agregar Pantalla de Niveles")]
    public static void Agregar()
    {
        fSistema = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (fSistema == null) { Debug.LogError("PantallaNiveles: falta LiberationSans SDF."); return; }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var camGo = raices.FirstOrDefault(g => g.name == "Camera");
        var gmGo = raices.FirstOrDefault(g => g.name == "GameManager");
        if (camGo == null || gmGo == null) { Debug.LogError("PantallaNiveles: no encontré Camera/GameManager."); return; }
        var cam = camGo.GetComponent<Camera>();
        var gm = gmGo.GetComponent<GameManager>();

        var existente = raices.FirstOrDefault(g => g.name == "Panel Niveles");
        if (existente != null) Object.DestroyImmediate(existente);

        var raiz = new GameObject("Panel Niveles");
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
        raiz.SetActive(true);

        var fondo = CrearImagen("Fondo", raiz.transform, FondoOscuro);
        var fondoRt = fondo.rectTransform;
        fondoRt.anchorMin = Vector2.zero; fondoRt.anchorMax = Vector2.one;
        fondoRt.offsetMin = Vector2.zero; fondoRt.offsetMax = Vector2.zero;

        var caja = CrearImagen("Caja", raiz.transform, PanelBg);
        var cajaRt = caja.rectTransform;
        cajaRt.anchorMin = cajaRt.anchorMax = new Vector2(0.5f, 0.5f);
        cajaRt.anchoredPosition = Vector2.zero;
        cajaRt.sizeDelta = new Vector2(380, 320);

        var titulo = CrearTexto("Titulo", caja.transform, "NIVELES", 26, ColorTexto,
            TextAlignmentOptions.Center, new Vector2(0, 122), new Vector2(300, 42));
        titulo.fontStyle = FontStyles.Bold;
        titulo.characterSpacing = 6;

        var sub = CrearTexto("Sub", caja.transform,
            "Elegí un nivel para jugar directo — no hace falta\nhaber llegado hasta ahí en una partida.",
            11, ColorSub, TextAlignmentOptions.Center, new Vector2(0, 82), new Vector2(340, 40));

        var boton1 = CrearBoton("BotonNivel1", caja.transform, new Vector2(0, 20), new Vector2(220, 46), "Nivel 1", 16);
        UnityEventTools.AddPersistentListener(boton1.onClick, gm.BotonJugar);

        var boton2 = CrearBoton("BotonNivel2", caja.transform, new Vector2(0, -42), new Vector2(220, 46), "Nivel 2", 16);
        UnityEventTools.AddPersistentListener(boton2.onClick, gm.BotonJugarNivel2);

        var volver = CrearBoton("BotonVolver", caja.transform, new Vector2(0, -128), new Vector2(150, 34), "Volver", 15);
        UnityEventTools.AddPersistentListener(volver.onClick, gm.CerrarNiveles);

        gm.panelNiveles = raiz;
        EditorUtility.SetDirty(gm);

        raiz.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PantallaNiveles: listo.");
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
