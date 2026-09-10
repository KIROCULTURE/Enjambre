using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// GameManager.orbesObjetivo ya estaba serializado en la escena en 5 (de un
/// ajuste anterior), así que subir el default en el código a 7 no alcanzaba
/// — Unity siempre prioriza el valor guardado en el .unity por sobre el
/// default de C#. Este script empuja el valor nuevo directo a la escena.
/// </summary>
public static class AplicarBalanceOrbes
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Aplicar Balance de Orbes")]
    public static void Aplicar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gmGo = raices.FirstOrDefault(g => g.name == "GameManager");
        if (gmGo == null) { Debug.LogError("AplicarBalanceOrbes: no encontré GameManager."); return; }

        var gm = gmGo.GetComponent<GameManager>();
        var so = new SerializedObject(gm);
        so.FindProperty("orbesObjetivo").intValue = 7;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(gm);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("AplicarBalanceOrbes: listo (GameManager.orbesObjetivo=7).");
    }
}
