using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class DiagnosticoPostProcesado
{
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Debug/Diagnosticar Post-Procesado")]
    public static void Diagnosticar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var camGo = raices.First(g => g.name == "Camera");
        var cam = camGo.GetComponent<Camera>();
        var camData = camGo.GetComponent<UniversalAdditionalCameraData>();

        Debug.Log($"URP activo: {UniversalRenderPipeline.asset != null}");
        Debug.Log($"URP supportsHDR: {UniversalRenderPipeline.asset?.supportsHDR}");
        Debug.Log($"cam.allowHDR: {cam.allowHDR}");
        Debug.Log($"camData.renderPostProcessing: {camData.renderPostProcessing}");
        Debug.Log($"camData.renderType: {camData.renderType}");
        Debug.Log($"camData.volumeLayerMask: {camData.volumeLayerMask.value} (binario: {System.Convert.ToString(camData.volumeLayerMask.value, 2)})");
        Debug.Log($"camGo.layer: {camGo.layer}");
        Debug.Log($"URP asset scriptableRenderer: {UniversalRenderPipeline.asset?.GetRenderer(0)?.GetType().Name}");

        var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
        Debug.Log($"Volumes encontrados en la escena: {volumes.Length}");
        foreach (var v in volumes)
        {
            Debug.Log($"  Volume '{v.name}': enabled={v.enabled}, isGlobal={v.isGlobal}, layer={v.gameObject.layer}, " +
                       $"profile={(v.sharedProfile != null ? v.sharedProfile.name : "null")}, " +
                       $"maskIncluyeLayer={(camData.volumeLayerMask.value & (1 << v.gameObject.layer)) != 0}");
            if (v.sharedProfile != null)
            {
                if (v.sharedProfile.TryGet(out Bloom bloom))
                    Debug.Log($"    Bloom: active={bloom.active}, threshold.overrideState={bloom.threshold.overrideState} value={bloom.threshold.value}, " +
                               $"intensity.overrideState={bloom.intensity.overrideState} value={bloom.intensity.value}");
                else
                    Debug.Log("    No tiene Bloom.");
            }
        }
    }
}
