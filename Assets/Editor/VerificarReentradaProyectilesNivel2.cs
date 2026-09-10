using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Verifica (sin Play Mode) un bug real encontrado jugando el 2026-09-10
/// (ArgumentOutOfRangeException real en el log del Editor, stack trace en
/// GameManager.ActualizarProyectiles -> DevolverProyectil): si el golpe
/// que agota la ÚLTIMA vida del jugador viene de un proyectil,
/// FormaPrecisa.RecibirGolpe dispara AlQuedarSinVidas -> ManejarDerrotaNivel2
/// -> TerminarPeleaNivel2 SÍNCRONAMENTE, DENTRO del mismo loop de
/// ActualizarProyectiles — y TerminarPeleaNivel2 llama LimpiarProyectiles(),
/// que vacía la lista que el loop externo todavía está indexando.
/// </summary>
public static class VerificarReentradaProyectilesNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Reentrada de Proyectiles Nivel 2")]
    public static void Verificar()
    {
        PruebaMorirPorProyectilNoRevientaElLoop();

        Debug.Log("Verificación de reentrada de proyectiles de Nivel 2 completa.");
    }

    static void PruebaMorirPorProyectilNoRevientaElLoop()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");
        typeof(GameManager).GetProperty("PeleaActiva").GetSetMethod(true).Invoke(gm, new object[] { true });

        var fp = Object.Instantiate(gm.formaPrecisaPrefab, Vector2.zero, Quaternion.identity);
        Invocar(fp, "Awake");
        typeof(GameManager).GetField("formaPrecisaActiva", Flags).SetValue(gm, fp);
        typeof(FormaPrecisa).GetField("vidas", Flags).SetValue(fp, 1); // el próximo golpe la mata

        // Mismo enganche que IniciarPeleaNivel2 — sin esto, RecibirGolpe
        // agota la vida pero nadie escucha AlQuedarSinVidas, y el bug real
        // (la reentrada en TerminarPeleaNivel2) nunca se dispararía.
        var metodoDerrota = typeof(GameManager).GetMethod("ManejarDerrotaNivel2", Flags);
        var delegadoDerrota = (System.Action)System.Delegate.CreateDelegate(typeof(System.Action), gm, metodoDerrota);
        fp.AlQuedarSinVidas += delegadoDerrota;

        // Dos proyectiles activos, los dos EXACTAMENTE sobre el jugador — el
        // loop de ActualizarProyectiles va de atrás hacia adelante, así que
        // el segundo (índice más alto) se procesa primero, mata al jugador,
        // dispara la reentrada — y el primero (índice 0) es el que revienta
        // si el fix no está.
        gm.DispararProyectil(Vector2.zero, Vector2.zero, Color.white);
        gm.DispararProyectil(Vector2.zero, Vector2.zero, Color.white);

        bool exploto = false;
        string mensajeError = "";
        try
        {
            Invocar(gm, "ActualizarProyectiles");
        }
        catch (System.Exception e)
        {
            exploto = true;
            mensajeError = e.Message;
        }

        bool peleaTermino = !gm.PeleaActiva;
        Debug.Log($"ActualizarProyectiles() con el golpe que mata al jugador a mitad del loop: excepción lanzada={exploto} (esperado false){(exploto ? $" [{mensajeError}]" : "")}, PeleaActiva tras el golpe={gm.PeleaActiva} (esperado false — la derrota se disparó de verdad)");
        if (exploto)
            Debug.LogError("FALLÓ: ActualizarProyectiles no debería explotar cuando el golpe que mata al jugador dispara TerminarPeleaNivel2 (y LimpiarProyectiles) a mitad del propio loop.");
        else if (peleaTermino == false)
            Debug.LogError("FALLÓ: el golpe debería haber disparado la derrota de verdad (PeleaActiva=false) — si no, el test no está probando el camino real.");
        else
            Debug.Log("OK: morir por un proyectil a mitad del loop no revienta ActualizarProyectiles, aunque LimpiarProyectiles vacíe la lista por debajo.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
