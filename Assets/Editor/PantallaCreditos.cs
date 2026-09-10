using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;
using System.Linq;

/// <summary>
/// Reemplaza el pie de "un juego de..." del menú por un botón CRÉDITOS
/// que abre una pantalla dedicada con el equipo y las citas APA 7 de la
/// música usada. Autocontenido (no reutiliza los helpers de
/// Phase2Styler): cada MenuItem de este proyecto corre en su propio
/// proceso de Unity en modo batch, así que el estado estático de otra
/// clase no sobrevive entre una ejecución y otra.
/// </summary>
public static class PantallaCreditos
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    static readonly Color Cian = new Color(0.176f, 0.910f, 1f);
    static readonly Color Rojo = new Color(1f, 0.184f, 0.373f);
    static readonly Color ColorTexto = new Color(0.933f, 0.949f, 0.961f);
    static readonly Color Sub = new Color(0.490f, 0.541f, 0.6f);
    static readonly Color PanelBg = new Color(0.090f, 0.075f, 0.122f);
    static readonly Color Negro = new Color(0.02f, 0.02f, 0.04f, 1f);
    static readonly Color SombraColor = new Color(0f, 0f, 0f, 0.6f);
    static readonly Vector2 OffsetSombra = new Vector2(6, -6);

    static TMP_FontAsset fGlitch, fMono;

    [MenuItem("Enjambre/Agregar pantalla de Créditos")]
    public static void Agregar()
    {
        fGlitch = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/RubikGlitch SDF.asset");
        fMono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/ShareTechMono SDF.asset");
        if (fGlitch == null || fMono == null)
        {
            Debug.LogError("PantallaCreditos: faltan Font Assets (Rubik Glitch / Share Tech Mono).");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var camGo = raices.FirstOrDefault(g => g.name == "Camera");
        var panelMenu = raices.FirstOrDefault(g => g.name == "Panel Menú");
        var gmGo = raices.FirstOrDefault(g => g.name == "GameManager");
        if (camGo == null || panelMenu == null || gmGo == null)
        {
            Debug.LogError("PantallaCreditos: no encontré Camera / Panel Menú / GameManager en la escena.");
            return;
        }
        var cam = camGo.GetComponent<Camera>();
        var gm = gmGo.GetComponent<GameManager>();

        var caja = BuscarHijo(panelMenu.transform, "Caja");
        if (caja == null) { Debug.LogError("PantallaCreditos: no encontré 'Caja' en Panel Menú. Corré primero 'Enjambre/Aplicar Estilo (Escalada Progresiva)'."); return; }

        // El pie viejo con los nombres ya no hace falta: lo reemplaza el botón.
        var footerViejo = BuscarHijo(panelMenu.transform, "Creditos");
        if (footerViejo != null) Object.DestroyImmediate(footerViejo.gameObject);

        AcomodarBotonesMenu(caja, gm);

        if (raices.FirstOrDefault(g => g.name == "Panel Créditos") == null)
        {
            ConstruirPanelCreditos(cam, gm);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("PantallaCreditos: listo.");
    }

    // Corre el botón JUGAR a la izquierda y agrega CRÉDITOS a la derecha,
    // uno al lado del otro, en vez de agrandar la caja del menú.
    static void AcomodarBotonesMenu(Transform caja, GameManager gm)
    {
        var jugar = BuscarHijo(caja, "_PlayButton");
        if (jugar == null) { Debug.LogError("PantallaCreditos: no encontré _PlayButton."); return; }
        var jugarRt = jugar.GetComponent<RectTransform>();
        jugarRt.anchoredPosition = new Vector2(-78, jugarRt.anchoredPosition.y);
        jugarRt.sizeDelta = new Vector2(140, jugarRt.sizeDelta.y);

        var sombraJugar = BuscarHijo(caja, "_PlayButton_Sombra");
        if (sombraJugar != null)
        {
            var srt = sombraJugar.GetComponent<RectTransform>();
            srt.anchoredPosition = jugarRt.anchoredPosition + OffsetSombra;
            srt.sizeDelta = jugarRt.sizeDelta;
        }

        if (BuscarHijo(caja, "BotonCreditos") != null) return; // ya existe, no lo dupliques

        var boton = CrearBoton("BotonCreditos", caja, new Vector2(78, jugarRt.anchoredPosition.y), new Vector2(140, jugarRt.sizeDelta.y), "CRÉDITOS", 15);
        UnityEventTools.AddPersistentListener(boton.onClick, gm.AbrirCreditos);
    }

    static void ConstruirPanelCreditos(Camera cam, GameManager gm)
    {
        var raiz = new GameObject("Panel Créditos");
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
        raiz.SetActive(false); // arranca oculto; GameManager lo prende con AbrirCreditos()

        var fondo = CrearImagen("Fondo", raiz.transform, new Color(0.02f, 0.02f, 0.04f, 0.85f));
        var fondoRt = fondo.rectTransform;
        fondoRt.anchorMin = Vector2.zero; fondoRt.anchorMax = Vector2.one;
        fondoRt.offsetMin = Vector2.zero; fondoRt.offsetMax = Vector2.zero;

        var caja = CrearCaja("Caja", raiz.transform, Vector2.zero, new Vector2(480, 380));

        AgregarTituloGlitch(caja, "CRÉDITOS", new Vector2(0, 150), 34);

        CrearTexto("Equipo", caja, "un juego de Alejandro Gómez · Bastián Allende · Dario Valdebenito",
            fMono, 13, ColorTexto, TextAlignmentOptions.Center, new Vector2(0, 100), new Vector2(420, 40));

        CrearTexto("MusicaHeader", caja, "Música", fMono, 15, Cian,
            TextAlignmentOptions.Center, new Vector2(0, 60), new Vector2(420, 26));

        // Citas en formato APA 7ª edición. "van Meijl" se alfabetiza por el
        // apellido completo (convención holandesa: "van" en minúscula,
        // parte del apellido, no un prefijo aparte).
        CrearTexto("Cita1", caja,
            "van Meijl, K. (s.f.). <i>Space main theme</i> [pista de audio]. SoundCloud.\nhttps://soundcloud.com/kyra-van-meijl/space-main-theme",
            fMono, 11, Sub, TextAlignmentOptions.Center, new Vector2(0, 10), new Vector2(430, 55));

        CrearTexto("Cita2", caja,
            "van Meijl, K. (s.f.). <i>Exploding sun</i> [pista de audio]. SoundCloud.\nhttps://soundcloud.com/kyra-van-meijl/exploding-sun",
            fMono, 11, Sub, TextAlignmentOptions.Center, new Vector2(0, -45), new Vector2(430, 55));

        var boton = CrearBoton("BotonVolver", caja, new Vector2(0, -155), new Vector2(150, 34), "VOLVER", 16);
        UnityEventTools.AddPersistentListener(boton.onClick, gm.CerrarCreditos);

        gm.panelCreditos = raiz;
        EditorUtility.SetDirty(gm);
    }

    static void AgregarTituloGlitch(Transform padre, string texto, Vector2 pos, float tam)
    {
        var capaRojo = CrearTexto("Titulo_R", padre, texto, fGlitch, tam, new Color(Rojo.r, Rojo.g, Rojo.b, 0.85f),
            TextAlignmentOptions.Center, pos + new Vector2(-5, 2), new Vector2(420, 60));
        var capaCian = CrearTexto("Titulo_C", padre, texto, fGlitch, tam, new Color(Cian.r, Cian.g, Cian.b, 0.85f),
            TextAlignmentOptions.Center, pos + new Vector2(5, -2), new Vector2(420, 60));
        var frente = CrearTexto("Titulo", padre, texto, fGlitch, tam, ColorTexto, TextAlignmentOptions.Center, pos, new Vector2(420, 60));
        frente.fontMaterial.EnableKeyword("OUTLINE_ON");
        frente.outlineWidth = 0.15f;
        frente.outlineColor = Negro;
        capaRojo.transform.SetAsLastSibling();
        capaCian.transform.SetAsLastSibling();
        frente.transform.SetAsLastSibling();
    }

    static Button CrearBoton(string nombre, Transform padre, Vector2 pos, Vector2 tam, string texto, float fontSize)
    {
        CrearRectImagen(nombre + "_Sombra", padre, pos + OffsetSombra, tam, Negro);
        var img = CrearImagen(nombre, padre, Cian);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        var boton = img.gameObject.AddComponent<Button>();
        boton.targetGraphic = img;
        var bh = img.gameObject.AddComponent<BotonHundido>();
        bh.offsetHundido = OffsetSombra;

        var label = CrearTexto("Texto", rt, texto, fGlitch, fontSize, Negro, TextAlignmentOptions.Center, Vector2.zero, tam);
        label.raycastTarget = false;
        return boton;
    }

    static Transform CrearCaja(string nombre, Transform padre, Vector2 pos, Vector2 tam)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;

        CrearRectImagen("Sombra", go.transform, OffsetSombra, tam, SombraColor);
        CrearRectImagen("Borde", go.transform, Vector2.zero, tam, Negro);
        CrearRectImagen("Relleno", go.transform, Vector2.zero, tam - new Vector2(6, 6), PanelBg);

        return go.transform;
    }

    static Image CrearRectImagen(string nombre, Transform padre, Vector2 pos, Vector2 tam, Color color)
    {
        var img = CrearImagen(nombre, padre, color);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        return img;
    }

    static Image CrearImagen(string nombre, Transform padre, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static TMP_Text CrearTexto(string nombre, Transform padre, string texto, TMP_FontAsset fuente, float tam, Color color, TextAlignmentOptions align, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.font = fuente;
        tmp.fontSize = tam;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Transform BuscarHijo(Transform raiz, string nombre)
    {
        if (raiz.name == nombre) return raiz;
        return raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == nombre && t != raiz);
    }
}
