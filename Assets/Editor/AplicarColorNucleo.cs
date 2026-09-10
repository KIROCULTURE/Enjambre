using UnityEngine;
using UnityEditor;

/// <summary>
/// GameManager.colorApagado/colorVivido del prefab Nucleo ya estaban
/// serializados — primero de cuando el sprite era la ilustración
/// importada, después del primer pase a círculo relleno (azul plano,
/// dentro de 0-1). Ahora colorVivido es HDR a propósito (canales > 1,
/// ver AplicarPostProcesado.cs) para que el Bloom lo agarre de verdad al
/// llegar al clímax. Empuja el valor nuevo directo al prefab.
/// </summary>
public static class AplicarColorNucleo
{
    const string RutaPrefab = "Assets/Prefabs/Nucleo.prefab";

    [MenuItem("Enjambre/Aplicar Color de Núcleo")]
    public static void Aplicar()
    {
        var prefabRoot = PrefabUtility.LoadPrefabContents(RutaPrefab);
        var nucleo = prefabRoot.GetComponent<Nucleo>();
        if (nucleo == null)
        {
            Debug.LogError("AplicarColorNucleo: el prefab no tiene componente Nucleo.");
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            return;
        }

        nucleo.colorApagado = new Color(0.25f, 0.32f, 0.5f);
        nucleo.colorVivido = new Color(0.63f, 1.35f, 1.8f);

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, RutaPrefab);
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        Debug.Log("AplicarColorNucleo: listo.");
    }
}
