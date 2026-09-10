using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// La UI real de la terminal de escalada de privilegios (Fase 7): una caja
/// oscura pegada arriba de la pantalla (a diferencia de la caja de diálogo
/// de PantallaCutsceneNivel2.cs, que vive abajo — así no compiten por el
/// mismo tercio de pantalla si algún día se solapan) con texto verde
/// fósforo y un botón "Saltar" propio. Independiente del panel de
/// cutscenes: esta secuencia corre con estado==Jugando (no Cutscene, ver
/// GameManager.IniciarEscaladaPrivilegiosNivel2), así que comparte fuente
/// (LiberationSans — sin fuentes de Google, pedido explícito del proyecto)
/// pero ni panel ni botón ni flag de salto con las otras dos cutscenes.
/// </summary>
public static class PantallaTerminalNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    static readonly Color FondoTerminal = new Color(0.02f, 0.03f, 0.025f, 0.94f);
    static readonly Color TextoTerminal = new Color(0.55f, 1f, 0.68f);
    static readonly Color BotonFill = new Color(0.86f, 0.86f, 0.83f);
    static readonly Color BotonTexto = new Color(0.08f, 0.08f, 0.08f);

    static TMP_FontAsset fSistema;

    [MenuItem("Enjambre/Agregar Pantalla de Terminal Nivel 2 (Fase 7)")]
    public static void Agregar()
    {
        fSistema = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (fSistema == null) { Debug.LogError("PantallaTerminalNivel2: falta LiberationSans SDF."); return; }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var camGo = raices.FirstOrDefault(g => g.name == "Camera");
        var gmGo = raices.FirstOrDefault(g => g.name == "GameManager");
        if (camGo == null || gmGo == null) { Debug.LogError("PantallaTerminalNivel2: no encontré Camera/GameManager."); return; }
        var cam = camGo.GetComponent<Camera>();
        var gm = gmGo.GetComponent<GameManager>();

        var raiz = raices.FirstOrDefault(g => g.name == "Panel Terminal Nivel 2");
        if (raiz == null)
        {
            raiz = new GameObject("Panel Terminal Nivel 2");
            var canvas = raiz.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            canvas.overrideSorting = true;
            // Por encima de la barra de vida del boss (12) y de la caja de
            // diálogo de las otras cutscenes (15) — esta secuencia corre
            // EN MEDIO de la pelea, tiene que tapar el HUD del boss.
            canvas.sortingOrder = 20;
            var scaler = raiz.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 600);
            scaler.matchWidthOrHeight = 0.5f;
            raiz.AddComponent<GraphicRaycaster>();
        }
        else
        {
            for (int i = raiz.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(raiz.transform.GetChild(i).gameObject);
        }
        raiz.SetActive(true); // activo mientras se arma, se apaga antes de guardar (ver PantallaInstrucciones.cs)

        // Caja pegada arriba — "desciende" narrativamente (el texto de
        // GameManager empieza a tipear recién tras un beat de espera, que
        // es todo el "tween" que hace falta a este tamaño de caja).
        var caja = CrearImagen("CajaTerminal", raiz.transform, FondoTerminal);
        var cajaRt = caja.rectTransform;
        cajaRt.anchorMin = new Vector2(0.5f, 1f);
        cajaRt.anchorMax = new Vector2(0.5f, 1f);
        cajaRt.pivot = new Vector2(0.5f, 1f);
        cajaRt.anchoredPosition = new Vector2(0, -24);
        cajaRt.sizeDelta = new Vector2(620, 200);
        caja.sprite = ShapeFactory.Pastilla(64, 10, Color.white);
        caja.type = Image.Type.Sliced;

        var texto = CrearTexto("Texto", caja.transform, "", 16, TextoTerminal,
            TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(560, 160));
        var textoRt = texto.rectTransform;
        textoRt.anchorMin = Vector2.zero; textoRt.anchorMax = Vector2.one;
        textoRt.offsetMin = new Vector2(20, 16); textoRt.offsetMax = new Vector2(-20, -16);
        texto.alignment = TextAlignmentOptions.TopLeft;
        texto.enableWordWrapping = true;

        var saltar = CrearBoton("BotonSaltar", raiz.transform, new Vector2(0, 0), new Vector2(110, 34), "Saltar »", 13);
        var saltarRt = saltar.GetComponent<RectTransform>();
        saltarRt.anchorMin = saltarRt.anchorMax = new Vector2(1f, 1f);
        saltarRt.anchoredPosition = new Vector2(-70, -30);
        UnityEventTools.AddPersistentListener(saltar.onClick, gm.SaltarEscaladaPrivilegios);

        gm.panelTerminalNivel2 = raiz;
        gm.textoTerminalNivel2 = texto;
        gm.botonSaltarTerminalNivel2 = saltar.gameObject;
        EditorUtility.SetDirty(gm);

        raiz.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PantallaTerminalNivel2: listo.");
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
