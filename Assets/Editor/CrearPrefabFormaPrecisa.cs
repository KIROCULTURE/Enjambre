using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Arma el prefab Assets/Prefabs/FormaPrecisa.prefab desde cero, mismo
/// patrón que Nucleo.prefab (root con CircleCollider2D+script, un hijo
/// "Visual" con SpriteRenderer) más "Hitbox" para el punto real de
/// colisión (visible siempre desde Fase 2, ver FormaPrecisa.cs) y "Aura"
/// para la carga de energía de Fase 3 (ver ActualizarCargaEnergia).
/// También lo asigna a GameManager.formaPrecisaPrefab en la escena real
/// (si no, el campo se queda en None hasta que alguien lo arrastre a mano
/// en el Inspector — este proyecto evita esos pasos manuales).
/// Reejecutable: si el prefab ya existe, lo reconstruye entero — clave
/// cada vez que cambian los valores default de FormaPrecisa (radioVisual,
/// radioHitbox, etc.), porque quedan horneados en el .prefab y no se
/// actualizan solos con un cambio en el script.
/// </summary>
public static class CrearPrefabFormaPrecisa
{
    const string RutaPrefab = "Assets/Prefabs/FormaPrecisa.prefab";
    const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Enjambre/Crear Prefab Forma Precisa")]
    public static void Crear()
    {
        var raiz = new GameObject("FormaPrecisa");
        raiz.tag = "Player";
        var col = raiz.AddComponent<CircleCollider2D>();
        col.isTrigger = true;

        var visual = new GameObject("Visual", typeof(SpriteRenderer));
        visual.transform.SetParent(raiz.transform, false);

        var hitbox = new GameObject("Hitbox", typeof(SpriteRenderer));
        hitbox.transform.SetParent(raiz.transform, false);
        hitbox.SetActive(false);

        // Fase 3 del rediseño de Nivel 2: aura de carga de energía (ver
        // FormaPrecisa.ActualizarCargaEnergia) — arranca activo, a alpha 0
        // vía ActualizarCargaEnergia(0f) en el propio Awake, no oculto por
        // SetActive como Hitbox (esto sí necesita animarse cada frame que
        // cambia la carga, no prenderse/apagarse una sola vez).
        var aura = new GameObject("Aura", typeof(SpriteRenderer));
        aura.transform.SetParent(raiz.transform, false);

        var fp = raiz.AddComponent<FormaPrecisa>();
        fp.visual = visual.transform;
        fp.hitboxVisual = hitbox.transform;
        fp.auraEnergia = aura.transform;
        col.radius = fp.radioHitbox;

        PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
        Object.DestroyImmediate(raiz);

        var prefabAsset = AssetDatabase.LoadAssetAtPath<FormaPrecisa>(RutaPrefab);

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var gm = scene.GetRootGameObjects().First(g => g.name == "GameManager").GetComponent<GameManager>();
        gm.formaPrecisaPrefab = prefabAsset;
        EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("CrearPrefabFormaPrecisa: listo — " + RutaPrefab + " (asignado a GameManager.formaPrecisaPrefab)");
    }
}
