using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Asigna la música de la pelea de Nivel 2 a MusicManager.clipNivel2.
/// Fase 6 del rediseño grande: reemplaza "AI Malware" (104.4s, usada
/// mientras la pelea no tenía estructura de canción real) por "Industrial
/// Planet" de Kyra van Meijl (128s, 117.5 BPM, CC BY 4.0 — cita en
/// Créditos), puesta ahí directo por el usuario ya en .mp3 (no hizo falta
/// convertir, a diferencia de AI Malware.m4a en su momento). AI Malware.mp3
/// queda en Assets/Songs/ sin usar — no se borra por las dudas de que se
/// quiera reusar en otro lado.
/// </summary>
public static class AplicarMusicaNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string ClipPath = "Assets/Songs/Industrial Planet.mp3";

    [MenuItem("Enjambre/Aplicar Música de Nivel 2")]
    public static void Aplicar()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        if (clip == null) { Debug.LogError($"AplicarMusicaNivel2: no encontré el AudioClip en {ClipPath}."); return; }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var mm = raices.SelectMany(r => r.GetComponentsInChildren<MusicManager>(true)).FirstOrDefault();
        if (mm == null) { Debug.LogError("AplicarMusicaNivel2: no encontré MusicManager en la escena."); return; }

        var so = new SerializedObject(mm);
        so.FindProperty("clipNivel2").objectReferenceValue = clip;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(mm);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("AplicarMusicaNivel2: listo — MusicManager.clipNivel2 = " + clip.name);
    }
}
