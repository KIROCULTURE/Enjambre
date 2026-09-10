using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// Pantalla "Instrucciones": qué hace cada elemento del juego (enemigos,
/// gemas, orbe de velocidad, racha de combo, Fever). Reusa las formas y
/// colores reales del juego (ShapeFactory + los mismos colores vívidos de
/// Enemigo/Orbe/GameManager) para que cada ícono sea coherente con lo que
/// se ve jugando, en vez de un ícono nuevo inventado para la ocasión.
/// Autocontenida, en el mismo lenguaje visual plano que EstiloMinimal.cs
/// (sin sombras duras, LiberationSans, paneles planos).
/// </summary>
public static class PantallaInstrucciones
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    static readonly Color ColorTexto = new Color(0.90f, 0.90f, 0.88f);
    static readonly Color ColorSub = new Color(0.55f, 0.56f, 0.60f);
    static readonly Color PanelBg = new Color(0.05f, 0.05f, 0.06f, 0.82f);
    static readonly Color FondoOscuro = new Color(0.02f, 0.02f, 0.03f, 0.78f);
    static readonly Color BotonFill = new Color(0.86f, 0.86f, 0.83f);
    static readonly Color BotonTexto = new Color(0.08f, 0.08f, 0.08f);

    // Mismos colores "vívidos" que usan Enemigo/Orbe/GameManager en tiempo
    // real, para que el ícono de cada fila sea literalmente el mismo
    // objeto que aparece jugando, no una reinterpretación.
    static readonly Color ColorEnemigo = new Color(1f, 0.176f, 0.294f);
    static readonly Color ColorGema = new Color(0.224f, 1f, 0.690f);
    static readonly Color ColorVelocidad = new Color(1f, 0.85f, 0.1f);
    static readonly Color ColorCrecimiento = new Color(1f, 0.55f, 0.15f);
    static readonly Color ColorFever = new Color(1f, 0.35f, 0.65f);

    static TMP_FontAsset fSistema;

    [MenuItem("Enjambre/Agregar Pantalla de Instrucciones")]
    public static void Agregar()
    {
        fSistema = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (fSistema == null) { Debug.LogError("PantallaInstrucciones: falta LiberationSans SDF."); return; }
        var spriteMouse = ImportarIconoUI("Assets/Sprites/IconoMouse.png");

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var camGo = raices.FirstOrDefault(g => g.name == "Camera");
        var gmGo = raices.FirstOrDefault(g => g.name == "GameManager");
        if (camGo == null || gmGo == null) { Debug.LogError("PantallaInstrucciones: no encontré Camera/GameManager."); return; }
        var cam = camGo.GetComponent<Camera>();
        var gm = gmGo.GetComponent<GameManager>();

        // Reejecutable: si ya existe, la destruye y la arma de nuevo en
        // vez de acumular una segunda copia.
        var existente = raices.FirstOrDefault(g => g.name == "Panel Instrucciones");
        if (existente != null) Object.DestroyImmediate(existente);

        var raiz = new GameObject("Panel Instrucciones");
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
        // Se arma activa: un Canvas inactivo no corre el layout pass de
        // TMP (el wrapping de texto queda mal calculado) ni resuelve el
        // localScale de los RectTransform recién creados. Se apaga recién
        // antes de guardar.
        raiz.SetActive(true);

        var fondo = CrearImagen("Fondo", raiz.transform, FondoOscuro);
        var fondoRt = fondo.rectTransform;
        fondoRt.anchorMin = Vector2.zero; fondoRt.anchorMax = Vector2.one;
        fondoRt.offsetMin = Vector2.zero; fondoRt.offsetMax = Vector2.zero;

        // OJO con la altura: el Canvas usa referenceResolution 960x600,
        // pero CanvasScaler ("Scale With Screen Size", match 0.5) hace que
        // la altura EFECTIVA disponible en unidades de canvas caiga por
        // debajo de 600 en pantallas anchas (16:9 real da ~570, no 600) —
        // un panel de 640 de alto quedaba cortado arriba y abajo en el
        // juego real aunque se viera bien en una captura de prueba 800x600
        // (una relación de aspecto casual y más generosa). Con esto no se
        // puede confiar en probar solo en 4:3-ish: hay que dejar margen
        // real por debajo de 600, no pegado al límite.
        var caja = CrearImagen("Caja", raiz.transform, PanelBg);
        var cajaRt = caja.rectTransform;
        cajaRt.anchorMin = cajaRt.anchorMax = new Vector2(0.5f, 0.5f);
        cajaRt.anchoredPosition = Vector2.zero;
        cajaRt.sizeDelta = new Vector2(520, 540);

        var titulo = CrearTexto("Titulo", caja.transform, "INSTRUCCIONES", 26, ColorTexto,
            TextAlignmentOptions.Center, new Vector2(0, 232), new Vector2(460, 42));
        titulo.fontStyle = FontStyles.Bold;
        titulo.characterSpacing = 6;

        float y = 165f;
        const float paso = 62f;

        FilaConIcono(caja.transform, spriteMouse, ColorTexto, y, "Moverse",
            "Mueve tu núcleo con el mouse o las flechas.");
        y -= paso;
        FilaConForma(caja.transform, ShapeFactory.Estrella(64, 5), ColorEnemigo, y, "Enemigos",
            "Te golpean y te parten en dos, cada vez más chico.");
        y -= paso;
        FilaConForma(caja.transform, ShapeFactory.Diamante(64), ColorGema, y, "Gemas",
            "Recógelas para crecer.");
        y -= paso;
        FilaConForma(caja.transform, ShapeFactory.Rayo(64), ColorVelocidad, y, "Orbe de velocidad",
            "Te da un impulso de velocidad temporal.");
        y -= paso;
        FilaConSwatch(caja.transform, ColorCrecimiento, y, "Racha de combo",
            "Cada 10 gemas seguidas duplica tu crecimiento por un rato.");
        y -= paso;
        FilaConSwatch(caja.transform, ColorFever, y, "Fever",
            "Cada 20 combos sube de nivel (hasta x5) por 15s: más gemas,\npero también más enemigos. Si no lo renovás, baja solo.");

        var boton = CrearBoton("BotonVolver", caja.transform, new Vector2(0, -228), new Vector2(150, 34), "Volver", 15);
        UnityEventTools.AddPersistentListener(boton.onClick, gm.CerrarInstrucciones);

        gm.panelInstrucciones = raiz;
        EditorUtility.SetDirty(gm);

        raiz.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PantallaInstrucciones: listo.");
    }

    static void FilaConIcono(Transform padre, Sprite sprite, Color color, float y, string titulo, string descripcion)
    {
        if (sprite != null)
        {
            var icono = CrearImagen("Icono_" + titulo, padre, color);
            icono.sprite = sprite;
            icono.preserveAspect = true;
            var rt = icono.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-215, y);
            rt.sizeDelta = new Vector2(30, 30);
        }
        TextoFila(padre, y, titulo, descripcion);
    }

    static void FilaConForma(Transform padre, Sprite sprite, Color color, float y, string titulo, string descripcion)
        => FilaConIcono(padre, sprite, color, y, titulo, descripcion);

    // Para mecánicas que no son un objeto físico en pantalla (racha de
    // combo, Fever): una placa de color lisa con el mismo acento que usa
    // el popup real, en vez de inventar una forma que no existe en el juego.
    static void FilaConSwatch(Transform padre, Color color, float y, string titulo, string descripcion)
    {
        var swatch = CrearImagen("Swatch_" + titulo, padre, color);
        var rt = swatch.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-215, y);
        rt.sizeDelta = new Vector2(26, 26);
        TextoFila(padre, y, titulo, descripcion);
    }

    static void TextoFila(Transform padre, float y, string titulo, string descripcion)
    {
        string colorHex = ColorUtility.ToHtmlStringRGB(ColorSub);
        var tmp = CrearTexto("Texto_" + titulo, padre,
            $"<b>{titulo}</b>\n<color=#{colorHex}>{descripcion}</color>",
            12, ColorTexto, TextAlignmentOptions.MidlineLeft, new Vector2(10, y), new Vector2(380, 48));
        tmp.richText = true;
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

    // Mismo import que EstiloMinimal.ImportarIconoUI, duplicado acá para
    // no depender del orden en que se corran los dos scripts.
    static Sprite ImportarIconoUI(string ruta)
    {
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (importer == null) { Debug.LogError("PantallaInstrucciones: no encontré el importer de " + ruta); return null; }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
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
