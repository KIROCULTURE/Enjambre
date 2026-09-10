using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Actualiza los valores ya serializados en la escena que el código nuevo
/// de cámara-estilo-Agario necesita pero que Unity no vuelve a tomar del
/// default de C# porque ya existen guardados en el .unity (FondoNebulosa.
/// anchoMundo/altoMundo venían de cuando el mundo jugable era del mismo
/// tamaño que la cámara).
/// </summary>
public static class AplicarMundoExpandido
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Aplicar Mundo Expandido (cámara Agario)")]
    public static void Aplicar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();

        var fondoGo = raices.FirstOrDefault(g => g.GetComponent("FondoNebulosa") != null);
        if (fondoGo == null) { Debug.LogError("AplicarMundoExpandido: no encontré FondoNebulosa."); return; }

        var comp = fondoGo.GetComponent("FondoNebulosa");
        var so = new SerializedObject(comp);
        so.FindProperty("anchoMundo").floatValue = 22f;
        so.FindProperty("altoMundo").floatValue = 12f;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(comp);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("AplicarMundoExpandido: listo (FondoNebulosa anchoMundo=22 altoMundo=12).");
    }
}
