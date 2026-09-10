using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// GameManager.nucleosDondeEmpiezaEscaladaPorTamano/nucleosParaEscaladaMaximaPorTamano/
/// multiplicadorMaximoPorNucleos/telegraphLaser ya estaban serializados en la
/// escena con los valores de la primera pasada de balance — empuja los
/// valores retunados con la segunda partida real (telemetría) directo a la
/// escena.
/// </summary>
public static class AplicarBalanceDificultad2
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Aplicar Balance de Dificultad 2")]
    public static void Aplicar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gmGo = raices.FirstOrDefault(g => g.name == "GameManager");
        if (gmGo == null) { Debug.LogError("AplicarBalanceDificultad2: no encontré GameManager."); return; }

        var gm = gmGo.GetComponent<GameManager>();
        var so = new SerializedObject(gm);
        so.FindProperty("nucleosDondeEmpiezaEscaladaPorTamano").intValue = 8;
        so.FindProperty("nucleosParaEscaladaMaximaPorTamano").intValue = 55;
        so.FindProperty("multiplicadorMaximoPorNucleos").floatValue = 3f;
        so.FindProperty("telegraphLaser").floatValue = 0.9f;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(gm);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("AplicarBalanceDificultad2: listo (curva de escalada por tamaño retuneada, telegraphLaser=0.9).");
    }
}
