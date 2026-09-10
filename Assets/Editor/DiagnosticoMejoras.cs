using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Vuelca la jerarquía de Panel Mejoras (nombre, posición, tamaño, sortingOrder de su Canvas) para diagnosticar el rectángulo suelto reportado.</summary>
public static class DiagnosticoMejoras
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Debug/Diagnosticar Panel Mejoras")]
    public static void Diagnosticar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();

        foreach (var raiz in raices)
        {
            var canvas = raiz.GetComponent<Canvas>();
            if (canvas == null) continue;
            Debug.Log($"=== Root '{raiz.name}' activo={raiz.activeSelf} Canvas.sortingOrder={canvas.sortingOrder} renderMode={canvas.renderMode} ===");
            VolcarHijos(raiz.transform, 1);
        }
    }

    static void VolcarHijos(Transform t, int profundidad)
    {
        foreach (Transform hijo in t)
        {
            var rt = hijo.GetComponent<RectTransform>();
            var img = hijo.GetComponent<Image>();
            string info = rt != null
                ? $"anchoredPos={rt.anchoredPosition} sizeDelta={rt.sizeDelta} anchorMin={rt.anchorMin} anchorMax={rt.anchorMax} pivot={rt.pivot}"
                : "(sin RectTransform)";
            string imgInfo = img != null ? $" | Image color={img.color} sprite={(img.sprite != null ? img.sprite.name : "null")}" : "";
            Debug.Log($"{new string(' ', profundidad * 2)}{hijo.name} activo={hijo.gameObject.activeSelf} {info}{imgInfo}");
            VolcarHijos(hijo, profundidad + 1);
        }
    }
}
