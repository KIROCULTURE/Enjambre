using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;
using System.Linq;

/// <summary>
/// Agrega el control de volumen (slider) al menú principal, y el
/// GameObject "VolumenControlador" a la escena. Reejecutable: si el
/// slider ya existe no lo duplica, solo revisa que quede bien conectado.
/// </summary>
public static class AgregarVolumen
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly Color Cian = new Color(0.176f, 0.910f, 1f);
    static readonly Color PanelBg = new Color(0.090f, 0.075f, 0.122f);
    static readonly Color Negro = new Color(0.02f, 0.02f, 0.04f, 1f);
    static readonly Color Sub = new Color(0.490f, 0.541f, 0.6f);

    [MenuItem("Enjambre/Agregar control de Volumen")]
    public static void Agregar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (GameObject.Find("VolumenControlador") == null)
        {
            var go = new GameObject("VolumenControlador");
            go.AddComponent<VolumenControlador>();
        }

        var fMono = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/ShareTechMono SDF.asset");

        var panelMenu = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Panel Menú");
        if (panelMenu == null) { Debug.LogError("AgregarVolumen: no encontré 'Panel Menú'."); return; }
        var caja = BuscarHijo(panelMenu.transform, "Caja");
        if (caja == null) { Debug.LogError("AgregarVolumen: no encontré 'Caja' en Panel Menú. Corré primero 'Enjambre/Aplicar Estilo (Escalada Progresiva)'."); return; }

        if (BuscarHijo(caja, "FilaVolumen") == null)
        {
            ConstruirFilaVolumen(caja, fMono);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("AgregarVolumen: listo.");
    }

    static void ConstruirFilaVolumen(Transform caja, TMP_FontAsset fMono)
    {
        var fila = new GameObject("FilaVolumen", typeof(RectTransform));
        fila.transform.SetParent(caja, false);
        var filaRt = fila.GetComponent<RectTransform>();
        filaRt.anchorMin = filaRt.anchorMax = new Vector2(0.5f, 0.5f);
        filaRt.anchoredPosition = new Vector2(0, -70);
        filaRt.sizeDelta = new Vector2(340, 30);

        var label = new GameObject("Etiqueta", typeof(RectTransform));
        label.transform.SetParent(filaRt, false);
        var labelRt = label.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 0.5f); labelRt.anchorMax = new Vector2(0f, 0.5f);
        labelRt.pivot = new Vector2(0f, 0.5f);
        labelRt.anchoredPosition = Vector2.zero;
        labelRt.sizeDelta = new Vector2(90, 30);
        var tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.text = "volumen";
        tmp.font = fMono;
        tmp.fontSize = 14;
        tmp.color = Sub;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;

        var sliderGo = new GameObject("SliderVolumen", typeof(RectTransform));
        sliderGo.transform.SetParent(filaRt, false);
        var sliderRt = sliderGo.GetComponent<RectTransform>();
        sliderRt.anchorMin = new Vector2(1f, 0.5f); sliderRt.anchorMax = new Vector2(1f, 0.5f);
        sliderRt.pivot = new Vector2(1f, 0.5f);
        sliderRt.anchoredPosition = Vector2.zero;
        sliderRt.sizeDelta = new Vector2(210, 20);
        var slider = sliderGo.AddComponent<Slider>();
        slider.minValue = 0f; slider.maxValue = 1f; slider.wholeNumbers = false;

        // Fondo (track)
        var fondo = new GameObject("Fondo", typeof(RectTransform));
        fondo.transform.SetParent(sliderRt, false);
        var fondoRt = fondo.GetComponent<RectTransform>();
        fondoRt.anchorMin = Vector2.zero; fondoRt.anchorMax = Vector2.one;
        fondoRt.offsetMin = Vector2.zero; fondoRt.offsetMax = Vector2.zero;
        var fondoImg = fondo.AddComponent<Image>();
        fondoImg.color = PanelBg;
        // borde negro fino, dibujado como un segundo Image un pelo más grande detrás
        var borde = new GameObject("Borde", typeof(RectTransform));
        borde.transform.SetParent(sliderRt, false);
        borde.transform.SetAsFirstSibling();
        var bordeRt = borde.GetComponent<RectTransform>();
        bordeRt.anchorMin = Vector2.zero; bordeRt.anchorMax = Vector2.one;
        bordeRt.offsetMin = new Vector2(-2, -2); bordeRt.offsetMax = new Vector2(2, 2);
        borde.AddComponent<Image>().color = Negro;

        // Fill Area / Fill
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderRt, false);
        var fillAreaRt = fillArea.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0f); fillAreaRt.anchorMax = new Vector2(1f, 1f);
        fillAreaRt.offsetMin = new Vector2(3, 3); fillAreaRt.offsetMax = new Vector2(-3, -3);

        var fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(fillAreaRt, false);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = Cian;
        slider.fillRect = fillRt;
        slider.targetGraphic = fillImg;

        // Handle Slide Area / Handle
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderRt, false);
        var handleAreaRt = handleArea.GetComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero; handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(6, 0); handleAreaRt.offsetMax = new Vector2(-6, 0);

        var handle = new GameObject("Handle", typeof(RectTransform));
        handle.transform.SetParent(handleAreaRt, false);
        var handleRt = handle.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(14, 26);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;
        slider.handleRect = handleRt;

        var volumenObj = GameObject.Find("VolumenControlador");
        var controlador = volumenObj != null ? volumenObj.GetComponent<VolumenControlador>() : null;
        float valorInicial = controlador != null ? controlador.VolumenActual : 0.8f;
        slider.SetValueWithoutNotify(valorInicial);
        if (controlador != null)
            UnityEventTools.AddPersistentListener(slider.onValueChanged, controlador.SetVolumen);
    }

    static Transform BuscarHijo(Transform raiz, string nombre) =>
        raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == nombre && t != raiz);
}
