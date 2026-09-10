using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// GameManager.radioReposicionOrbe y radioExplosion ya estaban serializados
/// en la escena (2.2 y 1.8, de la pasada anterior) — igual que
/// orbesObjetivo antes, subir el default en el código no alcanza. Empuja
/// los valores nuevos directo a la escena.
/// </summary>
public static class AplicarBalanceExplosion
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Aplicar Balance de Explosión")]
    public static void Aplicar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gmGo = raices.FirstOrDefault(g => g.name == "GameManager");
        if (gmGo == null) { Debug.LogError("AplicarBalanceExplosion: no encontré GameManager."); return; }

        var gm = gmGo.GetComponent<GameManager>();
        var so = new SerializedObject(gm);
        so.FindProperty("radioReposicionOrbe").floatValue = 3f;
        so.FindProperty("radioReposicionMinimo").floatValue = 1f;
        so.FindProperty("radioExplosion").floatValue = 3.2f;
        so.FindProperty("radioExplosionMinimo").floatValue = 1f;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(gm);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("AplicarBalanceExplosion: listo (radioReposicionOrbe=3, radioReposicionMinimo=1, radioExplosion=3.2, radioExplosionMinimo=1).");
    }
}
