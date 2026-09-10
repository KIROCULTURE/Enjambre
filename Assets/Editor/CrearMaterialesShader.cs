using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Arma los Material de los shaders del punto 6 (Assets/Shaders/) y los
/// asigna en GameManager.materialHazEnergia/materialPortal en la escena
/// real. Reejecutable: reconstruye los Material desde cero cada vez, así
/// que ajustar Properties por defecto en el .shader alcanza con volver a
/// correr esto para que se reflejen.
/// </summary>
public static class CrearMaterialesShader
{
    const string CarpetaMateriales = "Assets/Materials";
    const string RutaMaterialHaz = CarpetaMateriales + "/HazEnergia.mat";
    const string RutaMaterialPortal = CarpetaMateriales + "/PortalVortice.mat";
    const string RutaMaterialGlitch = CarpetaMateriales + "/CorrupcionGlitch.mat";
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Crear Materiales de Shaders")]
    public static void Crear()
    {
        var shaderHaz = Shader.Find("Enjambre/HazEnergia");
        var shaderPortal = Shader.Find("Enjambre/PortalVortice");
        var shaderGlitch = Shader.Find("Enjambre/CorrupcionGlitch");
        if (shaderHaz == null || shaderPortal == null || shaderGlitch == null)
        {
            Debug.LogError($"CrearMaterialesShader: no encontré los shaders (HazEnergia={shaderHaz != null}, PortalVortice={shaderPortal != null}, CorrupcionGlitch={shaderGlitch != null}) — revisar que Assets/Shaders/ compiló sin errores.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(CarpetaMateriales))
            AssetDatabase.CreateFolder("Assets", "Materials");

        var matHaz = CrearOReemplazar(RutaMaterialHaz, shaderHaz);
        var matPortal = CrearOReemplazar(RutaMaterialPortal, shaderPortal);
        var matGlitch = CrearOReemplazar(RutaMaterialGlitch, shaderGlitch);

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var gm = scene.GetRootGameObjects().First(g => g.name == "GameManager").GetComponent<GameManager>();
        gm.materialHazEnergia = matHaz;
        gm.materialPortal = matPortal;
        gm.materialCorrupcionGlitchNivel2 = matGlitch;
        EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("CrearMaterialesShader: listo — HazEnergia, PortalVortice y CorrupcionGlitch asignados en GameManager.");
    }

    static Material CrearOReemplazar(string ruta, Shader shader)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(ruta) != null)
            AssetDatabase.DeleteAsset(ruta);
        var mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }
}
