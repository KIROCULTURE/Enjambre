using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Pantalla de mejoras permanentes (accesible desde el Menú y desde Fin):
/// 4 filas (Núcleo Inicial/Crecimiento/Duración Fever/Gemas en pantalla),
/// cada una con nivel actual, costo del próximo nivel y un botón Comprar,
/// más el total de Esencia disponible arriba. Mismo lenguaje visual plano
/// que PantallaPausa.cs/PantallaInstrucciones.cs — helpers duplicados acá
/// por convención de este proyecto (cada script de pantalla es autocontenido).
/// </summary>
public static class PantallaMejoras
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    static readonly Color ColorTexto = new Color(0.90f, 0.90f, 0.88f);
    static readonly Color ColorSub = new Color(0.62f, 0.62f, 0.60f);
    static readonly Color ColorEsencia = new Color(0.55f, 0.85f, 1f);
    static readonly Color PanelBg = new Color(0.05f, 0.05f, 0.06f, 0.82f);
    static readonly Color FondoOscuro = new Color(0.02f, 0.02f, 0.03f, 0.78f);
    static readonly Color BotonFill = new Color(0.86f, 0.86f, 0.83f);
    static readonly Color BotonTexto = new Color(0.08f, 0.08f, 0.08f);

    static TMP_FontAsset fSistema;

    [MenuItem("Enjambre/Agregar Pantalla de Mejoras")]
    public static void Agregar()
    {
        fSistema = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (fSistema == null) { Debug.LogError("PantallaMejoras: falta LiberationSans SDF."); return; }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var camGo = raices.FirstOrDefault(g => g.name == "Camera");
        var gmGo = raices.FirstOrDefault(g => g.name == "GameManager");
        if (camGo == null || gmGo == null) { Debug.LogError("PantallaMejoras: no encontré Camera/GameManager."); return; }
        var cam = camGo.GetComponent<Camera>();
        var gm = gmGo.GetComponent<GameManager>();

        var existente = raices.FirstOrDefault(g => g.name == "Panel Mejoras");
        if (existente != null) Object.DestroyImmediate(existente);

        var raiz = new GameObject("Panel Mejoras");
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
        raiz.SetActive(true); // se arma activa, se apaga recién antes de guardar (ver PantallaPausa.cs)

        var fondo = CrearImagen("Fondo", raiz.transform, FondoOscuro);
        var fondoRt = fondo.rectTransform;
        fondoRt.anchorMin = Vector2.zero; fondoRt.anchorMax = Vector2.one;
        fondoRt.offsetMin = Vector2.zero; fondoRt.offsetMax = Vector2.zero;

        var caja = CrearImagen("Caja", raiz.transform, PanelBg);
        var cajaRt = caja.rectTransform;
        cajaRt.anchorMin = cajaRt.anchorMax = new Vector2(0.5f, 0.5f);
        cajaRt.anchoredPosition = Vector2.zero;
        cajaRt.sizeDelta = new Vector2(480, 460);

        var titulo = CrearTexto("Titulo", caja.transform, "MEJORAS", 28, ColorTexto,
            TextAlignmentOptions.Center, new Vector2(0, 200), new Vector2(400, 42));
        titulo.fontStyle = FontStyles.Bold;
        titulo.characterSpacing = 6;

        var textoEsencia = CrearTexto("TextoEsencia", caja.transform, "Esencia: 0", 18, ColorEsencia,
            TextAlignmentOptions.Center, new Vector2(0, 160), new Vector2(300, 28));

        var controlador = raiz.AddComponent<ControladorMejoras>();
        controlador.textoEsencia = textoEsencia;

        (TMP_Text nivel, TMP_Text costo) FilaMejora(string nombre, string etiqueta, float y, System.Action<Button> wireComprar)
        {
            CrearTexto($"Nombre{nombre}", caja.transform, etiqueta, 16, ColorTexto,
                TextAlignmentOptions.Left, new Vector2(-220, y), new Vector2(180, 28));
            var nivelTxt = CrearTexto($"Nivel{nombre}", caja.transform, "Nivel 0/5", 13, ColorSub,
                TextAlignmentOptions.Left, new Vector2(-30, y), new Vector2(100, 24));
            var costoTxt = CrearTexto($"Costo{nombre}", caja.transform, "0 Esencia", 13, ColorSub,
                TextAlignmentOptions.Left, new Vector2(70, y), new Vector2(90, 24));
            var boton = CrearBoton($"Comprar{nombre}", caja.transform, new Vector2(195, y), new Vector2(80, 30), "Comprar", 12);
            wireComprar(boton);
            return (nivelTxt, costoTxt);
        }

        (controlador.textoNivelNucleo, controlador.textoCostoNucleo) = FilaMejora("Nucleo", "Núcleo Inicial", 100,
            b => UnityEventTools.AddPersistentListener(b.onClick, controlador.ComprarNucleo));
        (controlador.textoNivelCrecimiento, controlador.textoCostoCrecimiento) = FilaMejora("Crecimiento", "Crecimiento", 50,
            b => UnityEventTools.AddPersistentListener(b.onClick, controlador.ComprarCrecimiento));
        (controlador.textoNivelFever, controlador.textoCostoFever) = FilaMejora("Fever", "Duración Fever", 0,
            b => UnityEventTools.AddPersistentListener(b.onClick, controlador.ComprarFever));
        (controlador.textoNivelGemas, controlador.textoCostoGemas) = FilaMejora("Gemas", "Gemas en pantalla", -50,
            b => UnityEventTools.AddPersistentListener(b.onClick, controlador.ComprarGemas));

        var volver = CrearBoton("BotonVolver", caja.transform, new Vector2(0, -195), new Vector2(200, 38), "Volver", 15);
        UnityEventTools.AddPersistentListener(volver.onClick, gm.CerrarMejoras);

        gm.panelMejoras = raiz;
        EditorUtility.SetDirty(gm);

        raiz.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PantallaMejoras: listo.");
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
