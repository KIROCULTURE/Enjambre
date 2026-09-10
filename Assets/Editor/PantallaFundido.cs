using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Fundido a negro entre Nivel 1 y la cutscene de Nivel 2 (Fase 1, punto 4
/// del rediseño grande — "agrega un fundido corto entre niveles... no un
/// corte duro"). Un único Image negro a pantalla completa, en su propio
/// Canvas por ENCIMA de todo lo demás (sortingOrder 30 — el más alto del
/// proyecto hasta ahora era 15, la cutscene de Nivel 2), animado a mano
/// por GameManager.Fundir() (Color.a, sin Animator/DOTween). raycastTarget
/// en false a propósito: aunque quede semi-transparente un instante, nunca
/// tiene que robarle el click al botón "Saltar" que vive debajo.
///
/// Reusa el root viejo si ya existe (mismo criterio que el resto de las
/// pantallas de este proyecto), arranca inactivo — GameManager lo prende
/// él mismo cuando hace falta.
/// </summary>
public static class PantallaFundido
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Agregar Pantalla de Fundido")]
    public static void Agregar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var camGo = raices.FirstOrDefault(g => g.name == "Camera");
        var gmGo = raices.FirstOrDefault(g => g.name == "GameManager");
        if (camGo == null || gmGo == null) { Debug.LogError("PantallaFundido: no encontré Camera/GameManager."); return; }
        var cam = camGo.GetComponent<Camera>();
        var gm = gmGo.GetComponent<GameManager>();

        var raiz = raices.FirstOrDefault(g => g.name == "Panel Fundido");
        if (raiz == null)
        {
            raiz = new GameObject("Panel Fundido");
            var canvas = raiz.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f; // lo más cerca posible de la cámara — por encima de todo
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30;
            var scaler = raiz.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 600);
            scaler.matchWidthOrHeight = 0.5f;
            // Sin GraphicRaycaster a propósito — este panel nunca necesita
            // recibir clicks, y así ni por accidente puede tapar al resto.
        }
        else
        {
            for (int i = raiz.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(raiz.transform.GetChild(i).gameObject);
        }
        raiz.SetActive(true); // activo mientras se arma, se apaga antes de guardar (ver abajo)

        var go = new GameObject("Negro", typeof(RectTransform));
        go.transform.SetParent(raiz.transform, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f); // arranca invisible — GameManager.Fundir() sube el alpha cuando hace falta
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        gm.imagenFundido = img;
        EditorUtility.SetDirty(gm);

        raiz.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PantallaFundido: listo.");
    }
}
