using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Empuja GameManager.duracionPeleaNivel2 (y los nuevos límites de
/// estructura de Fase 6 — build-up/fase1/breakdown/rebuild) al valor real
/// del script hacia la escena guardada. Necesario porque duracionPeleaNivel2
/// YA estaba serializado en Game.unity en 104.4 (de antes del rediseño a
/// 128s/Industrial Planet) — cambiar el default en C# por sí solo NO
/// alcanza, la escena gana. Mismo motivo por el que este proyecto usa
/// SerializedObject + ApplyModifiedProperties en vez de tocar el YAML a
/// mano. Reejecutable — no hace nada raro si se corre de nuevo.
/// </summary>
public static class ActualizarDuracionPeleaNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    // A PROPÓSITO valores literales, NO "gm.duracionPeleaNivel2" leído del
    // componente recién cargado — ese ya viene de la escena (104.4,
    // stale), así que leerlo de ahí y reescribirlo sería un no-op
    // circular. Estos números tienen que calzar a mano con los defaults de
    // GameManager.cs (ver el comentario de esa sección) cada vez que
    // cambien.
    const float DuracionPelea = 128f;
    const float DuracionBuildUp = 32f;
    const float DuracionFase1 = 80f;
    const float DuracionBreakdown = 88f;
    const float DuracionRebuild = 112f;

    [MenuItem("Enjambre/Actualizar Duración de la Pelea Nivel 2 (Fase 6)")]
    public static void Actualizar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var gm = scene.GetRootGameObjects().First(g => g.name == "GameManager").GetComponent<GameManager>();

        var so = new SerializedObject(gm);
        so.FindProperty("duracionPeleaNivel2").floatValue = DuracionPelea;
        so.FindProperty("duracionBuildUpNivel2").floatValue = DuracionBuildUp;
        so.FindProperty("duracionFase1Nivel2").floatValue = DuracionFase1;
        so.FindProperty("duracionBreakdownNivel2").floatValue = DuracionBreakdown;
        so.FindProperty("duracionRebuildNivel2").floatValue = DuracionRebuild;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(gm);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"ActualizarDuracionPeleaNivel2: listo — duracionPeleaNivel2={DuracionPelea}, buildUp={DuracionBuildUp}, fase1={DuracionFase1}, breakdown={DuracionBreakdown}, rebuild={DuracionRebuild}.");
    }
}
