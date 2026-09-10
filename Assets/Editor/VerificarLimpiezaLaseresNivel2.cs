using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Verifica (sin Play Mode) un bug real encontrado en la revisión
/// nocturna del 2026-09-10 (Prioridad 3): a diferencia de los proyectiles
/// (pooled, LimpiarProyectiles los devuelve todos), los LaserHazard vivos
/// NO se limpiaban al terminar la pelea de ninguna forma — una pared que
/// ya estaba en telegraph justo cuando sonó la victoria podía seguir
/// resolviendo su impacto DURANTE la cutscene de cierre, restándole una
/// vida "fantasma" a un jugador que ya había ganado. Arreglado con
/// LimpiarLaseresActivosNivel2(), llamado desde PeleaNivel2Victoria,
/// TerminarPeleaNivel2 y LimpiarPartida.
/// </summary>
public static class VerificarLimpiezaLaseresNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Limpieza de Láseres Nivel 2")]
    public static void Verificar()
    {
        PruebaLimpiarLaseresActivosDestruyeTodo();
        PruebaVictoriaNoDejaLaseresVivos();
        PruebaTerminarPeleaNoDejaLaseresVivos();
        PruebaLimpiarPartidaNoDejaLaseresVivos();

        Debug.Log("Verificación de limpieza de láseres de Nivel 2 completa.");
    }

    static GameManager AbrirEscenaFresca()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");
        return gm;
    }

    static void CrearMurosDePrueba(GameManager gm, int cantidad)
    {
        var metodo = typeof(GameManager).GetMethod("CrearMuroLaser", Flags, null, new[] { typeof(Rect) }, null);
        for (int i = 0; i < cantidad; i++)
            metodo.Invoke(gm, new object[] { new Rect(-1f, -1f, 2f, 2f) });
    }

    static void PruebaLimpiarLaseresActivosDestruyeTodo()
    {
        var gm = AbrirEscenaFresca();
        CrearMurosDePrueba(gm, 3);
        int antes = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Length;

        typeof(GameManager).GetMethod("LimpiarLaseresActivosNivel2", Flags).Invoke(gm, null);

        int despues = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Length;
        Debug.Log($"LimpiarLaseresActivosNivel2: LaserHazard antes={antes} (esperado 3), después={despues} (esperado 0)");
        if (antes != 3 || despues != 0)
            Debug.LogError("FALLÓ: LimpiarLaseresActivosNivel2 debería destruir todos los LaserHazard vivos.");
        else
            Debug.Log("OK: LimpiarLaseresActivosNivel2 destruye todas las paredes vivas.");
    }

    static void PruebaVictoriaNoDejaLaseresVivos()
    {
        var gm = AbrirEscenaFresca();
        var boss = Object.Instantiate(gm.administradorPrefab, new Vector3(0f, gm.mitadAlto * 0.6f, 0f), Quaternion.identity);
        typeof(GameManager).GetField("administradorActivo", Flags).SetValue(gm, boss);
        typeof(GameManager).GetProperty("PeleaActiva").GetSetMethod(true).Invoke(gm, new object[] { true });
        CrearMurosDePrueba(gm, 2);

        typeof(GameManager).GetMethod("PeleaNivel2Victoria", Flags).Invoke(gm, null);

        int restantes = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Length;
        Debug.Log($"Tras PeleaNivel2Victoria() con 2 muros vivos: LaserHazard restantes={restantes} (esperado 0 — no debería quedar ninguna pared 'fantasma' resolviendo durante la cutscene de cierre)");
        if (restantes != 0)
            Debug.LogError("FALLÓ: la victoria debería limpiar cualquier pared láser que siguiera viva — si no, puede seguir dañando al jugador durante la cutscene de cierre, ya ganada.");
        else
            Debug.Log("OK: ganar la pelea no deja paredes vivas que puedan seguir golpeando durante la cutscene de cierre.");
    }

    static void PruebaTerminarPeleaNoDejaLaseresVivos()
    {
        var gm = AbrirEscenaFresca();
        typeof(GameManager).GetProperty("PeleaActiva").GetSetMethod(true).Invoke(gm, new object[] { true });
        CrearMurosDePrueba(gm, 2);

        typeof(GameManager).GetMethod("TerminarPeleaNivel2", Flags).Invoke(gm, new object[] { false });

        int restantes = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Length;
        Debug.Log($"Tras TerminarPeleaNivel2(false) con 2 muros vivos: LaserHazard restantes={restantes} (esperado 0)");
        if (restantes != 0)
            Debug.LogError("FALLÓ: terminar la pelea (derrota) debería limpiar cualquier pared láser que siguiera viva.");
        else
            Debug.Log("OK: la derrota tampoco deja paredes láser huérfanas.");
    }

    static void PruebaLimpiarPartidaNoDejaLaseresVivos()
    {
        var gm = AbrirEscenaFresca();
        CrearMurosDePrueba(gm, 2);

        typeof(GameManager).GetMethod("LimpiarPartida", Flags).Invoke(gm, null);

        int restantes = Object.FindObjectsByType<LaserHazard>(FindObjectsSortMode.None).Length;
        Debug.Log($"Tras LimpiarPartida() con 2 muros vivos: LaserHazard restantes={restantes} (esperado 0 — antes quedaban huérfanos si el jugador volvía al menú a mitad de un telegraph)");
        if (restantes != 0)
            Debug.LogError("FALLÓ: volver al menú (Pausa -> Menú) a mitad de un telegraph no debería dejar paredes láser vivas de la partida anterior.");
        else
            Debug.Log("OK: volver al menú limpia cualquier pared láser que hubiera quedado viva.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
