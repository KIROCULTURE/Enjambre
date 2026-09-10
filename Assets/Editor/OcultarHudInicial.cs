using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// El juego siempre arranca en estado Menu (Start() llama a
/// MostrarMenu(), que oculta el HUD), pero el HUD quedaba activo en el
/// estado GUARDADO de la escena — igual que "Panel Fin", que sí arranca
/// desactivado en la escena por la misma razón. Esto lo alinea.
/// </summary>
public static class OcultarHudInicial
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Ocultar HUD inicial en la escena")]
    public static void Ocultar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var hud = GameObject.Find("HUD");
        if (hud == null) { Debug.LogError("OcultarHudInicial: no encontré 'HUD'."); return; }
        hud.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("OcultarHudInicial: listo.");
    }
}
