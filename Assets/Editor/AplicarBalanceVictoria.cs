using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// GameManager.tiempoVictoria ya estaba serializado en la escena en 120
/// (el valor original) — cambiar el default en el código a 105f (ver
/// [[project_secuencia_victoria]], retuneado con 21 muertes reales) no
/// alcanza para que la escena lo use; confirmado con una partida real que
/// disparó la Victoria recién cerca de los 120s, no 105s. Empuja el valor
/// retuneado directo a la escena.
/// </summary>
public static class AplicarBalanceVictoria
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Aplicar Balance de Victoria")]
    public static void Aplicar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gmGo = raices.FirstOrDefault(g => g.name == "GameManager");
        if (gmGo == null) { Debug.LogError("AplicarBalanceVictoria: no encontré GameManager."); return; }

        var gm = gmGo.GetComponent<GameManager>();
        var so = new SerializedObject(gm);
        so.FindProperty("tiempoVictoria").floatValue = 105f;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(gm);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("AplicarBalanceVictoria: tiempoVictoria empujado a 105 en la escena.");
    }
}
