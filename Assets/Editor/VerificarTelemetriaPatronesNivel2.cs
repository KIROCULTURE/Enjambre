using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Verifica (sin Play Mode) la instrumentación de telemetría agregada en
/// el punto 10: DispararPatronDeFase (Fase 1) y DispararPatronLaserFase2
/// (Fase 2) ahora DEVUELVEN el nombre del patrón elegido, no solo lo
/// disparan — sin esto, "qué patrón mata gente" (la pregunta real que el
/// balance necesita contestar) no se podía leer del CSV, solo la fase/tier
/// numérica. Ver GameManager.cs, el comentario de opcionesPatron.
/// </summary>
public static class VerificarTelemetriaPatronesNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    static readonly string[] NombresFase1Validos = { "barrido_simple", "cruz", "corredor", "abanico", "anillo_expansivo", "disparo_dirigido", "espiral", "espiral_doble", "flor_giratoria" };
    static readonly string[] NombresFase2Validos = { "barrido_simple", "cruz", "corredor", "abanico", "tela_radial", "espiral_giratoria" };

    [MenuItem("Enjambre/Debug/Verificar Telemetría de Patrones Nivel 2")]
    public static void Verificar()
    {
        PruebaDispararPatronDeFaseDevuelveNombreValido();
        PruebaDispararPatronLaserFase2DevuelveNombreValido();

        Debug.Log("Verificación de la telemetría de patrones de Nivel 2 completa.");
    }

    static GameManager AbrirEscenaFresca()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var raices = scene.GetRootGameObjects();
        var gm = raices.First(g => g.name == "GameManager").GetComponent<GameManager>();
        gm.estado = EstadoJuego.Jugando;
        Invocar(gm, "Awake");
        Invocar(gm, "ActualizarLimitesDesdeCamara");
        var fp = Object.Instantiate(gm.formaPrecisaPrefab, Vector2.zero, Quaternion.identity);
        Invocar(fp, "Awake");
        typeof(GameManager).GetField("formaPrecisaActiva", Flags).SetValue(gm, fp);
        var boss = Object.Instantiate(gm.administradorPrefab, new Vector3(0f, gm.mitadAlto * 0.6f, 0f), Quaternion.identity);
        typeof(GameManager).GetField("administradorActivo", Flags).SetValue(gm, boss);
        return gm;
    }

    /// <summary>Prueba las 5 fases (1-5, mismo rango que EscaladaPatronesFase1) muchas veces cada una — con pesos parejos, unas pocas tiradas alcanzan para ver nombres de sobra sin depender de qué tocó al azar.</summary>
    static void PruebaDispararPatronDeFaseDevuelveNombreValido()
    {
        var gm = AbrirEscenaFresca();
        var metodo = typeof(GameManager).GetMethod("DispararPatronDeFase", Flags);
        int invalidos = 0;
        string ultimoInvalido = "";
        for (int fase = 1; fase <= 5; fase++)
        {
            for (int i = 0; i < 20; i++)
            {
                string nombre = (string)metodo.Invoke(gm, new object[] { fase });
                if (string.IsNullOrEmpty(nombre) || !NombresFase1Validos.Contains(nombre)) { invalidos++; ultimoInvalido = nombre; }
            }
        }
        Debug.Log($"DispararPatronDeFase(1..5), 20 tiradas cada una: nombres inválidos/vacíos={invalidos} (esperado 0)" + (invalidos > 0 ? $", último inválido=\"{ultimoInvalido}\"" : ""));
        if (invalidos > 0)
            Debug.LogError("FALLÓ: DispararPatronDeFase debería devolver siempre uno de los nombres conocidos de patrón — el CSV de balance necesita saber CUÁL salió, no solo la fase.");
        else
            Debug.Log("OK: DispararPatronDeFase siempre devuelve el nombre del patrón que de verdad disparó.");
    }

    static void PruebaDispararPatronLaserFase2DevuelveNombreValido()
    {
        var gm = AbrirEscenaFresca();
        var metodo = typeof(GameManager).GetMethod("DispararPatronLaserFase2", Flags);
        int invalidos = 0;
        string ultimoInvalido = "";
        for (int i = 0; i < 30; i++)
        {
            string nombre = (string)metodo.Invoke(gm, new object[] { i % 2 == 0 }); // alterna climax true/false — las dos tablas de peso tienen que devolver nombres válidos
            if (string.IsNullOrEmpty(nombre) || !NombresFase2Validos.Contains(nombre)) { invalidos++; ultimoInvalido = nombre; }
        }
        Debug.Log($"DispararPatronLaserFase2(), 30 tiradas: nombres inválidos/vacíos={invalidos} (esperado 0)" + (invalidos > 0 ? $", último inválido=\"{ultimoInvalido}\"" : ""));
        if (invalidos > 0)
            Debug.LogError("FALLÓ: DispararPatronLaserFase2 debería devolver siempre uno de los 4 nombres reusados del viejo Show de Láseres.");
        else
            Debug.Log("OK: DispararPatronLaserFase2 siempre devuelve el nombre del patrón que de verdad disparó.");
    }

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
