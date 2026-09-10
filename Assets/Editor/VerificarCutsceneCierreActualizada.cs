using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Verifica (sin Play Mode) la actualización de la cutscene de cierre
/// pedida vía revisión: el Administrador habla desde el poder de Super
/// Administrador (no la desesperación de la Fase 1), y el láser final
/// deja al jugador visible con un tinte de color — "un rayo vistoso
/// brillante que lo transforma en una nueva figura morfológica" (el
/// primer intento, reusar el shader de glitch del punto 9, dejaba al
/// jugador INVISIBLE con el sprite procedural — ver el comentario de
/// AplicarGlitchTransformacionFinalNivel2). Mismo límite de siempre con
/// corrutinas largas en batch mode: solo el prefijo síncrono (hasta el
/// primer diálogo) es tickeable de punta a punta; el resto se prueba
/// invocando los métodos propios por separado.
/// </summary>
public static class VerificarCutsceneCierreActualizada
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Cutscene de Cierre Actualizada")]
    public static void Verificar()
    {
        PruebaPrimerDialogoHablaDesdeElPoder();
        PruebaGlitchTransformacionFinalDejaAlJugadorVisible();

        Debug.Log("Verificación de la cutscene de cierre actualizada completa.");
    }

    static GameManager AbrirEscenaFresca()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        gm.estado = EstadoJuego.Cutscene; // la cutscene de cierre corre en este estado (ver PeleaNivel2Victoria)
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");
        if (gm.panelCutsceneNivel2 == null || gm.textoDialogoCutscene == null)
            Debug.LogError("VerificarCutsceneCierreActualizada: falta panelCutsceneNivel2/textoDialogoCutscene — correr antes 'Enjambre/Agregar Pantalla de Cutscene Nivel 2'.");
        return gm;
    }

    /// <summary>Corre síncrono hasta el primer yield real (dentro de MostrarDialogo) — el texto ya quedó asignado antes de ese punto, así que se puede leer sin tickear más.</summary>
    static void PruebaPrimerDialogoHablaDesdeElPoder()
    {
        var gm = AbrirEscenaFresca();
        var boss = Object.Instantiate(gm.administradorPrefab, Vector2.zero, Quaternion.identity);
        typeof(GameManager).GetField("administradorActivo", Flags).SetValue(gm, boss);

        gm.StartCoroutine((System.Collections.IEnumerator)typeof(GameManager).GetMethod("CutsceneCierreNivel2", Flags).Invoke(gm, null));

        string texto = gm.textoDialogoCutscene.text;
        Debug.Log($"Primer diálogo de la cutscene de cierre: \"{texto}\" (esperado que hable desde el control — 'root'/'no va a fallar', no la vieja amenaza desesperada 'no te dejaré corromper')");
        bool hablaDesdeElPoder = texto.Contains("root") || texto.ToLowerInvariant().Contains("no va a fallar");
        bool esLaViejaLinea = texto.Contains("CORROMPER");
        if (!hablaDesdeElPoder || esLaViejaLinea)
            Debug.LogError("FALLÓ: el primer diálogo de la cutscene de cierre debería reflejar el control del Super Administrador, no la vieja amenaza desesperada de la Fase 1.");
        else
            Debug.Log("OK: el Administrador ya transformado habla desde el control, no la desesperación de antes.");
    }

    /// <summary>
    /// El primer intento (aplicar el shader de glitch al jugador) dejaba
    /// el sprite COMPLETAMENTE INVISIBLE — confirmado con capturas reales,
    /// no una sospecha. Funciona con el Administrador (textura importada)
    /// pero no con el sprite procedural de FormaPrecisa, y sin Play Mode
    /// no hay forma de depurar el HLSL interactivamente. Un jugador
    /// invisible en el momento más importante de la cutscene es peor que
    /// no tener el efecto — se cayó a un tinte de color (mismo Color
    /// visible que ya usa toda la cutscene, ColorFinalNivel3) + partículas,
    /// hasta poder diagnosticar el shader con el usuario mirando el
    /// resultado real.
    /// </summary>
    static void PruebaGlitchTransformacionFinalDejaAlJugadorVisible()
    {
        var gm = AbrirEscenaFresca();
        var fp = Object.Instantiate(gm.formaPrecisaPrefab, Vector2.zero, Quaternion.identity);
        Invocar(fp, "Awake");
        typeof(GameManager).GetField("formaPrecisaActiva", Flags).SetValue(gm, fp);

        typeof(GameManager).GetMethod("AplicarGlitchTransformacionFinalNivel2", Flags).Invoke(gm, null);

        var sr = fp.SpriteCuerpoRenderer;
        bool siguePintable = sr.color.a > 0.01f; // el shader roto dejaba el sprite invisible incluso con alpha "correcto" en el .color — esto solo confirma que NO estamos usando ese camino
        bool noUsaElShaderRoto = sr.sharedMaterial != gm.materialCorrupcionGlitchNivel2;
        Debug.Log($"Tras AplicarGlitchTransformacionFinalNivel2: color del jugador={sr.color} (esperado ColorFinalNivel3, alpha>0), sigue usando su material normal (no el shader que lo volvía invisible)={noUsaElShaderRoto} (esperado true)");
        if (!siguePintable || !noUsaElShaderRoto)
            Debug.LogError("FALLÓ: la transformación final debería dejar al jugador visible con un tinte de color, no reintroducir el shader que lo volvía invisible.");
        else
            Debug.Log("OK: la transformación final se ve (tinte de color + partículas), sin el shader roto que dejaba al jugador invisible.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
