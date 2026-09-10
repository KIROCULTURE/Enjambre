using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Verifica (sin Play Mode) el combo rápido -> pulso potente de Nivel 2
/// (Fase 5 del rediseño grande a boss fight) y los dos frenos contra el
/// loop "combo rápido -> pulso potente -> muchos orbes del boss -> combo
/// rápido otra vez" que el pedido explícito advertía. Todo síncrono:
/// ProcesarPickupOrbeNivel2/DescargarPulsoNivel2/GenerarOrbesDelBossNivel2
/// no son corrutinas.
///
/// tPelea se fuerza a mano vía reflexión entre pickups (en vez de tickear
/// Update real) para simular "rápido" vs "lento" — mismo criterio que el
/// resto del proyecto usa para Time.deltaTime, no controlable en batch mode.
/// </summary>
public static class VerificarComboPulsoPotenteNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Combo y Pulso Potente Nivel 2")]
    public static void Verificar()
    {
        var gmChequeo = AbrirEscenaFresca();
        if (gmChequeo.administradorPrefab == null || gmChequeo.formaPrecisaPrefab == null)
        {
            Debug.LogError("FALLÓ: falta administradorPrefab o formaPrecisaPrefab en GameManager — correr antes 'Crear Administrador del Sistema' y 'Crear Prefab Forma Precisa'.");
            return;
        }

        PruebaCargaRapidaEsPotenteYSueltaOrbes();
        PruebaCargaLentaEsNormal();
        PruebaCooldownFuerzaNormalAunqueSeaRapido();
        PruebaCooldownDecaeYVuelveAHabilitarPotente();
        PruebaOrbesDelBossValenMenos();

        Debug.Log("Verificación de combo/pulso potente de Nivel 2 completa.");
    }

    static GameManager AbrirEscenaFresca()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        gm.estado = EstadoJuego.Menu;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");
        return gm;
    }

    static FormaPrecisa CrearFormaPrecisaDePrueba(GameManager gm, Vector2 pos)
    {
        var fp = Object.Instantiate(gm.formaPrecisaPrefab, pos, Quaternion.identity);
        Invocar(fp, "Awake");
        typeof(GameManager).GetField("formaPrecisaActiva", Flags).SetValue(gm, fp);
        return fp;
    }

    static AdministradorSistema CrearBossDePrueba(GameManager gm, Vector2 pos)
    {
        var boss = Object.Instantiate(gm.administradorPrefab, pos, Quaternion.identity);
        typeof(GameManager).GetField("administradorActivo", Flags).SetValue(gm, boss);
        return boss;
    }

    static void ForzarTPelea(GameManager gm, float valor) => typeof(GameManager).GetField("tPelea", Flags).SetValue(gm, valor);
    static float LeerVida(GameManager gm) => (float)typeof(GameManager).GetField("vidaBossNivel2", Flags).GetValue(gm);
    static void EscribirVida(GameManager gm, float v) => typeof(GameManager).GetField("vidaBossNivel2", Flags).SetValue(gm, v);
    static float LeerCooldown(GameManager gm) => (float)typeof(GameManager).GetField("tCooldownPulsoPotenteRestanteNivel2", Flags).GetValue(gm);
    static void EscribirCooldown(GameManager gm, float v) => typeof(GameManager).GetField("tCooldownPulsoPotenteRestanteNivel2", Flags).SetValue(gm, v);

    static void RecolectarOrbeDirecto(GameManager gm, bool origenBoss)
    {
        var o = Object.Instantiate(gm.orbePrefab, Vector3.zero, Quaternion.identity);
        Invocar(o, "Awake");
        o.origenBoss = origenBoss;
        typeof(GameManager).GetMethod("ProcesarPickupOrbeNivel2", Flags).Invoke(gm, new object[] { o });
    }

    /// <summary>Llena la barra entera con orbes normales, todos en el mismo instante de tPelea (combo "instantáneo" — el caso más rápido posible).</summary>
    static void LlenarBarraRapido(GameManager gm)
    {
        int orbes = Mathf.CeilToInt(gm.energiaMaxNivel2 / gm.cargaPorOrbeNivel2);
        for (int i = 0; i < orbes; i++) RecolectarOrbeDirecto(gm, false);
    }

    static void PruebaCargaRapidaEsPotenteYSueltaOrbes()
    {
        var gm = AbrirEscenaFresca();
        var fp = CrearFormaPrecisaDePrueba(gm, Vector2.zero);
        var boss = CrearBossDePrueba(gm, new Vector3(0f, gm.mitadAlto * 0.6f, 0f));
        EscribirVida(gm, gm.vidaBossMaxNivel2);
        ForzarTPelea(gm, 5f); // todos los orbes en el mismo instante -> tiempoCarga=0

        LlenarBarraRapido(gm);

        float vidaEsperada = gm.vidaBossMaxNivel2 - gm.danoPulsoPotenteNivel2;
        int orbesBoss = Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None).Count(o => o.origenBoss && !o.recolectado);
        float cooldown = LeerCooldown(gm);
        Debug.Log($"Combo instantáneo: vidaBossNivel2={LeerVida(gm)} (esperado {vidaEsperada} — daño POTENTE), orbes sueltos por el boss={orbesBoss} (esperado {gm.orbesPorPulsoPotenteNivel2}), cooldown activado={cooldown} (esperado >0)");
        if (Mathf.Abs(LeerVida(gm) - vidaEsperada) > 0.001f || orbesBoss != gm.orbesPorPulsoPotenteNivel2 || cooldown <= 0f)
            Debug.LogError("FALLÓ: cargar la barra rápido debería disparar el pulso POTENTE, soltar los orbes del boss y activar el cooldown.");
        else
            Debug.Log("OK: combo rápido -> pulso potente, con recompensa (orbes del boss) y cooldown activado.");
    }

    static void PruebaCargaLentaEsNormal()
    {
        var gm = AbrirEscenaFresca();
        var fp = CrearFormaPrecisaDePrueba(gm, Vector2.zero);
        var boss = CrearBossDePrueba(gm, new Vector3(0f, gm.mitadAlto * 0.6f, 0f));
        EscribirVida(gm, gm.vidaBossMaxNivel2);

        int orbes = Mathf.CeilToInt(gm.energiaMaxNivel2 / gm.cargaPorOrbeNivel2);
        for (int i = 0; i < orbes; i++)
        {
            // Cada orbe llega bien espaciado en el tiempo — un combo lento
            // de verdad, no uno instantáneo.
            ForzarTPelea(gm, i * (gm.umbralComboRapidoNivel2 + 1f));
            RecolectarOrbeDirecto(gm, false);
        }

        float vidaEsperada = gm.vidaBossMaxNivel2 - gm.danoPulsoNivel2;
        Debug.Log($"Combo lento (bien espaciado): vidaBossNivel2={LeerVida(gm)} (esperado {vidaEsperada} — daño NORMAL, no potente)");
        if (Mathf.Abs(LeerVida(gm) - vidaEsperada) > 0.001f)
            Debug.LogError("FALLÓ: un combo lento (por encima de umbralComboRapidoNivel2) debería resolver como pulso normal.");
        else
            Debug.Log("OK: combo lento -> pulso normal, sin recompensa extra.");
    }

    static void PruebaCooldownFuerzaNormalAunqueSeaRapido()
    {
        var gm = AbrirEscenaFresca();
        var fp = CrearFormaPrecisaDePrueba(gm, Vector2.zero);
        var boss = CrearBossDePrueba(gm, new Vector3(0f, gm.mitadAlto * 0.6f, 0f));
        EscribirVida(gm, gm.vidaBossMaxNivel2);
        // Simula que un pulso potente YA se disparó hace un instante — el
        // cooldown sigue corriendo.
        EscribirCooldown(gm, gm.cooldownPulsoPotenteNivel2);
        ForzarTPelea(gm, 5f);

        LlenarBarraRapido(gm); // tan rápido como el primer test, pero esta vez con cooldown activo

        float vidaEsperada = gm.vidaBossMaxNivel2 - gm.danoPulsoNivel2;
        Debug.Log($"Combo rápido CON cooldown activo: vidaBossNivel2={LeerVida(gm)} (esperado {vidaEsperada} — forzado a NORMAL pese a ser rápido)");
        if (Mathf.Abs(LeerVida(gm) - vidaEsperada) > 0.001f)
            Debug.LogError("FALLÓ: con el cooldown del pulso potente activo, incluso un combo instantáneo debería resolver como normal — este es el freno #1 contra el loop.");
        else
            Debug.Log("OK: el cooldown frena el pulso potente aunque el combo sea perfecto — no hay loop de potente en potente.");
    }

    static void PruebaCooldownDecaeYVuelveAHabilitarPotente()
    {
        var gm = AbrirEscenaFresca();
        var fp = CrearFormaPrecisaDePrueba(gm, Vector2.zero);
        var boss = CrearBossDePrueba(gm, new Vector3(0f, gm.mitadAlto * 0.6f, 0f));
        EscribirCooldown(gm, 3f);

        var metodo = typeof(GameManager).GetMethod("ActualizarOrbesNivel2", Flags);
        metodo.Invoke(gm, new object[] { 5f }); // dt > cooldown restante

        float cooldown = LeerCooldown(gm);
        Debug.Log($"Tras un dt mayor al cooldown restante: cooldown={cooldown} (esperado <= 0)");
        if (cooldown > 0f)
            Debug.LogError("FALLÓ: el cooldown del pulso potente debería decaer con el tiempo (ActualizarOrbesNivel2 lo descuenta con dt).");
        else
            Debug.Log("OK: el cooldown decae solo — el pulso potente vuelve a estar disponible pasado el tiempo.");
    }

    static void PruebaOrbesDelBossValenMenos()
    {
        var gm = AbrirEscenaFresca();
        var fp = CrearFormaPrecisaDePrueba(gm, Vector2.zero);
        var campoEnergia = typeof(GameManager).GetField("energiaNivel2", Flags);

        RecolectarOrbeDirecto(gm, true); // origenBoss=true
        float energiaTrasOrbeBoss = (float)campoEnergia.GetValue(gm);
        Debug.Log($"Tras recolectar 1 orbe del boss: energiaNivel2={energiaTrasOrbeBoss} (esperado {gm.cargaPorOrbeBossNivel2}, MENOS que cargaPorOrbeNivel2={gm.cargaPorOrbeNivel2})");
        if (Mathf.Abs(energiaTrasOrbeBoss - gm.cargaPorOrbeBossNivel2) > 0.001f || gm.cargaPorOrbeBossNivel2 >= gm.cargaPorOrbeNivel2)
            Debug.LogError("FALLÓ: un orbe soltado por el boss debería cargar MENOS que uno normal — este es el freno #2 contra el loop.");
        else
            Debug.Log("OK: los orbes del boss valen menos — no alcanzan solos para volver a llenar la barra al toque.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
