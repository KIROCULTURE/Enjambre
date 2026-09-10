using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// La UI real de las dos cutscenes de Nivel 2 — apertura (punto 3) y
/// cierre (punto 5), que la comparten porque nunca corren a la vez (ver
/// GameManager.CutsceneAperturaNivel2/CutsceneCierreNivel2): una caja de
/// diálogo fija abajo de la pantalla (estilo presentación de boss) más un
/// botón "Saltar" siempre disponible arriba a la derecha, wireado a
/// GameManager.SaltarCutscene (un solo método despacha a la cutscene que
/// esté activa). El boss y el enjambre/Forma Precisa/forma de Nivel 3
/// viven en el mundo, por eso esta caja no tapa el resto de la pantalla —
/// solo el tercio de abajo.
///
/// Reemplaza a PantallaCutsceneNivel2Stub.cs (el panel placeholder
/// estático del punto 1) — reusa el mismo GameObject raíz si existe (para
/// no perder la referencia gm.panelCutsceneNivel2 innecesariamente) pero
/// tira todos sus hijos viejos y arma los nuevos desde cero.
/// </summary>
public static class PantallaCutsceneNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    static readonly Color ColorTexto = new Color(0.92f, 0.92f, 0.9f);
    static readonly Color ColorNombre = new Color(1f, 0.35f, 0.5f);
    static readonly Color CajaDialogoBg = new Color(0.03f, 0.03f, 0.05f, 0.88f);
    static readonly Color BotonFill = new Color(0.86f, 0.86f, 0.83f);
    static readonly Color BotonTexto = new Color(0.08f, 0.08f, 0.08f);

    static TMP_FontAsset fSistema;

    [MenuItem("Enjambre/Agregar Pantalla de Cutscene Nivel 2")]
    public static void Agregar()
    {
        fSistema = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (fSistema == null) { Debug.LogError("PantallaCutsceneNivel2: falta LiberationSans SDF."); return; }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var camGo = raices.FirstOrDefault(g => g.name == "Camera");
        var gmGo = raices.FirstOrDefault(g => g.name == "GameManager");
        if (camGo == null || gmGo == null) { Debug.LogError("PantallaCutsceneNivel2: no encontré Camera/GameManager."); return; }
        var cam = camGo.GetComponent<Camera>();
        var gm = gmGo.GetComponent<GameManager>();

        // Reusa el root viejo (del stub del punto 1) si existe, para no
        // duplicar Canvas — pero tira TODOS sus hijos, son de otro diseño.
        var raiz = raices.FirstOrDefault(g => g.name == "Panel Cutscene Nivel 2 (stub)")
                 ?? raices.FirstOrDefault(g => g.name == "Panel Cutscene Nivel 2");
        if (raiz == null)
        {
            raiz = new GameObject("Panel Cutscene Nivel 2");
            var canvas = raiz.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 15;
            var scaler = raiz.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 600);
            scaler.matchWidthOrHeight = 0.5f;
            raiz.AddComponent<GraphicRaycaster>();
        }
        else
        {
            raiz.name = "Panel Cutscene Nivel 2";
            for (int i = raiz.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(raiz.transform.GetChild(i).gameObject);
        }
        raiz.SetActive(true); // ver nota en PantallaInstrucciones.cs: activo mientras se arma, se apaga antes de guardar

        // Caja de diálogo, pegada abajo — deja libre el resto de la
        // pantalla para el boss y el enjambre/Forma Precisa en el mundo.
        var caja = CrearImagen("CajaDialogo", raiz.transform, CajaDialogoBg);
        var cajaRt = caja.rectTransform;
        cajaRt.anchorMin = new Vector2(0f, 0f);
        cajaRt.anchorMax = new Vector2(1f, 0f);
        cajaRt.pivot = new Vector2(0.5f, 0f);
        cajaRt.anchoredPosition = new Vector2(0, 18);
        cajaRt.sizeDelta = new Vector2(-60, 130);

        // Ancladas a la esquina superior izquierda de la caja (no al
        // centro) — la caja se estira con el ancho de pantalla, así que
        // un offset relativo al centro no quedaría pegado al borde real
        // en aspect ratios distintos.
        var nombre = CrearTexto("Nombre", caja.transform, "EL ADMINISTRADOR", 15, ColorNombre,
            TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(400, 26));
        AnclarArribaIzquierda(nombre.rectTransform, new Vector2(24, -14));
        nombre.fontStyle = FontStyles.Bold;
        nombre.characterSpacing = 2;

        // Empieza oculto — GameManager.MostrarDialogo lo prende/apaga por beat.
        var dialogo = CrearTexto("Dialogo", caja.transform, "", 20, ColorTexto,
            TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(700, 70));
        AnclarArribaIzquierda(dialogo.rectTransform, new Vector2(24, -46));
        dialogo.gameObject.SetActive(false);

        // Botón "Saltar" — esquina superior derecha, siempre disponible
        // mientras dura la cutscene (GameManager la muestra/oculta junto
        // con el panel entero).
        var saltar = CrearBoton("BotonSaltar", raiz.transform, new Vector2(0, 0), new Vector2(110, 34), "Saltar »", 13);
        var saltarRt = saltar.GetComponent<RectTransform>();
        saltarRt.anchorMin = saltarRt.anchorMax = new Vector2(1f, 1f);
        saltarRt.anchoredPosition = new Vector2(-70, -30);
        UnityEventTools.AddPersistentListener(saltar.onClick, gm.SaltarCutscene);

        gm.panelCutsceneNivel2 = raiz;
        gm.textoDialogoCutscene = dialogo;
        gm.botonSaltarCutscene = saltar.gameObject;
        EditorUtility.SetDirty(gm);

        raiz.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PantallaCutsceneNivel2: listo.");
    }

    static void AnclarArribaIzquierda(RectTransform rt, Vector2 offset)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = offset;
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
