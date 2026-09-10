using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Agrega el GameObject "MusicManager" a la escena si todavía no existe.
/// A propósito NO lo destruye/recrea en reejecuciones (a diferencia de
/// otros pasos de estilo): si ya le arrastraste tus clips de música en
/// el Inspector, esto no te los borra.
/// </summary>
public static class AgregarMusica
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Agregar MusicManager a la escena")]
    public static void Agregar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var existente = GameObject.Find("MusicManager");
        if (existente != null)
        {
            Debug.Log("AgregarMusica: ya existe un MusicManager en la escena, no toco nada.");
            return;
        }

        var go = new GameObject("MusicManager");
        go.AddComponent<MusicManager>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("AgregarMusica: MusicManager agregado. Arrastrale tus archivos de audio en 'Clip Menu' / 'Clip Juego' desde el Inspector.");
    }
}
