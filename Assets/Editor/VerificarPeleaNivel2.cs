using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Verifica (sin Play Mode) la pelea real del Nivel 2 (punto 4 del
/// rediseño a boss fight) y su cierre (punto 5): movimiento del boss,
/// fases por tiempo, el pool de Proyectil (con un stress test de
/// rendimiento, mismo lenguaje que VerificarRendimientoSeparacion.cs), que
/// TODAS las fuentes de daño (proyectiles Y los 4 patrones de pared
/// heredados del Show de Láseres) lleguen de verdad a FormaPrecisa —
/// antes del punto 4 esta última parte estaba rota (ResolverImpactoLaser
/// solo miraba nucleosActivos, vacío a propósito en Nivel 2) — y los dos
/// caminos de la cutscene de cierre (ya vista antes / salto manual a
/// mitad de la real), mismo criterio que VerificarCutsceneApertura.cs
/// usa para la de apertura.
///
/// El bucle principal (PeleaNivel2, temporizado con WaitForSeconds por
/// fase) y la corrutina de la cutscene de cierre (CutsceneCierreNivel2)
/// no son tickeables en batch mode más allá del primer yield — mismo
/// límite que el resto del proyecto (ver CutsceneAperturaNivel2) — así
/// que se prueban por separado sus piezas síncronas: FaseEnTiempo y EscaladaPatronesFase1 (puras, dos ejes distintos desde Fase 6),
/// CalcularObjetivo del boss (pura), el pool de Proyectil, el
/// arranque+derrota de la pelea, y los dos caminos síncronos del cierre.
///
/// CUIDADO CON PLAYERPREFS: la cutscene de CIERRE sigue usando
/// MetaProgreso.CutsceneCierreVista, un flag persistente real — estas
/// pruebas lo fuerzan para llegar directo a donde hace falta sin tickear
/// la corrutina entera, y lo restauran en un try/finally (igual que
/// VerificarCutsceneApertura.cs). La de APERTURA ya no toca PlayerPrefs
/// (ver GameManager.cutsceneAperturaVistaSesion, campo de sesión en
/// memoria) — se fuerza directo por reflexión, sin guardar/restaurar nada.
/// </summary>
public static class VerificarPeleaNivel2
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string ClaveCutsceneCierreVista = "enjambre_cutscene_cierre_vista";
    static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

    [MenuItem("Enjambre/Debug/Verificar Pelea Nivel 2")]
    public static void Verificar()
    {
        var gmChequeo = AbrirEscenaFresca();
        if (gmChequeo.administradorPrefab == null || gmChequeo.formaPrecisaPrefab == null)
        {
            Debug.LogError("FALLÓ: falta administradorPrefab o formaPrecisaPrefab en GameManager — correr antes 'Crear Administrador del Sistema' y 'Crear Prefab Forma Precisa'.");
            return;
        }

        bool claveCierreExistia = PlayerPrefs.HasKey(ClaveCutsceneCierreVista);
        int valorCierreOriginal = claveCierreExistia ? PlayerPrefs.GetInt(ClaveCutsceneCierreVista) : 0;
        try
        {
            PruebaFaseEnTiempo();
            PruebaEscaladaPatronesFase1();
            PruebaCalcularObjetivoBoss();
            PruebaMovimientoBossConverge();
            PruebaRotarGrados();
            PruebaPoolProyectilesImpacta();
            PruebaResolverImpactoLaserContraFormaPrecisa();
            PruebaRendimientoProyectiles();
            PruebaPatronesNuevosNoRompen();
            PruebaArranqueYDerrotaSincronos();
            PruebaCutsceneCierreYaVista();
            PruebaCutsceneCierreSaltoManual();
        }
        finally
        {
            if (claveCierreExistia) PlayerPrefs.SetInt(ClaveCutsceneCierreVista, valorCierreOriginal);
            else PlayerPrefs.DeleteKey(ClaveCutsceneCierreVista);
            PlayerPrefs.Save();
            MetaProgreso.Cargar();
        }

        Debug.Log("Verificación de la pelea de Nivel 2 completa.");
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

    /// <summary>
    /// Fase 6 del rediseño grande: FaseEnTiempo pasó a ser el MAPA de la
    /// canción (Industrial Planet, 128s) en vez de la vieja escalada de
    /// dificultad sobre los 104.4s de AI Malware — reescrita a los nuevos
    /// umbrales (32/80/88/112), no solo borrada (mismo criterio que ya
    /// mordió a este proyecto una vez: una aserción vieja tapando un hueco
    /// real, ver project_fix_hueco_muralla_laser en las memorias).
    /// </summary>
    static void PruebaFaseEnTiempo()
    {
        var gm = AbrirEscenaFresca();
        var muestras = new (float t, int esperado)[]
        {
            (0f, 1), (31.9f, 1), (32f, 2), (79.9f, 2), (80f, 3), (87.9f, 3), (88f, 4), (111.9f, 4), (112f, 5), (128f, 5),
        };
        bool ok = true;
        foreach (var (t, esperado) in muestras)
        {
            int fase = gm.FaseEnTiempo(t);
            if (fase != esperado) { ok = false; Debug.LogError($"FALLÓ: FaseEnTiempo({t}) = {fase}, esperado {esperado}."); }
        }
        Debug.Log(ok ? "OK: FaseEnTiempo devuelve el mapa de canción esperado (1/2/3/4/5 en 0/32/80/88/112s)." : "Ver errores de FaseEnTiempo arriba.");
    }

    /// <summary>La escalada de dificultad DE LOS PATRONES (eje separado de FaseEnTiempo desde Fase 6) — cuantizada a frases de ~16.35s dentro del cuerpo de Fase 1 (0-80s).</summary>
    static void PruebaEscaladaPatronesFase1()
    {
        var gm = AbrirEscenaFresca();
        var muestras = new (float t, int esperado)[]
        {
            (0f, 1), (16.3f, 1), (16.4f, 2), (32.6f, 2), (32.8f, 3), (49f, 3), (49.1f, 4), (65.3f, 4), (65.5f, 5), (80f, 5),
        };
        bool ok = true;
        foreach (var (t, esperado) in muestras)
        {
            int fase = gm.EscaladaPatronesFase1(t);
            if (fase != esperado) { ok = false; Debug.LogError($"FALLÓ: EscaladaPatronesFase1({t}) = {fase}, esperado {esperado}."); }
        }
        Debug.Log(ok ? "OK: EscaladaPatronesFase1 escala 1-5 cuantizado a límites de frase (~16.35s)." : "Ver errores de EscaladaPatronesFase1 arriba.");
    }

    static void PruebaCalcularObjetivoBoss()
    {
        const float mitadAncho = 4.2f, mitadAlto = 2.6f, margen = 0.7f, distanciaMinima = 1.7f, fraccionAlturaDescanso = 0.55f;
        var muestras = new[] { Vector2.zero, new Vector2(3.5f, 2.2f), new Vector2(-3.5f, -2.2f), new Vector2(0f, 2.3f) };
        bool ok = true;
        foreach (var posJugador in muestras)
        {
            Vector2 objetivo = AdministradorSistema.CalcularObjetivo(posJugador, mitadAncho, mitadAlto, margen, distanciaMinima, fraccionAlturaDescanso);
            bool dentro = Mathf.Abs(objetivo.x) <= mitadAncho - margen + 0.001f && Mathf.Abs(objetivo.y) <= mitadAlto - margen + 0.001f;
            float distancia = Vector2.Distance(objetivo, posJugador);
            bool distanciaOk = distancia >= distanciaMinima - 0.001f;
            Debug.Log($"CalcularObjetivo(jugador={posJugador}) = {objetivo}, distancia={distancia:F2} (mínima {distanciaMinima}), dentro del área={dentro}");
            if (!dentro || !distanciaOk) { ok = false; Debug.LogError($"FALLÓ para jugador={posJugador}: dentro={dentro}, distanciaOk={distanciaOk}."); }
        }
        Debug.Log(ok ? "OK: el boss siempre queda dentro del área y respeta la distancia mínima al jugador." : "Ver errores de CalcularObjetivo arriba.");

        // Bug real reportado: acercarse por ABAJO no generaba ninguna
        // reacción visible — el objetivo en Y ya nacía pegado al techo
        // (mismo número que el límite del clamp), así que "retroceder"
        // no tenía a dónde ir. Confirma que ahora SÍ hay una altura de
        // descanso por debajo del techo, y que acercarse por abajo mueve
        // el objetivo de verdad hacia arriba (retroceso visible), no que
        // se quede pegado al mismo valor de siempre.
        float alturaDescanso = mitadAlto * fraccionAlturaDescanso;
        float techo = mitadAlto - margen;
        Vector2 objetivoLejos = AdministradorSistema.CalcularObjetivo(new Vector2(0f, -2.0f), mitadAncho, mitadAlto, margen, distanciaMinima, fraccionAlturaDescanso);
        Vector2 objetivoCerca = AdministradorSistema.CalcularObjetivo(new Vector2(0f, 1.0f), mitadAncho, mitadAlto, margen, distanciaMinima, fraccionAlturaDescanso);
        Debug.Log($"Acercándose desde abajo: lejos(y=-2.0)->objetivo.y={objetivoLejos.y:F2} (reposo, esperado {alturaDescanso:F2}); cerca(y=1.0)->objetivo.y={objetivoCerca.y:F2} (debería retroceder por encima del reposo, hasta cerca del techo {techo:F2})");
        if (Mathf.Abs(alturaDescanso - techo) < 0.05f)
            Debug.LogError("FALLÓ: alturaDescanso y techo son prácticamente el mismo valor — no queda aire para retroceder (el bug original).");
        else if (objetivoCerca.y <= objetivoLejos.y + 0.05f)
            Debug.LogError("FALLÓ: acercarse por abajo debería empujar el objetivo hacia arriba de verdad, no quedarse en la misma altura de reposo.");
        else
            Debug.Log("OK: hay aire real entre la altura de reposo y el techo — acercarse por abajo ahora sí retrocede visiblemente.");
    }

    static void PruebaMovimientoBossConverge()
    {
        var gm = AbrirEscenaFresca();
        var boss = Object.Instantiate(gm.administradorPrefab, new Vector3(-3f, -2f, 0f), Quaternion.identity);

        Vector2 objetivo = new Vector2(2.5f, 1.9f);
        const float dt = 1f / 60f;
        for (int i = 0; i < 600; i++) // 10s simulados — de sobra para converger a velocidadMovimiento=2
            boss.Mover(objetivo, dt);

        float distanciaFinal = Vector2.Distance(boss.transform.position, objetivo);
        Debug.Log($"Tras 10s simulados de Mover(): posición={boss.transform.position}, distancia al objetivo={distanciaFinal:F3}");
        if (distanciaFinal > 0.01f)
            Debug.LogError("FALLÓ: Mover() debería converger al objetivo con tiempo de sobra.");
        else
            Debug.Log("OK: el boss converge al punto calculado sin overshoot (MoveTowards).");
    }

    /// <summary>Matemática pura de PatronFlorGiratoria (Proyectil.velocidadAngular) — sin depender de Time.deltaTime, que en Edit Mode no es controlable.</summary>
    static void PruebaRotarGrados()
    {
        var metodo = typeof(GameManager).GetMethod("RotarGrados", BindingFlags.NonPublic | BindingFlags.Static);
        Vector2 rotado90 = (Vector2)metodo.Invoke(null, new object[] { new Vector2(1f, 0f), 90f });
        Vector2 rotadoMenos90 = (Vector2)metodo.Invoke(null, new object[] { new Vector2(1f, 0f), -90f });
        Debug.Log($"RotarGrados((1,0), 90°)={rotado90} (esperado ≈(0,1)), RotarGrados((1,0), -90°)={rotadoMenos90} (esperado ≈(0,-1))");
        bool ok = Vector2.Distance(rotado90, new Vector2(0f, 1f)) < 0.001f && Vector2.Distance(rotadoMenos90, new Vector2(0f, -1f)) < 0.001f;
        if (!ok) Debug.LogError("FALLÓ: RotarGrados no rota como se espera — las balas curvas de PatronFlorGiratoria curvarían mal.");
        else Debug.Log("OK: RotarGrados rota en el sentido esperado — la base de las balas curvas (flor giratoria) es correcta.");
    }

    static void PruebaPoolProyectilesImpacta()
    {
        var gm = AbrirEscenaFresca();
        var fp = CrearFormaPrecisaDePrueba(gm, Vector2.zero);

        // Un disparo lejano y quieto (velocidad cero — así el resultado no
        // depende de qué valga Time.deltaTime en Edit Mode) más uno ya
        // encima del hitbox, para distinguir "recicla SOLO el que pegó"
        // de "recicla todo".
        gm.DispararProyectil(new Vector2(3f, 1.5f), Vector2.zero, Color.white);
        gm.DispararProyectil(Vector2.zero, Vector2.zero, Color.white);
        int vidasAntes = fp.vidas;

        var metodoActualizar = typeof(GameManager).GetMethod("ActualizarProyectiles", Flags);
        metodoActualizar.Invoke(gm, null);

        Debug.Log($"Tras ActualizarProyectiles con una bala encima del hitbox: vidas {vidasAntes}->{fp.vidas}, activos={gm.ProyectilesActivosCount} (esperado 1 — solo queda la lejana)");
        if (fp.vidas != vidasAntes - 1 || gm.ProyectilesActivosCount != 1)
            Debug.LogError("FALLÓ: debería restar 1 vida y reciclar únicamente la bala que pegó.");
        else
            Debug.Log("OK: el pool mueve, resuelve impacto contra FormaPrecisa (vía RecibirGolpe) y recicla solo la bala que pegó.");
    }

    static void PruebaResolverImpactoLaserContraFormaPrecisa()
    {
        var gm = AbrirEscenaFresca();
        var fp = CrearFormaPrecisaDePrueba(gm, Vector2.zero);
        int vidasAntes = fp.vidas;

        gm.ResolverImpactoLaser(new Rect(-1f, -1f, 2f, 2f)); // contiene (0,0)

        Debug.Log($"Tras ResolverImpactoLaser con FormaPrecisa adentro del área: vidas {vidasAntes}->{fp.vidas}");
        if (fp.vidas != vidasAntes - 1)
            Debug.LogError("FALLÓ: los 4 patrones de pared heredados deberían dañar a la Forma Precisa, no solo a los viejos Nucleo.");
        else
            Debug.Log("OK: ResolverImpactoLaser (Barrido/Cruz/Corredor/Abanico) ahora sí golpea a la Forma Precisa.");
    }

    static void PruebaRendimientoProyectiles()
    {
        var gm = AbrirEscenaFresca();
        // Sin FormaPrecisa activa a propósito: aísla el costo de mover +
        // chequear límites de mundo, sin que el reciclado por impacto
        // achique la lista a mitad de la corrida cronometrada.
        const int cantidad = 300;
        for (int i = 0; i < cantidad; i++)
            gm.DispararProyectil(new Vector2(Random.Range(-3f, 3f), Random.Range(-2f, 2f)), Random.insideUnitCircle * 2.5f, Color.white);

        var metodoActualizar = typeof(GameManager).GetMethod("ActualizarProyectiles", Flags);
        var reloj = Stopwatch.StartNew();
        metodoActualizar.Invoke(gm, null); // un frame real de mover+chequear las 300 balas
        reloj.Stop();

        double ms = reloj.Elapsed.TotalMilliseconds;
        Debug.Log($"ActualizarProyectiles para {cantidad} balas activas: {ms:F3} ms (presupuesto de un frame a 60fps: 16.6ms).");
        if (ms > 8.0)
            Debug.LogError($"FALLÓ (posible): {ms:F3} ms es una porción grande del presupuesto de un frame — revisar si hace falta un grid espacial como el de Nucleo.");
        else
            Debug.Log("OK: muy por debajo del presupuesto de un frame — no debería notarse como lag.");
    }

    /// <summary>
    /// Los 5 patrones basados en Proyectil arrancan sin excepción — el
    /// resto de cada uno vive detrás de un yield (telegraph), no
    /// tickeable en batch mode, mismo límite que el resto del proyecto.
    /// Cubre en particular los 2 patrones nuevos (EspiralDoble,
    /// FlorGiratoria) y las firmas con `fase` que absorbieron los 3 que
    /// ya existían.
    /// </summary>
    static void PruebaPatronesNuevosNoRompen()
    {
        var gm = AbrirEscenaFresca();
        gm.estado = EstadoJuego.Jugando;
        CrearFormaPrecisaDePrueba(gm, Vector2.zero);
        var boss = Object.Instantiate(gm.administradorPrefab, new Vector3(0f, gm.mitadAlto * 0.6f, 0f), Quaternion.identity);
        typeof(GameManager).GetField("administradorActivo", Flags).SetValue(gm, boss);

        void Invocar(string metodo, object[] args)
        {
            try
            {
                typeof(GameManager).GetMethod(metodo, Flags).Invoke(gm, args);
                Debug.Log($"OK: {metodo} arrancó sin excepción.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"FALLÓ: {metodo} lanzó una excepción al arrancar: {e.InnerException ?? e}");
            }
        }

        Invocar("PatronAnilloExpansivo", new object[] { 3 });
        Invocar("PatronEspiral", new object[] { 3 });
        Invocar("PatronDisparoDirigido", new object[] { 4 });
        Invocar("PatronEspiralDoble", null);
        Invocar("PatronFlorGiratoria", null);
    }

    static void PruebaArranqueYDerrotaSincronos()
    {
        var gm = AbrirEscenaFresca();
        ForzarAperturaVistaSesion(gm, true); // salta la cutscene entera, sin corrutina, directo a la pelea

        gm.BotonJugarNivel2();

        var campoFormaPrecisaActiva = typeof(GameManager).GetField("formaPrecisaActiva", Flags);
        var fp = (FormaPrecisa)campoFormaPrecisaActiva.GetValue(gm);
        Debug.Log($"Tras BotonJugarNivel2() (cutscene ya vista): estado={gm.estado} (esperado Jugando), PeleaActiva={gm.PeleaActiva} (esperado true), TiempoPelea={gm.TiempoPelea} (esperado 0)");
        if (gm.estado != EstadoJuego.Jugando || !gm.PeleaActiva || gm.TiempoPelea != 0f || fp == null)
        {
            Debug.LogError("FALLÓ: el arranque síncrono de la pelea no dejó el estado esperado.");
            return;
        }
        Debug.Log("OK: IniciarPeleaNivel2() arranca la pelea de una (reloj propio en 0, PeleaActiva=true).");

        // 3 golpes consecutivos deberían vaciar las vidas y disparar la
        // derrota — limpiando `invulnerable` a mano entre golpes (simula
        // que pasó tiempo real), mismo truco que VerificarFormaPrecisa.cs.
        var campoInvulnerable = typeof(FormaPrecisa).GetField("invulnerable", Flags);
        while (fp.vidas > 0)
        {
            campoInvulnerable.SetValue(fp, false);
            fp.RecibirGolpe("prueba");
        }

        Debug.Log($"Tras vaciar las vidas: estado={gm.estado} (esperado Fin), PeleaActiva={gm.PeleaActiva} (esperado false), panelFin activo={gm.panelFin.activeSelf}, texto=\"{gm.textoResultado.text}\"");
        if (gm.estado != EstadoJuego.Fin || gm.PeleaActiva || !gm.panelFin.activeSelf || !gm.textoResultado.text.Contains("Te borró"))
            Debug.LogError("FALLÓ: quedarse sin vidas debería terminar la pelea en derrota (panelFin con el texto correspondiente).");
        else
            Debug.Log("OK: AlQuedarSinVidas -> ManejarDerrotaNivel2 -> TerminarPeleaNivel2(derrota) resuelve todo de una.");
    }

    /// <summary>
    /// Con la cutscene de cierre YA vista (ganar de nuevo en una partida
    /// futura), PeleaNivel2Victoria() tiene que resolver todo de una: sin
    /// corrutina, directo al placeholder de Nivel 3 y a panelFin. El
    /// límite real (duracionPeleaNivel2, 128s) solo se cruza tickeando PeleaNivel2() (no
    /// tickeable en batch mode) — se prueba el cierre invocando el
    /// método privado directo, mismo criterio que el resto del proyecto
    /// para piezas de una corrutina que no llegan a tickearse (ver
    /// AsegurarFormaPrecisa en VerificarCutsceneApertura.cs).
    /// </summary>
    static void PruebaCutsceneCierreYaVista()
    {
        var gm = AbrirEscenaFresca();
        ForzarAperturaVistaSesion(gm, true);
        ForzarCutsceneVista(ClaveCutsceneCierreVista, true);
        gm.BotonJugarNivel2();

        var metodoVictoria = typeof(GameManager).GetMethod("PeleaNivel2Victoria", Flags);
        metodoVictoria.Invoke(gm, null);

        var fp = (FormaPrecisa)typeof(GameManager).GetField("formaPrecisaActiva", Flags).GetValue(gm);
        var formaNivel3 = (GameObject)typeof(GameManager).GetField("formaNivel3Activa", Flags).GetValue(gm);
        Debug.Log($"Tras PeleaNivel2Victoria() (cierre ya vista): estado={gm.estado} (esperado Fin), PeleaActiva={gm.PeleaActiva} (esperado false), FormaPrecisaActiva!=null: {fp != null} (esperado false), formaNivel3Activa!=null: {formaNivel3 != null} (esperado true), texto=\"{gm.textoResultado.text}\"");
        if (gm.estado != EstadoJuego.Fin || gm.PeleaActiva || fp != null || formaNivel3 == null || !gm.textoResultado.text.Contains("Nivel 3"))
            Debug.LogError("FALLÓ: con la cutscene de cierre ya vista, ganar debería resolverse de una (Forma Precisa -> placeholder de Nivel 3 -> panelFin).");
        else
            Debug.Log("OK: ya vista -> salto instantáneo directo al cierre, sin repetir la cutscene entera.");
    }

    /// <summary>Botón "Saltar" a mitad de la cutscene de cierre real (no vista).</summary>
    static void PruebaCutsceneCierreSaltoManual()
    {
        var gm = AbrirEscenaFresca();
        ForzarAperturaVistaSesion(gm, true); // solo la de apertura — la de cierre es la que se prueba acá
        ForzarCutsceneVista(ClaveCutsceneCierreVista, false);
        gm.BotonJugarNivel2();

        var metodoVictoria = typeof(GameManager).GetMethod("PeleaNivel2Victoria", Flags);
        metodoVictoria.Invoke(gm, null); // arranca la corrutina real, stuck tras el primer yield (MostrarDialogo)

        var campoEnCierre = typeof(GameManager).GetField("enCutsceneCierre", Flags);
        bool enCierre = (bool)campoEnCierre.GetValue(gm);
        Debug.Log($"Tras PeleaNivel2Victoria() (cierre, primera vez): estado={gm.estado} (esperado Cutscene), enCutsceneCierre={enCierre} (esperado true), panelCutsceneNivel2 activo={gm.panelCutsceneNivel2.activeSelf}, botonSaltar activo={gm.botonSaltarCutscene.activeSelf}");
        if (gm.estado != EstadoJuego.Cutscene || !enCierre || !gm.panelCutsceneNivel2.activeSelf || !gm.botonSaltarCutscene.activeSelf)
        {
            Debug.LogError("FALLÓ: el arranque síncrono de la cutscene de cierre no dejó el estado esperado.");
            return;
        }

        gm.SaltarCutscene();

        var fp = (FormaPrecisa)typeof(GameManager).GetField("formaPrecisaActiva", Flags).GetValue(gm);
        var formaNivel3 = (GameObject)typeof(GameManager).GetField("formaNivel3Activa", Flags).GetValue(gm);
        Debug.Log($"Tras Saltar() a mitad del cierre: estado={gm.estado} (esperado Fin), FormaPrecisaActiva!=null: {fp != null} (esperado false), formaNivel3Activa!=null: {formaNivel3 != null} (esperado true), CutsceneCierreVista={MetaProgreso.CutsceneCierreVista} (esperado true)");
        if (gm.estado != EstadoJuego.Fin || fp != null || formaNivel3 == null || !MetaProgreso.CutsceneCierreVista)
            Debug.LogError("FALLÓ: Saltar() debería resolver el resto del cierre de una (transformación a Nivel 3 incluida), no dejarlo a medias.");
        else
            Debug.Log("OK: Saltar() resuelve el resto de la cutscene de cierre de una, no la deja colgada.");
    }

    static void ForzarCutsceneVista(string clave, bool vista)
    {
        PlayerPrefs.SetInt(clave, vista ? 1 : 0);
        PlayerPrefs.Save();
        MetaProgreso.Cargar();
    }

    static void ForzarAperturaVistaSesion(GameManager gm, bool vista) =>
        typeof(GameManager).GetField("cutsceneAperturaVistaSesion", Flags).SetValue(gm, vista);

    static void Invocar(object obj, string metodo) =>
        obj.GetType().GetMethod(metodo, Flags).Invoke(obj, null);
}
