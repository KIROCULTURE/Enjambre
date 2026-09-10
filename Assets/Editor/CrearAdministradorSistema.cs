using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

/// <summary>
/// Arma, de punta a punta, el placeholder del boss del Nivel 2:
/// 1) importa las hojas de sprites de Assets/Sprites/Boss/ (LuizMelo
///    "EVil Wizard 2", CC0 — ver LEEME_PLACEHOLDER.txt ahí) y las corta
///    en sub-sprites (cada hoja es una fila de frames de 250x250px),
/// 2) arma 3 AnimationClip (Idle en loop, LanzarHechizo, Retroceso) con
///    la animación COMPLETA de cada pose (no un solo frame estático como
///    el placeholder anterior),
/// 3) arma el AnimatorController con exactamente esos 3 estados nombrados
///    "idle"/"lanzar_hechizo"/"retroceso" pedidos — para que reemplazar
///    el arte más adelante sea cambiar los clips de cada estado, no
///    tocar código,
/// 4) arma el prefab Assets/Prefabs/AdministradorSistema.prefab y lo
///    asigna a GameManager.administradorPrefab en la escena real.
/// Reejecutable: reconstruye todo desde cero cada vez.
/// </summary>
public static class CrearAdministradorSistema
{
    const string CarpetaSprites = "Assets/Sprites/Boss";
    const string CarpetaAnimaciones = "Assets/Animations";
    const string RutaControlador = CarpetaAnimaciones + "/AdministradorSistema.controller";
    const string RutaPrefab = "Assets/Prefabs/AdministradorSistema.prefab";
    const string ScenePath = "Assets/Scenes/Game.unity";
    const float FrameRate = 10f;
    const int TamanoFrame = 250; // cada hoja del pack es una fila de frames de 250x250px

    [MenuItem("Enjambre/Crear Administrador del Sistema (boss placeholder)")]
    public static void Crear()
    {
        var idle = ImportarHoja("boss_idle.png");
        var ataque = ImportarHoja("boss_ataque.png");
        var golpe = ImportarHoja("boss_golpe.png");
        var caida = ImportarHoja("boss_caida.png");
        if (idle == null || ataque == null || golpe == null || caida == null)
        {
            Debug.LogError("CrearAdministradorSistema: faltó importar alguna hoja — revisar Assets/Sprites/Boss/.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(CarpetaAnimaciones))
            AssetDatabase.CreateFolder("Assets", "Animations");

        var clipIdle = CrearClip("AdministradorSistema_Idle", idle, loop: true);
        var clipLanzar = CrearClip("AdministradorSistema_LanzarHechizo", ataque, loop: false);
        var clipRetroceso = CrearClip("AdministradorSistema_Retroceso", golpe, loop: false);

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaControlador) != null)
            AssetDatabase.DeleteAsset(RutaControlador);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(RutaControlador);
        var maquina = controller.layers[0].stateMachine;

        var estadoIdle = maquina.AddState("idle");
        estadoIdle.motion = clipIdle;
        maquina.defaultState = estadoIdle;

        var estadoLanzar = maquina.AddState("lanzar_hechizo");
        estadoLanzar.motion = clipLanzar;

        var estadoRetroceso = maquina.AddState("retroceso");
        estadoRetroceso.motion = clipRetroceso;

        controller.AddParameter("LanzarHechizo", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Retroceso", AnimatorControllerParameterType.Trigger);

        var aLanzar = maquina.AddAnyStateTransition(estadoLanzar);
        aLanzar.AddCondition(AnimatorConditionMode.If, 0, "LanzarHechizo");
        aLanzar.duration = 0f;
        aLanzar.hasExitTime = false;
        aLanzar.canTransitionToSelf = false;

        var aRetroceso = maquina.AddAnyStateTransition(estadoRetroceso);
        aRetroceso.AddCondition(AnimatorConditionMode.If, 0, "Retroceso");
        aRetroceso.duration = 0f;
        aRetroceso.hasExitTime = false;
        aRetroceso.canTransitionToSelf = false;

        var volverDeLanzar = estadoLanzar.AddTransition(estadoIdle);
        volverDeLanzar.hasExitTime = true; volverDeLanzar.exitTime = 1f; volverDeLanzar.duration = 0f;

        var volverDeRetroceso = estadoRetroceso.AddTransition(estadoIdle);
        volverDeRetroceso.hasExitTime = true; volverDeRetroceso.exitTime = 1f; volverDeRetroceso.duration = 0f;

        // --- Prefab ---
        var raiz = new GameObject("AdministradorSistema");
        // 1.3, no 1.5 como el placeholder anterior: el canvas de 250px de
        // este pack ya trae bastante aire alrededor del personaje (mismo
        // motivo por el que la escala efectiva no escala 1:1 con el
        // tamaño del archivo) — 1.3 deja una altura en pantalla parecida
        // a la del placeholder viejo, "notoriamente más grande" que la
        // Forma Precisa sin comerse la mitad de la arena.
        raiz.transform.localScale = Vector3.one * 1.3f;
        var sr = raiz.AddComponent<SpriteRenderer>();
        sr.sprite = idle[0];
        var animator = raiz.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        var admin = raiz.AddComponent<AdministradorSistema>();
        admin.sr = sr;
        admin.animator = animator;
        admin.spriteDescenso = caida[0];

        if (AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab) != null)
            AssetDatabase.DeleteAsset(RutaPrefab);
        PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
        Object.DestroyImmediate(raiz);

        var prefabAsset = AssetDatabase.LoadAssetAtPath<AdministradorSistema>(RutaPrefab);

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var gm = scene.GetRootGameObjects().First(g => g.name == "GameManager").GetComponent<GameManager>();
        gm.administradorPrefab = prefabAsset;
        EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("CrearAdministradorSistema: listo — " + RutaPrefab + " (asignado a GameManager.administradorPrefab)");
    }

    static AnimationClip CrearClip(string nombre, Sprite[] frames, bool loop)
    {
        var ruta = $"{CarpetaAnimaciones}/{nombre}.anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta) != null) AssetDatabase.DeleteAsset(ruta);

        var clip = new AnimationClip { frameRate = FrameRate };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keyframes = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
            keyframes[i] = new ObjectReferenceKeyframe { time = i / FrameRate, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, ruta);
        return clip;
    }

    /// <summary>Corta una hoja horizontal (fila de frames de TamanoFrame x TamanoFrame) en sub-sprites, en orden izquierda->derecha.</summary>
    static Sprite[] ImportarHoja(string archivo)
    {
        var ruta = $"{CarpetaSprites}/{archivo}";
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (importer == null) { Debug.LogError("CrearAdministradorSistema: no encontré el importer de " + ruta); return null; }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        int cantidadFrames = tex.width / TamanoFrame;
        string nombreBase = Path.GetFileNameWithoutExtension(archivo);
#pragma warning disable CS0618 // TextureImporter.spritesheet/SpriteMetaData: obsoleto a favor de ISpriteEditorDataProvider, pero sigue siendo el camino directo para trocear en batch mode sin abrir el Sprite Editor.
        var meta = new SpriteMetaData[cantidadFrames];
        for (int i = 0; i < cantidadFrames; i++)
        {
            meta[i] = new SpriteMetaData
            {
                name = $"{nombreBase}_{i}",
                rect = new Rect(i * TamanoFrame, 0, TamanoFrame, TamanoFrame),
                pivot = new Vector2(0.5f, 0.5f),
                alignment = (int)SpriteAlignment.Center,
            };
        }
        importer.spritesheet = meta;
#pragma warning restore CS0618
        importer.SaveAndReimport();

        // Los sub-sprites cortados salen nombrados "<nombreBase>_<i>" (ver
        // arriba) — ordenar por nombre alcanza porque acá nunca hay más
        // de 9 frames por hoja (un solo dígito, sin problema de orden
        // lexicográfico tipo "_10" antes que "_2").
        return AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Sprite>().OrderBy(s => s.name).ToArray();
    }
}
