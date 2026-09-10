using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;

/// <summary>
/// Bloom + viñeta sutil vía el Volume framework de URP. El proyecto ya
/// tenía HDR habilitado en el URP Asset y el Renderer 2D ya trae un
/// PostProcessData válido — solo faltaba prenderlo en la cámara y armar
/// el Volume/Profile, que es lo que hace este script.
///
/// A propósito NO se toca Tonemapping ni ColorAdjustments: este juego
/// tiene un sistema de paleta progresiva (apagado -> vívido según
/// GameManager.Intensidad, ver feedback_estetica_progresiva) y un
/// color-grading global de post-procesado pisaría ese diseño. El bloom
/// tampoco se logra subiendo Bloom.threshold global a un valor bajo (eso
/// haría brillar CUALQUIER cosa saturada, incluida la UI/texto) — en vez
/// de eso, threshold queda en 1 (el techo LDR real) y son los colores
/// puntuales que queremos que brillen (núcleo, gema, láser) los que se
/// suben a valores HDR (>1) en su propio script, así el bloom es
/// selectivo por diseño, no por casualidad de qué tan saturado quedó cada
/// color.
/// </summary>
public static class AplicarPostProcesado
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string RutaProfile = "Assets/Settings/PerfilPostProcesado.asset";

    [MenuItem("Enjambre/Aplicar Post-Procesado (Bloom)")]
    public static void Aplicar()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var camGo = raices.FirstOrDefault(g => g.name == "Camera");
        if (camGo == null) { Debug.LogError("AplicarPostProcesado: no encontré 'Camera'."); return; }

        var camData = camGo.GetComponent<UniversalAdditionalCameraData>();
        if (camData == null) { Debug.LogError("AplicarPostProcesado: la cámara no tiene UniversalAdditionalCameraData — ¿URP está activo?"); return; }
        camData.renderPostProcessing = true;
        EditorUtility.SetDirty(camGo);

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaProfile);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, RutaProfile);
        }

        // profile.Add<T>() solo agrega el VolumeComponent (otro
        // ScriptableObject) a la lista en memoria — para un asset guardado
        // en disco (no un profile clonado en runtime) hay que registrarlo
        // como sub-asset con AssetDatabase.AddObjectToAsset, si no
        // AssetDatabase.SaveAssets() lo descarta en silencio (mismo tipo
        // de bug que un Sprite generado en memoria asignado a un prefab:
        // ver AplicarNucleoCirculoRelleno.cs/project_rendimiento_nucleo_y_laser).
        bool bloomEraNuevo = !profile.TryGet(out Bloom bloom);
        if (bloomEraNuevo) bloom = profile.Add<Bloom>(true);
        bloom.threshold.overrideState = true; bloom.threshold.value = 1f;
        bloom.intensity.overrideState = true; bloom.intensity.value = 0.85f;
        bloom.scatter.overrideState = true; bloom.scatter.value = 0.65f;
        bloom.clamp.overrideState = true; bloom.clamp.value = 6f;
        if (bloomEraNuevo) AssetDatabase.AddObjectToAsset(bloom, profile);

        bool vignetteEraNuevo = !profile.TryGet(out Vignette vignette);
        if (vignetteEraNuevo) vignette = profile.Add<Vignette>(true);
        vignette.intensity.overrideState = true; vignette.intensity.value = 0.22f;
        vignette.smoothness.overrideState = true; vignette.smoothness.value = 0.5f;
        vignette.color.overrideState = true; vignette.color.value = new Color(0.01f, 0.01f, 0.02f);
        if (vignetteEraNuevo) AssetDatabase.AddObjectToAsset(vignette, profile);

        EditorUtility.SetDirty(bloom);
        EditorUtility.SetDirty(vignette);
        EditorUtility.SetDirty(profile);

        var volumeGo = raices.FirstOrDefault(g => g.name == "PostProcesado");
        if (volumeGo == null) volumeGo = new GameObject("PostProcesado");
        var volume = volumeGo.GetComponent<Volume>();
        if (volume == null) volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volumeGo);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("AplicarPostProcesado: listo — cámara con post-procesado, Volume global con Bloom+Viñeta.");
    }
}
