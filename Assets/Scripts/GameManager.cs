using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public enum EstadoJuego { Menu, Jugando, Pausado, Fin, Cutscene }

/// <summary>
/// Controlador central: estado de la partida, spawns, input y cálculo
/// del centro de masa del enjambre (lo persiguen los enemigos).
/// Equivale a las funciones sueltas del prototipo web (paso(), reset(),
/// generarEnemigo(), etc.) pero organizadas como un singleton de Unity.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instancia { get; private set; }

    [Header("Estado")]
    public EstadoJuego estado = EstadoJuego.Menu;

    [Header("Prefabs de juego")]
    public Nucleo nucleoPrefab;
    public Enemigo enemigoPrefab;
    public Orbe orbePrefab;
    // Personaje jugable del Nivel 2 (punto 2 del rediseño a boss fight) —
    // todavía sin conectar al flujo real de juego: la cutscene de
    // apertura (punto 3) es la que lo instancia de verdad, en el momento
    // de la transformación. Por ahora solo existe el prefab + su script,
    // verificados sueltos (ver VerificarFormaPrecisa.cs).
    public FormaPrecisa formaPrecisaPrefab;

    [Header("Núcleo — ajusta jugando")]
    public float radioInicial = 0.5f;
    public float radioMinimo = 0.18f;
    public float radioMaximo = 0.8f;
    public float crecimientoPorOrbe = 0.05f;
    public float ratioAlPartir = 0.5f;
    // Al partirse, los fragmentos salen despedidos lejos del enemigo que
    // pegó (no en una dirección al azar) — da más feedback del golpe, y
    // como todos los núcleos persiguen el mismo Objetivo, se reincorporan
    // solos al grupo apenas se les pasa el impulso.
    public float fuerzaKnockback = 3.2f;
    // Destello del sprite "golpeado" (naranja) al nacer un fragmento recién partido.
    public float duracionFlashGolpeado = 0.15f;

    [Header("Separación entre núcleos — evita que se apilen unos sobre otros")]
    // Todos persiguen el mismo Objetivo (ver GameManager.Objetivo), así que
    // sin esto terminaban apilados en el mismo punto exacto en vez de
    // leerse como un enjambre disperso. Cada núcleo empuja a los demás que
    // tenga demasiado cerca — el mismo truco de "separación" de un boids
    // clásico, sumado como una fuerza más sobre la velocidad existente.
    public float margenSeparacionNucleos = 1.15f;
    public float fuerzaSeparacionNucleos = 6f;
    // Tope duro de vecinos que CADA núcleo procesa por frame (ver
    // Nucleo.ComputarSeparacion) — sin esto, un enjambre grande y COMPACTO
    // (persiguiendo el mismo Objetivo, no esparcido por el mundo) seguía
    // costando caro incluso con el grid espacial de abajo, porque todos
    // caen en las mismas celdas. Es una fuerza sumada entre vecinos, no una
    // regla exacta, así que recortarla a los primeros N no cambia la
    // dirección de la separación, solo la afina un poco menos.
    public int maxVecinosSeparacion = 20;

    [Header("Achique por cantidad — para que muchos núcleos no absorban el mapa")]
    // Con muchos fragmentos, la separación de arriba por sí sola los
    // esparciría cada vez más lejos (con 50 núcleos, terminarían cubriendo
    // el mapa entero). En vez de eso, a partir de
    // cantidadDondeEmpiezaAchique se van achicando (visual Y espacio
    // personal, ambos a la vez) hasta un piso de
    // factorEscalaMinimoPorCantidad — nunca desaparecen del todo, pero un
    // enjambre grande ocupa un área más acotada en vez de crecer sin límite.
    public int cantidadDondeEmpiezaAchique = 6;
    public int cantidadParaEscalaMinima = 40;
    public float factorEscalaMinimoPorCantidad = 0.55f;

    [Header("Spawns")]
    // El mundo jugable ahora es factorMundo² (~2.25x) más grande que antes
    // (ver GameManager.mitadMundoAncho/Alto) — con solo 3 gemas dando
    // vueltas por ahí, quedaban demasiado lejos unas de otras para
    // encadenar combos. Subido para mantener una densidad similar a la
    // de antes de agrandar el mundo.
    public int orbesObjetivo = 7;
    // Las gemas ya no se reponen en cualquier punto del mundo (grande):
    // se reponen cerca de donde recién agarraste una, para que armar
    // cadenas de combo no dependa de la suerte de que la siguiente te
    // haya tocado cerca. Ver GenerarOrbeCercaDe. radioReposicionMinimo > 0
    // para que no aparezcan literalmente encima tuyo (eso hacía el combo
    // trivial, sin nada que apuntar/mover) — siempre hay que moverse un
    // poco, pero no salir a buscarla del otro lado del mapa.
    public float radioReposicionMinimo = 1f;
    public float radioReposicionOrbe = 3f;
    public float intervaloEnemigoInicial = 1.6f;
    public float intervaloEnemigoMinimo = 0.5f;
    public float rampaDificultad = 0.02f;
    public float telegraphDuracion = 0.4f;
    public float dificultadEnemigoMax = 3.2f;
    public float dificultadEnemigoRampa = 24f;

    [Header("Orbe especial de velocidad")]
    public float intervaloOrbeVelocidadMin = 14f;
    public float intervaloOrbeVelocidadMax = 22f;
    public float duracionBoostVelocidad = 5f;
    public float multiplicadorBoostVelocidad = 1.8f;

    [Header("Orbe especial explosivo — hace estallar varias gemas alrededor")]
    public float intervaloOrbeExplosivoMin = 20f;
    public float intervaloOrbeExplosivoMax = 30f;
    public int orbesPorExplosion = 6;
    // Con el núcleo grande de vuelta, un radio chico dejaba comerse toda la
    // explosión de un solo bocado — más grande, y con un mínimo (para que
    // no salgan todas apiladas justo en el centro), obliga a moverse un
    // poco para juntarlas todas.
    public float radioExplosionMinimo = 1f;
    public float radioExplosion = 3.2f;

    [Header("Combo de gemas")]
    public float ventanaCombo = 1.5f;

    [Header("Recompensa de combo — cada 10 gemas seguidas")]
    // Temporal y decae sola (mismo patrón que el boost de velocidad del
    // orbe amarillo), no un multiplicador permanente que se vaya
    // acumulando partida tras partida.
    public int comboParaMultiplicador = 10;
    public float duracionBoostCrecimiento = 6f;
    public float multiplicadorBoostCrecimiento = 2f;

    [Header("Fever — cada 20 combos, estilo DJMAX (temporal)")]
    // A diferencia de una versión anterior, esto ya NO es permanente: cada
    // hito de 20 combos (re)arranca un cronómetro de duracionFever
    // segundos. Mientras dura, sube de nivel (hasta nivelFeverMax) y da más
    // gemas en pantalla, pero si no lo renovás encadenando otro combo de
    // 20 antes de que se acabe, el nivel va bajando solo de a uno cada
    // intervaloDecaimientoFever segundos hasta apagarse del todo — como el
    // boost de crecimiento de arriba, no un multiplicador que se acumule
    // partida tras partida.
    public int comboPorNivelFever = 20;
    public int nivelFeverMax = 5;
    public int orbesExtraPorNivelFever = 1;
    public float duracionFever = 15f;
    public float intervaloDecaimientoFever = 4f;
    // Como la dificultad/spawn de abajo leen nivelFever en vivo cada frame,
    // que el nivel decaiga solo ya alcanza para que la presión tampoco sea
    // permanente — jugar mejor sube el desafío mientras estás en racha,
    // pero afloja solo si dejás de encadenar combos.
    public float dificultadExtraPorNivelFever = 0.45f;
    public float intervaloMenosPorNivelFever = 0.07f;
    public float intervaloMinimoAbsoluto = 0.18f;

    [Header("Fever — densidad/variedad de enemigos (no patrones de proyectiles)")]
    // Antes cada nivel de Fever disparaba un patrón de proyectiles a
    // esquivar (curtain/pincer/espiral/ráfaga) — mecánica de bullet hell
    // que no encaja con un survivor-like: acá el jugador controla una masa
    // que se expande, no una nave con hitbox preciso, y la dificultad real
    // de este género sale de densidad/variedad de horda, no de patrones a
    // leer. Ahora Fever solo desbloquea/acelera 3 tipos de enemigo nuevos
    // (ver Enemigo.TipoAtaque y los Actualizar/Spawn de abajo), sumados al
    // homing normal (que ya no se pausa durante Fever).
    public float intervaloCazador = 3.2f;
    float tSpawnCazador;
    public float intervaloCoagulo = 9f;
    float tSpawnCoagulo;
    public int maxCoagulosSimultaneos = 3;
    public float intervaloRival = 7f;
    float tSpawnRival;
    public int rivalesPorCluster = 4;

    [Header("Evento especial — Muralla Láser (por tiempo, no por combo ni Fever)")]
    // Primera versión: se disparaba cada 250 combos seguidos. El usuario
    // jugó y lo sintió al revés de "amenazante" — llegar a 250 sin cortar
    // combo ya significa que estás jugando muy bien, así que un golpe
    // fuerte justo ahí se leía como un castigo arbitrario a la buena
    // racha, no como un peligro real. Ahora es un reloj aparte, para
    // TODOS por igual sin importar cómo estés jugando — "en algún
    // momento del juego van a aparecer rayos" en vez de "si te va
    // demasiado bien, te castigo". Se acorta con la escalada global
    // (MultiplicadorIntervaloEscalada), como todo lo demás que spawnea.
    public float tiempoPrimerEventoLaser = 45f;
    public float intervaloEventoLaser = 55f;
    float tProximoEventoLaser;
    // Un poco más larga que antes (0.7 -> 0.9): ahora la zona segura se
    // desplaza de donde estás parado (ver RafagaMurallaLaser), así que
    // moverse de verdad es obligatorio — necesita algo más de margen real
    // para reaccionar que cuando alcanzaba con no cortarse.
    public float telegraphLaser = 0.9f;
    public float duracionActivoLaser = 0.18f;
    [Range(0.3f, 0.9f)] public float anchoZonaSeguraLaser = 0.65f;
    // Más agresivo que un golpe normal (ratioAlPartir=0.5) — "casi
    // aniquila" como pidió el usuario, pero no mata de un solo golpe a un
    // núcleo bien crecido (queda un fragmento chico, no cero).
    public float ratioAlPartirLaser = 0.3f;
    // HDR (canales > 1) a propósito: con Bloom.threshold=1 esto brilla de
    // verdad en vez de ser un rojo plano — mismo truco que Nucleo/Orbe
    // colorVivido (ver EstiloMinimal/post-procesado).
    static readonly Color ColorLaser = new Color(2.2f, 0.33f, 0.33f);

    [Header("Victoria — final de la demo (no es una muerte)")]
    // Retuneado con 21 muertes reales acumuladas (no solo las 2 partidas
    // con mejoras al máximo que dieron el primer número de 120s): la
    // mayoría de las partidas reales caen entre 95 y 120s, con varias
    // muriendo justo ANTES de los 120 (114.1s, 118.7s) — con ese umbral la
    // mayoría de las partidas buenas se quedaban a centímetros de ver la
    // victoria. 105s deja pasar ~el 57% de esas 21 partidas (12/21) en vez
    // de solo ~28% (6/21) con 120s — para una demo que se muestra en vivo,
    // que la victoria sea alcanzable de verdad importa más que un número
    // "prestigioso". Se puede seguir retuneando con más datos.
    public float tiempoVictoria = 105f;
    bool victoriaDisparada;
    // Distinto de `estado`: durante esto el enjambre TODAVÍA se puede
    // mover (el jugador tiene que guiarlo adentro del portal), solo se
    // frenan los spawns nuevos — ver el gate en Update().
    bool secuenciaVictoriaActiva;

    [Header("Nivel 2 — modo boss fight")]
    // modoNivel2 significa "boss fight" — EntrarCutsceneNivel2() lo pone
    // en true (tanto viniendo del portal como del atajo de menú). Los 4
    // patrones de pared de abajo (PatronBarridoSimple/Cruz/Corredor/Abanico),
    // originalmente del viejo Show de Láseres, ahora son la familia "láser
    // telegrafiado" de la pelea real (punto 4, ver PeleaNivel2()) — se
    // llaman igual que antes (el origen sigue siendo CentroDeMasa(), que
    // en Nivel 2 da Vector2.zero porque nucleosActivos queda vacío a
    // propósito, es decir el centro fijo de la arena — no hacía falta
    // parametrizarlo a la posición del boss, esas paredes ya cruzan toda
    // la pantalla). Lo único que cambió es el daño: ver el bloque nuevo en
    // ResolverImpactoLaser más abajo. El viejo driver por temporizador
    // ActualizarShowLaseres queda reemplazado por completo por
    // PeleaNivel2()/DispararPatronDeFase (temporizado por fase, no fijo).
    public bool modoNivel2;
    public float telegraphShowLaser = 0.9f;
    public float duracionActivoShowLaser = 0.18f;
    [Range(0.06f, 0.3f)] public float anchoRayoShowLaser = 0.14f;

    [Header("Escalada global por tiempo — sube de golpe cada 90s")]
    // Independiente del Fever (que depende de que juegues bien y
    // encadenes combos): esto sube solo con el reloj, para que ninguna
    // partida se sienta "resuelta" — cada hito de intervaloEscaladaGlobal
    // segundos multiplica de golpe la dificultad/velocidad de TODO
    // enemigo (homing y los 4 patrones de ráfaga por igual) y hace que
    // todo spawnee más seguido. nivelEscaladaMax=21 llega al tope recién a
    // los 30 minutos (90s × 20 escalones) — un techo real sigue estando
    // ahí (para no volverse matemáticamente imposible en partidas
    // extremas), pero recién en un "late game" de verdad, no a los 7:30
    // como en la versión anterior.
    public float intervaloEscaladaGlobal = 90f;
    public int nivelEscaladaMax = 21;
    public float multiplicadorDificultadPorEscalada = 0.15f;
    public float multiplicadorIntervaloPorEscalada = 0.91f;

    [Header("Hit-stop (freeze-frame al golpear)")]
    public float duracionHitStop = 0.05f;
    public float escalaHitStop = 0.04f;

    [Header("Hitos de tiempo sobrevivido")]
    public float[] hitosTiempo = { 15f, 30f, 60f, 90f, 120f, 180f, 240f };

    [Header("Escalada progresiva (intensidad visual)")]
    public float climaxSegundos = 45f;

    /// <summary>
    /// 0 al arrancar, 1 en el clímax (y se mantiene en 1 si la partida
    /// sigue más allá de climaxSegundos). Todo lo visual —paleta, fondo,
    /// cámara, partículas, popups— lee este único número cada frame en
    /// vez de tener su propia lógica de "qué tan intenso está esto".
    /// </summary>
    public float Intensidad => Mathf.Clamp01(tiempo / climaxSegundos);

    [Header("Límites del mundo de juego")]
    public float mitadAncho = 4.2f;
    public float mitadAlto = 2.6f;

    // El mundo jugable es más grande que lo que la cámara muestra a la vez
    // (estilo Agar.io): CameraPunch.cs persigue al enjambre dentro de este
    // rectángulo más grande en vez de quedarse fija mostrando todo el mundo.
    [Header("Mundo — más grande que la cámara (estilo Agar.io)")]
    [Tooltip("Cuántas veces más grande es el mundo jugable que lo que la cámara alcanza a mostrar.")]
    public float factorMundo = 1.5f;
    public float mitadMundoAncho { get; private set; }
    public float mitadMundoAlto { get; private set; }

    [Header("Shaders (punto 6 del rediseño a boss fight) — opcionales, con respaldo si faltan")]
    // Usados con MaterialPropertyBlock (ver LaserHazard.cs) para animar
    // el flujo de energía del haz sin instanciar un Material por pared —
    // si no están asignados (p.ej. una escena vieja sin correr el setup
    // de nuevo), LaserHazard cae de vuelta al degradé horneado en textura
    // (ShapeFactory.Haz), sin romperse.
    public Material materialHazEnergia;
    public Material materialPortal;
    // Punto 9 — el único momento con shader hecho a mano fuera de las
    // paredes láser/el portal (pedido explícito: "reservado para momentos
    // clave", no en todo). Opcional: null = la transformación se queda
    // solo con el recoloreo/escala de siempre (ver AsegurarSuperAdministradorNivel2),
    // mismo criterio de fallback que materialHazEnergia con CrearHazVisual.
    public Material materialCorrupcionGlitchNivel2;

    [Header("Referencias de escena")]
    public Camera camara;
    public HudController hud;
    // "hud" es el componente en "Hud_Controller" (un hijo chico sin
    // visual propio); prender/apagarlo a él no toca a sus hermanos
    // T_Time/T_Cores/T_Record/T_Fever, que son los que realmente se ven.
    // Lo que hay que prender/apagar es la raíz "HUD" completa.
    public GameObject hudRoot;
    public GameObject panelMenu;
    public GameObject panelFin;
    public GameObject panelCreditos;
    public GameObject panelInstrucciones;
    public GameObject panelPausa;
    public GameObject panelMejoras;
    // Selector de nivel del menú principal (ver AbrirNiveles/CerrarNiveles)
    // — a propósito NO respeta MetaProgreso.NivelDosDesbloqueado: los dos
    // niveles quedan siempre elegibles acá, es un atajo de testeo mientras
    // se sigue construyendo el juego. El desbloqueo narrativo real sigue
    // existiendo (MetaProgreso.NivelDosDesbloqueado/DesbloquearNivel2) y
    // sigue siendo lo único que se guarda/telemetra — esto es puramente
    // un acceso de conveniencia en el menú, no un cambio al progreso.
    public GameObject panelNiveles;
    public TMP_Text textoResultado;
    public TMP_Text textoRecordFinal;
    public TMP_Text textoEsenciaGanada;

    [Header("Nivel 2 — cutscenes de apertura (punto 3) y cierre (punto 5)")]
    // Caja de diálogo real (ver PantallaCutsceneNivel2.cs), compartida
    // entre las dos cutscenes de Nivel 2 (apertura y cierre) — nunca
    // corren a la vez, así que un solo panel/corrutina/flag de "saltar"
    // alcanza para las dos (ver enCutsceneCierre más abajo, que distingue
    // cuál está activa para SaltarCutscene()).
    public GameObject panelCutsceneNivel2;
    public TMP_Text textoDialogoCutscene;
    public GameObject botonSaltarCutscene;
    /// <summary>
    /// Fundido a negro entre Nivel 1 y la cutscene de Nivel 2 (Fase 1,
    /// punto 4 — "no corte duro"). raycastTarget queda en false (ver el
    /// setup): nunca debe tapar clicks en el botón Saltar mientras se
    /// desvanece. Fundir() es la única que lo toca.
    /// </summary>
    public Image imagenFundido;
    public float duracionFundido = 0.35f;
    public AdministradorSistema administradorPrefab;
    AdministradorSistema administradorActivo;
    Coroutine cutsceneCoroutine;
    bool saltandoCutscene;
    bool enCutsceneCierre;
    /// <summary>
    /// A propósito NUNCA persistida en PlayerPrefs (a diferencia de la vieja
    /// MetaProgreso.CutsceneAperturaVista) — pedido explícito: "que la
    /// escena sea skippeable más no automaticamente skippeable". Vive solo
    /// en memoria durante esta sesión de Unity: morir y reintentar (mismo
    /// GameManager, ver BotonReintentar) la deja en true y salta directo a
    /// la pelea, pero cerrar y volver a abrir el juego siempre muestra la
    /// cutscene de nuevo — nunca un salto silencioso basado en una partida
    /// vieja. LimpiarPartida() NO la resetea a propósito, para que
    /// funcione a través de un reintento real.
    /// </summary>
    bool cutsceneAperturaVistaSesion;
    /// <summary>La Forma Precisa instanciada al transformar el enjambre — null hasta que pasa el beat (e) de la cutscene de apertura, y de nuevo null tras el beat (d) de la de cierre (ver AsegurarFormaNivel3). Punto 4 la usa para dirigir los ataques del boss.</summary>
    public FormaPrecisa FormaPrecisaActiva => formaPrecisaActiva;
    FormaPrecisa formaPrecisaActiva;
    // Placeholder LIMPIO de la forma de Nivel 3 (punto 5) — a propósito
    // sin script ni comportamiento propio, un solo SpriteRenderer, para
    // que reemplazarlo cuando se construya Nivel 3 de verdad sea trivial.
    GameObject formaNivel3Activa;
    // Dorado cegador — nunca usado antes en el juego (rojo=láser,
    // violeta=hechizo/proyectiles) a propósito: pedido explícito, "una
    // escalada más grande que la transformación de apertura".
    static readonly Color ColorFinalNivel3 = new Color(2.6f, 2f, 0.9f);

    readonly HashSet<Nucleo> nucleosActivos = new HashSet<Nucleo>();
    // Cuántos Enemigo (homing + los 4 patrones de ráfaga, todos cuentan
    // igual) puede haber vivos al mismo tiempo — sin esto, un Fever
    // sostenido muchos segundos seguidos (medido: más de 2 minutos en una
    // partida real) acumulaba ráfagas nuevas más rápido de lo que
    // tiempoVidaMax (12s) alcanzaba a limpiar las viejas, con cientos de
    // Enemigo vivos a la vez — el lag real que reportó el usuario con
    // 200+ núcleos en Fever x5. Los spawns nuevos simplemente se saltean
    // mientras se esté en el tope, no hace falta destruir nada.
    readonly HashSet<Enemigo> enemigosActivos = new HashSet<Enemigo>();
    public int maxEnemigosActivos = 90;
    float tiempo;
    float record;
    int recordCombo;
    int recordNucleos;
    int comboMaximo;
    int nucleosMaximo;
    float tSpawnEnemigo;
    float tProximoOrbeVelocidad;
    float tProximoOrbeExplosivo;
    float tBoostVelocidad;
    float tBoostCrecimiento;
    int nivelFever = 1;
    float tFever;
    float tDecaimientoFever;
    int orbesActivos;
    Vector2 objetivo;
    bool usandoTeclado;
    Vector3 ultimaPosMouse;

    int comboOrbes;
    float tUltimoOrbe = -99f;
    int siguienteHito;

    public Vector2 Objetivo => objetivo;
    public float Tiempo => tiempo;

    /// <summary>
    /// Multiplicador de velocidad vigente para todos los núcleos (activo
    /// mientras dura el boost del orbe amarillo). Nucleo.cs lo lee cada
    /// frame, así que aplica por igual a los fragmentos que ya existían y
    /// a los que se creen mientras dura el boost.
    /// </summary>
    public float MultiplicadorVelocidadActual => tBoostVelocidad > 0f ? multiplicadorBoostVelocidad : 1f;

    /// <summary>Nivel de Fever actual (1 = sin Fever). No decae solo — solo sube dentro de la partida.</summary>
    public int NivelFever => nivelFever;

    void Awake()
    {
        Instancia = this;
        record = PlayerPrefs.GetFloat("enjambre_record", 0f);
        recordCombo = PlayerPrefs.GetInt("enjambre_record_combo", 0);
        recordNucleos = PlayerPrefs.GetInt("enjambre_record_nucleos", 0);
        MetaProgreso.Cargar();
        if (camara == null) camara = Camera.main;
        ultimaPosMouse = Input.mousePosition;
    }

    void Start() => MostrarMenu();

    void Update()
    {
        ActualizarHitStop();
        ActualizarLimitesDesdeCamara();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (estado == EstadoJuego.Jugando) PausarPartida();
            else if (estado == EstadoJuego.Pausado) ReanudarPartida();
        }

        if (estado != EstadoJuego.Jugando) return;

        tiempo += Time.deltaTime;
        LeerInput();
        ActualizarGridSeparacion();
        // Mientras se espera que el jugador entre al portal (ver
        // SecuenciaVictoria), el enjambre se sigue pudiendo mover (por eso
        // esto NO cambia `estado`) pero no debe seguir llenándose la
        // pantalla de cosas nuevas — solo estos spawns/eventos se frenan,
        // el resto del loop (Fever, boosts, HUD) sigue andando normal.
        if (!secuenciaVictoriaActiva)
        {
            if (modoNivel2)
            {
                // La pelea real (punto 4): reloj propio (tPelea, no
                // `tiempo` — ver comentario de arriba de duracionPeleaNivel2)
                // más el tick de balas pooled. Los patrones en sí los
                // dispara la corrutina PeleaNivel2(), no Update() — acá
                // solo se mueve/resuelve lo que ya está en el aire.
                if (PeleaActiva)
                {
                    // Autoridad del reloj: el audio real si ya está sonando
                    // (ver MusicManager.ReproduciendoClipNivel2 — corrige un
                    // desfasaje real de >1 beat contra Time.deltaTime, ver su
                    // comentario), deltaTime como fallback (Editor batch mode
                    // no reproduce audio de verdad, así que cae acá siempre
                    // durante los tests — ningún Verificar* que fuerza tPelea
                    // por reflection pasa por este branch).
                    var mm = MusicManager.Instancia;
                    tPelea = (mm != null && mm.ReproduciendoClipNivel2) ? mm.TiempoClipNivel2 : tPelea + Time.deltaTime;
                    ActualizarProyectiles();
                    ActualizarOrbesNivel2(Time.deltaTime);
                    ActualizarBarraVidaBossNivel2();
                }
            }
            else
            {
                ActualizarSpawnEnemigos();
                ActualizarSpawnCazadores();
                ActualizarSpawnCoagulos();
                ActualizarSpawnRivales();
                ActualizarSpawnOrbeVelocidad();
                ActualizarSpawnOrbeExplosivo();
                ActualizarEventoLaser();
            }
        }
        // Bug real (encontrado leyendo telemetría real de Nivel 2, punto
        // 10): estas tres son sistemas de Nivel 1 y corrían SIN gatear por
        // modoNivel2 — Fever seguía decayendo (y escribiendo fever_baja al
        // CSV con el reloj `tiempo`, no tPelea) durante toda la pelea del
        // boss, ensuciando exactamente los datos que este punto necesita
        // leer. No afecta gameplay de Nivel 2 (nada ahí lee nivelFever),
        // pero si no se corta acá el ruido nunca deja de crecer.
        if (!modoNivel2)
        {
            ActualizarHitosTiempo();
            if (tBoostVelocidad > 0f) tBoostVelocidad -= Time.deltaTime;
            if (tBoostCrecimiento > 0f) tBoostCrecimiento -= Time.deltaTime;
            ActualizarDecaimientoFever();
        }

        if (nucleosActivos.Count > nucleosMaximo) nucleosMaximo = nucleosActivos.Count;

        // La Forma Precisa no es un Nucleo — nucleosActivos se queda en 0
        // durante todo Nivel 2 a propósito (ver AsegurarFormaPrecisa), así
        // que ninguno de estos dos chequeos de Nivel 1 aplica ahí. Su
        // condición de derrota es FormaPrecisa.AlQuedarSinVidas (punto 4).
        if (modoNivel2)
        {
            // Nivel 1 tampoco tiene HUD propio todavía para Nivel 2 (hudRoot
            // se queda oculto desde EntrarCutsceneNivel2) — nada que actualizar acá.
        }
        else
        {
            if (hud != null) hud.Actualizar(tiempo, nucleosActivos.Count, record, nivelFever);
            if (nucleosActivos.Count == 0) TerminarPartida();
            else if (!victoriaDisparada && tiempo >= tiempoVictoria) DispararVictoria();
        }
    }

    // El área de juego tenía un mitadAncho/mitadAlto fijos (pensados para
    // un aspect ratio de referencia), así que con una ventana de Game más
    // ancha o más angosta quedaba una franja muerta a los costados en vez
    // de llenar la pantalla real. Los recalculamos desde la cámara
    // ortográfica cada frame para que siempre coincidan con lo visible.
    const float margenBordeMundo = 0.94f;

    void ActualizarLimitesDesdeCamara()
    {
        if (camara == null || !camara.orthographic) return;
        mitadAlto = camara.orthographicSize * margenBordeMundo;
        mitadAncho = camara.orthographicSize * camara.aspect * margenBordeMundo;
        mitadMundoAncho = mitadAncho * factorMundo;
        mitadMundoAlto = mitadAlto * factorMundo;
    }

    void LeerInput()
    {
        Vector2 dir = Vector2.zero;
        bool teclaPresionada = false;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) { dir.x -= 1; teclaPresionada = true; }
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) { dir.x += 1; teclaPresionada = true; }
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) { dir.y += 1; teclaPresionada = true; }
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) { dir.y -= 1; teclaPresionada = true; }

        Vector3 mousePosActual = Input.mousePosition;
        bool seMovioElMouse = (mousePosActual - ultimaPosMouse).sqrMagnitude > 0.01f;
        ultimaPosMouse = mousePosActual;

        if (seMovioElMouse || Input.GetMouseButton(0) || Input.touchCount > 0) usandoTeclado = false;
        else if (teclaPresionada) usandoTeclado = true;

        if (usandoTeclado)
        {
            if (dir.sqrMagnitude > 0.001f)
            {
                Vector2 cm = CentroDeMasa();
                objetivo = cm + dir.normalized * 2.2f;
            }
        }
        else
        {
            Vector3 mundo = camara.ScreenToWorldPoint(mousePosActual);
            objetivo = new Vector2(mundo.x, mundo.y);
        }
    }

    // El piso de intervalo y el techo de dificultad de abajo suben con
    // nivelFever: jugar muy bien (encadenar combos/Fevers) no debería ser
    // una espiral solo a favor del jugador.
    float IntervaloSpawnPiso => Mathf.Max(intervaloMinimoAbsoluto, intervaloEnemigoMinimo - (nivelFever - 1) * intervaloMenosPorNivelFever);
    float DificultadEnemigoTecho => dificultadEnemigoMax + (nivelFever - 1) * dificultadExtraPorNivelFever;

    // Escalón de escalada global actual (1 al empezar, sube de a uno cada
    // intervaloEscaladaGlobal segundos, tope nivelEscaladaMax) y sus dos
    // multiplicadores: uno que endurece a los enemigos, otro que acorta
    // los intervalos de spawn — ambos aplican por igual al homing normal
    // y a los 4 patrones de ráfaga de Fever.
    int NivelEscaladaActual => 1 + Mathf.Min(nivelEscaladaMax - 1, Mathf.FloorToInt(tiempo / intervaloEscaladaGlobal));
    float MultiplicadorDificultadEscalada => 1f + (NivelEscaladaActual - 1) * multiplicadorDificultadPorEscalada;
    float MultiplicadorIntervaloEscalada => Mathf.Pow(multiplicadorIntervaloPorEscalada, NivelEscaladaActual - 1);

    // Escalada por TAMAÑO DEL ENJAMBRE — la escalada de arriba solo mira
    // el reloj, así que un enjambre que creció mucho (partirse solo te da
    // más fragmentos, nunca te mata directamente) terminaba siendo
    // invencible por diseño: nada en el juego castigaba tener muchos
    // núcleos. Confirmado con datos reales de DOS partidas (telemetría):
    // la primera llegó a 223 núcleos sosteniendo Fever x5 sin cortarse
    // más de 2 minutos. Con el primer ajuste (tope en 150) el usuario
    // jugó de nuevo y esta vez con apenas ~45-60 núcleos (a los 80s) ya
    // se sintió "a salvo" — muy por debajo de donde esta curva llegaba a
    // su techo. Bajado nucleosParaEscaladaMaximaPorTamano de 150 a 55
    // (el techo real llega justo donde el juego empezaba a sentirse
    // trivial) y subido el techo mismo de 2.5x a 3x. Esto sube la
    // dificultad/velocidad real de los enemigos (no cuántos aparecen —
    // eso empeoraría el lag con enjambres grandes, ver
    // maxEnemigosActivos) a medida que el enjambre crece.
    public int nucleosDondeEmpiezaEscaladaPorTamano = 8;
    public int nucleosParaEscaladaMaximaPorTamano = 55;
    public float multiplicadorMaximoPorNucleos = 3f;

    float MultiplicadorDificultadPorNucleos
    {
        get
        {
            int n = nucleosActivos.Count;
            if (n <= nucleosDondeEmpiezaEscaladaPorTamano) return 1f;
            float t = Mathf.Clamp01((n - nucleosDondeEmpiezaEscaladaPorTamano) / (float)(nucleosParaEscaladaMaximaPorTamano - nucleosDondeEmpiezaEscaladaPorTamano));
            return Mathf.Lerp(1f, multiplicadorMaximoPorNucleos, t);
        }
    }

    // Dificultad real que se le pasa a Enemigo.Inicializar() — la comparten
    // el homing normal y los 3 tipos nuevos (Cazador/Coágulo/Rival), para
    // que la escalada global Y la escalada por tamaño les peguen a todos
    // por igual.
    float DificultadEnemigoActual =>
        Mathf.Min(DificultadEnemigoTecho, 1f + tiempo / dificultadEnemigoRampa) * MultiplicadorDificultadEscalada * MultiplicadorDificultadPorNucleos;

    // Cuántas gemas debería haber dando vueltas en total con el nivel de
    // Fever actual — como nivelFever ahora sube y baja solo, esto también
    // sube y baja con él en vez de acumular gemas extra para siempre. La
    // mejora "gemas" (MetaProgreso) suma encima, no reemplaza el tuneable.
    int ObjetivoOrbesEfectivo => Mathf.RoundToInt(MetaProgreso.ValorEfectivo("gemas", orbesObjetivo, 1f, orbesObjetivo + MetaProgreso.NivelMaximo))
        + (nivelFever - 1) * orbesExtraPorNivelFever;

    // Las 3 mejoras permanentes restantes (ver MetaProgreso/PantallaMejoras):
    // suman sobre el valor tuneable del Inspector, nunca lo sobreescriben —
    // así una compra no deja un valor "stale" pisando el default de diseño.
    float RadioInicialEfectivo => MetaProgreso.ValorEfectivo("nucleo", radioInicial, 0.05f, 0.75f);
    float CrecimientoPorOrbeEfectivo => MetaProgreso.ValorEfectivo("crecimiento", crecimientoPorOrbe, 0.01f, 0.10f);
    float DuracionFeverEfectivo => MetaProgreso.ValorEfectivo("fever", duracionFever, 2f, 25f);

    // Mientras tFever > 0 el nivel se mantiene (y el cronómetro de
    // decaimiento se mantiene recargado); al agotarse, el nivel baja de a
    // uno cada intervaloDecaimientoFever segundos hasta volver a 1 (Fever
    // apagado). No hace falta destruir gemas ya generadas de más: al bajar
    // el objetivo efectivo, ProcesarPickupOrbe simplemente deja de
    // reponerlas hasta que el total se acomode solo con el juego normal.
    void ActualizarDecaimientoFever()
    {
        if (tFever > 0f)
        {
            tFever -= Time.deltaTime;
            tDecaimientoFever = intervaloDecaimientoFever;
        }
        else if (nivelFever > 1)
        {
            tDecaimientoFever -= Time.deltaTime;
            if (tDecaimientoFever <= 0f)
            {
                nivelFever--;
                tDecaimientoFever = intervaloDecaimientoFever;
                Telemetria.Registrar(tiempo, "fever_baja", nivelFever, comboOrbes, nucleosActivos.Count, orbesActivos);
            }
        }
    }

    void ActualizarSpawnEnemigos()
    {
        // Antes el homing normal se pausaba del todo en Fever (los
        // patrones de ráfaga lo reemplazaban). Ahora no hay patrones que
        // lo reemplacen — el homing sigue siempre activo (su propio
        // intervalo ya se acorta con nivelFever vía IntervaloSpawnPiso) y
        // Cazador/Coágulo/Rival se SUMAN encima, no lo reemplazan: más
        // densidad y variedad simultánea es la dificultad real de un
        // survivor-like, no un patrón a leer.
        tSpawnEnemigo += Time.deltaTime;
        float intervalo = Mathf.Max(IntervaloSpawnPiso, intervaloEnemigoInicial - tiempo * rampaDificultad) * MultiplicadorIntervaloEscalada;
        if (tSpawnEnemigo >= intervalo)
        {
            tSpawnEnemigo = 0f;
            // Se reinicia el cronómetro igual aunque no haya lugar, así no
            // se acumula un "atraso" que dispare un montón de golpe apenas
            // se libera espacio.
            if (HayLugarParaMasEnemigos) StartCoroutine(TelegraphYNacer(PuntoAleatorioDeBorde()));
        }
    }

    // Cazador: en vez de perseguir el centro de masa, persigue a un núcleo
    // específico elegido al nacer — el más aislado del enjambre (ver
    // NucleoMasAislado) — como un depredador real cazando al rezagado.
    // Aparece desde Fever x2.
    void ActualizarSpawnCazadores()
    {
        if (nivelFever < 2) return;
        tSpawnCazador -= Time.deltaTime;
        if (tSpawnCazador > 0f) return;
        tSpawnCazador = Mathf.Max(intervaloMinimoAbsoluto, intervaloCazador * MultiplicadorIntervaloEscalada);
        if (HayLugarParaMasEnemigos) StartCoroutine(TelegraphYNacerCazador(PuntoAleatorioDeBorde()));
    }

    IEnumerator TelegraphYNacerCazador(Vector2 punto)
    {
        EfectosVisuales.Instancia?.Telegraph(punto, telegraphDuracion);
        yield return new WaitForSeconds(telegraphDuracion);
        if (estado != EstadoJuego.Jugando) yield break;
        var objetivo = NucleoMasAislado();
        var e = Instantiate(enemigoPrefab, punto, Quaternion.identity);
        e.Inicializar(DificultadEnemigoActual);
        e.ConfigurarCazador(objetivo);
    }

    // Coágulo: obstáculo grande e inmóvil, no persigue — obliga a rodearlo
    // (decisión de ruta, no reflejo de esquive). No depende del Fever
    // (sube y baja solo); depende del tiempo real de partida, como un
    // "relleno" progresivo del área jugable. Tope bajo de simultáneos:
    // son puntos de referencia del mapa, no parte de la horda densa.
    void ActualizarSpawnCoagulos()
    {
        tSpawnCoagulo -= Time.deltaTime;
        if (tSpawnCoagulo > 0f) return;
        tSpawnCoagulo = Mathf.Max(intervaloMinimoAbsoluto, intervaloCoagulo * MultiplicadorIntervaloEscalada);
        int activos = enemigosActivos.Count(e => e.tipoAtaque == Enemigo.TipoAtaque.Estatico);
        if (activos < maxCoagulosSimultaneos && HayLugarParaMasEnemigos) SpawnCoagulo();
    }

    void SpawnCoagulo()
    {
        // Dentro de lo que la cámara alcanza a ver (mitadAncho/Alto), no
        // en cualquier punto del mundo entero — si no, podría aparecer
        // bien lejos de donde el jugador realmente está mirando.
        Vector2 centro = CentroDeMasa();
        Vector2 pos = centro + new Vector2(Random.Range(-mitadAncho, mitadAncho), Random.Range(-mitadAlto, mitadAlto)) * 0.85f;
        var e = Instantiate(enemigoPrefab, pos, Quaternion.identity);
        e.Inicializar(DificultadEnemigoActual);
        e.ConfigurarEstatico();
    }

    // Enjambre Rival: en vez de perseguir al jugador, persigue la gema más
    // cercana y la roba al tocarla — compite por el mismo recurso, no
    // ataca directo. Nace en clusters chicos. Aparece desde Fever x4.
    void ActualizarSpawnRivales()
    {
        if (nivelFever < 4) return;
        tSpawnRival -= Time.deltaTime;
        if (tSpawnRival > 0f) return;
        tSpawnRival = Mathf.Max(intervaloMinimoAbsoluto, intervaloRival * MultiplicadorIntervaloEscalada);
        if (HayLugarParaMasEnemigos) StartCoroutine(TelegraphYNacerRivales(PuntoAleatorioDeBorde()));
    }

    IEnumerator TelegraphYNacerRivales(Vector2 punto)
    {
        EfectosVisuales.Instancia?.Telegraph(punto, telegraphDuracion);
        yield return new WaitForSeconds(telegraphDuracion);
        if (estado != EstadoJuego.Jugando) yield break;
        int cantidad = Random.Range(3, rivalesPorCluster + 1);
        for (int i = 0; i < cantidad; i++)
        {
            if (!HayLugarParaMasEnemigos) break;
            Vector2 pos = punto + Random.insideUnitCircle * 0.6f;
            var e = Instantiate(enemigoPrefab, pos, Quaternion.identity);
            e.Inicializar(DificultadEnemigoActual);
            e.ConfigurarRival();
        }
    }

    // Reloj aparte del Fever/combo (ver header arriba): cada tanto tiempo,
    // 4 paredes desde los bordes del área visible, dejando una zona segura
    // en el medio. Cada pared telegrafea (tenue) y recién después resuelve
    // un único golpe de área (no daño continuo — así un núcleo recién
    // partido por la misma pared no puede volver a activarla en el mismo
    // instante).
    void ActualizarEventoLaser()
    {
        tProximoEventoLaser -= Time.deltaTime;
        if (tProximoEventoLaser > 0f) return;
        tProximoEventoLaser = intervaloEventoLaser * MultiplicadorIntervaloEscalada;
        RafagaMurallaLaser();
    }

    void RafagaMurallaLaser()
    {
        Vector2 posJugador = CentroDeMasa();

        // OJO: esto centraba la zona segura en CentroDeMasa() — la
        // posición ACTUAL del jugador. Un jugador quieto queda siempre
        // "en el medio de su propia zona segura" por construcción, sin
        // importar dónde esté parado: el evento no podía amenazar a nadie
        // que no se moviera (confirmado jugando: "me quedé quieto... para
        // terminar la run"). El centro de la zona segura ahora se
        // desplaza en una de las 4 direcciones cardinales desde donde
        // estás parado — moverse de verdad pasa a ser obligatorio.
        //
        // El desplazamiento tiene que ganarle al medio-ancho/alto de la
        // zona segura (mitadAncho/Alto * anchoZonaSeguraLaser, hasta *0.6
        // más chico todavía con un enjambre grande) para garantizar que tu
        // posición actual quede AFUERA de la zona ya movida — 0.75 le gana
        // cómodo al 0.65 (o menos) de la zona segura en cualquier caso.
        bool horizontal = Random.value < 0.5f;
        float signo = Random.value < 0.5f ? 1f : -1f;
        Vector2 direccionOffset = horizontal ? new Vector2(signo, 0f) : new Vector2(0f, signo);
        float distanciaOffset = (horizontal ? mitadAncho : mitadAlto) * 0.75f;
        Vector2 centro = posJugador + direccionOffset * distanciaOffset;

        EfectosVisuales.Instancia?.Popup(posJugador, "¡MURALLA LÁSER!", ColorLaser, 1.9f);
        BeepSynth.Instancia?.Beep(70f, 0.25f, BeepSynth.Onda.Sierra, 0.24f);
        Telemetria.Registrar(tiempo, "laser_evento", nivelFever, comboOrbes, nucleosActivos.Count, orbesActivos);

        // Con FactorEscalaPorCantidad, un enjambre grande queda tan
        // empaquetado que entraba casi entero en la zona segura sin
        // esfuerzo (medido: 1 golpe en 9 disparos con 200+ núcleos) — la
        // zona segura también se achica con la cantidad, así que un
        // enjambre grande necesita apuntar mejor, no solo "estar ahí".
        float factorZonaSegura = Mathf.Lerp(1f, 0.6f, Mathf.InverseLerp(1f, multiplicadorMaximoPorNucleos, MultiplicadorDificultadPorNucleos));
        float anchoSeguro = mitadAncho * anchoZonaSeguraLaser * factorZonaSegura;
        float altoSeguro = mitadAlto * anchoZonaSeguraLaser * factorZonaSegura;
        // 1.2 alcanzaba cuando la zona segura estaba centrada en el
        // jugador — pero ahora se desplaza hasta 0.75*mitadAncho/Alto (ver
        // arriba), y las 4 paredes salen desde `centro`, no desde
        // posJugador. En el peor caso (desplazamiento hacia un lado), la
        // pared del lado OPUESTO solo llegaba a 1.2-0.75=0.45 más allá del
        // jugador — bien adentro del área visible real, dejando un hueco
        // sin cubrir del otro lado del mapa (encontrado por
        // VerificarMurallaLaser.cs, que falla ~50% de las corridas según
        // hacia dónde cae el desplazamiento al azar). 2.0 asegura al menos
        // 2.0-0.75=1.25 de alcance en el peor caso, más allá de toda el
        // área que la cámara llega a mostrar.
        const float extension = 2.0f;

        CrearMuroLaser(new Rect(centro.x - mitadAncho * extension, centro.y - mitadAlto * extension,
            mitadAncho * extension - anchoSeguro, mitadAlto * extension * 2f)); // pared izquierda
        CrearMuroLaser(new Rect(centro.x + anchoSeguro, centro.y - mitadAlto * extension,
            mitadAncho * extension - anchoSeguro, mitadAlto * extension * 2f)); // pared derecha
        CrearMuroLaser(new Rect(centro.x - mitadAncho * extension, centro.y + altoSeguro,
            mitadAncho * extension * 2f, mitadAlto * extension - altoSeguro)); // pared arriba
        CrearMuroLaser(new Rect(centro.x - mitadAncho * extension, centro.y - mitadAlto * extension,
            mitadAncho * extension * 2f, mitadAlto * extension - altoSeguro)); // pared abajo
    }

    void CrearMuroLaser(Rect areaMundo) => CrearMuroLaser(areaMundo, telegraphLaser, duracionActivoLaser);

    /// <summary>Pared rotada (revisión — ver el comentario de LaserHazard sobre por qué es un método nuevo, no una sobrecarga del de arriba con un parámetro más). `tamano` es (ancho, largo) ANTES de rotar.</summary>
    void CrearMuroLaserRotado(Vector2 centro, Vector2 tamano, float anguloGrados, Color color, float telegraph, float duracionActivo)
    {
        var go = new GameObject("LaserHazardRotado");
        go.AddComponent<LaserHazard>().Configurar(centro, tamano, anguloGrados, telegraph, duracionActivo, color);
    }

    void CrearMuroLaser(Rect areaMundo, float telegraph, float duracionActivo)
    {
        var go = new GameObject("LaserHazard");
        go.AddComponent<LaserHazard>().Configurar(areaMundo, telegraph, duracionActivo, ColorLaser);
    }

    // ============= NIVEL 2 — FAMILIA "LÁSER TELEGRAFIADO" (pelea) ========
    // 4 patrones originales (del viejo Show de Láseres, ver comentario del
    // header arriba), todos armados con el mismo CrearMuroLaser de arriba
    // (Rects axis-aligned con telegraph + un único chequeo de impacto) —
    // no hace falta geometría nueva, solo variar cuántos rects, en qué
    // posición y con qué timing. Todos usan mitadAncho/Alto (lo que la
    // cámara alcanza a ver) como escala, igual que el resto de los
    // eventos de este archivo. El temporizado real (cuándo se dispara
    // cada uno) vive en PeleaNivel2()/DispararPatronDeFase, no acá.

    // Patrón 1 — Barrido simple: un único rayo fino, horizontal o
    // vertical, en una posición al azar. El más fácil de los 4, para que
    // el show empiece de a poco.
    void PatronBarridoSimple()
    {
        Vector2 centro = CentroDeMasa();
        bool horizontal = Random.value < 0.5f;
        float ancho = (horizontal ? mitadAlto : mitadAncho) * anchoRayoShowLaser;
        const float extension = 1.3f;
        Rect area = horizontal
            ? new Rect(centro.x - mitadAncho * extension, centro.y + Random.Range(-mitadAlto * 0.85f, mitadAlto * 0.85f) - ancho * 0.5f, mitadAncho * extension * 2f, ancho)
            : new Rect(centro.x + Random.Range(-mitadAncho * 0.85f, mitadAncho * 0.85f) - ancho * 0.5f, centro.y - mitadAlto * extension, ancho, mitadAlto * extension * 2f);
        CrearMuroLaser(area, telegraphShowLaser, duracionActivoShowLaser);
    }

    // Patrón 2 — Cruz: dos rayos perpendiculares que se cruzan en un
    // punto al azar cerca del enjambre. Divide la pantalla en 4 cuadrantes
    // seguros — hay que elegir uno y quedarse ahí, no solo esquivar una
    // línea.
    void PatronCruz()
    {
        Vector2 centro = CentroDeMasa();
        Vector2 punto = centro + new Vector2(Random.Range(-mitadAncho * 0.6f, mitadAncho * 0.6f), Random.Range(-mitadAlto * 0.6f, mitadAlto * 0.6f));
        float anchoH = mitadAlto * anchoRayoShowLaser;
        float anchoV = mitadAncho * anchoRayoShowLaser;
        const float extension = 1.3f;
        CrearMuroLaser(new Rect(centro.x - mitadAncho * extension, punto.y - anchoH * 0.5f, mitadAncho * extension * 2f, anchoH), telegraphShowLaser, duracionActivoShowLaser);
        CrearMuroLaser(new Rect(punto.x - anchoV * 0.5f, centro.y - mitadAlto * extension, anchoV, mitadAlto * extension * 2f), telegraphShowLaser, duracionActivoShowLaser);
    }

    // Patrón 3 — Corredor: dos paredes paralelas con un hueco angosto
    // entre ellas, desplazado del centro — un pasillo real que cruzar, no
    // solo "no estar en la línea".
    void PatronCorredor()
    {
        Vector2 centro = CentroDeMasa();
        bool horizontal = Random.value < 0.5f;
        float huecoAncho = (horizontal ? mitadAlto : mitadAncho) * 0.4f;
        float offset = Random.Range(-0.3f, 0.3f) * (horizontal ? mitadAlto : mitadAncho);
        const float extension = 1.3f;
        const float grosor = 0.5f;
        if (horizontal)
        {
            float huecoY = centro.y + offset;
            CrearMuroLaser(new Rect(centro.x - mitadAncho * extension, huecoY - huecoAncho * 0.5f - grosor, mitadAncho * extension * 2f, grosor), telegraphShowLaser, duracionActivoShowLaser);
            CrearMuroLaser(new Rect(centro.x - mitadAncho * extension, huecoY + huecoAncho * 0.5f, mitadAncho * extension * 2f, grosor), telegraphShowLaser, duracionActivoShowLaser);
        }
        else
        {
            float huecoX = centro.x + offset;
            CrearMuroLaser(new Rect(huecoX - huecoAncho * 0.5f - grosor, centro.y - mitadAlto * extension, grosor, mitadAlto * extension * 2f), telegraphShowLaser, duracionActivoShowLaser);
            CrearMuroLaser(new Rect(huecoX + huecoAncho * 0.5f, centro.y - mitadAlto * extension, grosor, mitadAlto * extension * 2f), telegraphShowLaser, duracionActivoShowLaser);
        }
    }

    // Patrón 4 — Abanico: 3 rayos paralelos disparados en secuencia
    // (no todos juntos), barriendo la pantalla de un lado al otro — obliga
    // a seguir reposicionándose durante ~0.7s en vez de esquivar una vez y
    // ya.
    void PatronAbanico() => StartCoroutine(SecuenciaAbanico(CentroDeMasa()));

    IEnumerator SecuenciaAbanico(Vector2 centro)
    {
        bool horizontal = Random.value < 0.5f;
        const int cantidad = 3;
        float ancho = (horizontal ? mitadAlto : mitadAncho) * anchoRayoShowLaser;
        const float extension = 1.3f;
        for (int i = 0; i < cantidad; i++)
        {
            float t = i / (float)(cantidad - 1);
            float offset = Mathf.Lerp(-0.7f, 0.7f, t) * (horizontal ? mitadAlto : mitadAncho);
            Rect area = horizontal
                ? new Rect(centro.x - mitadAncho * extension, centro.y + offset - ancho * 0.5f, mitadAncho * extension * 2f, ancho)
                : new Rect(centro.x + offset - ancho * 0.5f, centro.y - mitadAlto * extension, ancho, mitadAlto * extension * 2f);
            CrearMuroLaser(area, telegraphShowLaser, duracionActivoShowLaser);
            yield return new WaitForSeconds(0.35f);
        }
    }
    // ============ FIN NIVEL 2 — FAMILIA "LÁSER TELEGRAFIADO" ============

    // ===================== NIVEL 2 — PROYECTILES (pool) ==================
    // Pool simple (Stack de inactivas + List de activas) para las balas de
    // Anillo/Espiral/Disparo Dirigido — con varias decenas activas a la
    // vez durante buena parte de la pelea, Instantiate/Destroy por cada
    // una sería el mismo problema de rendimiento que ya se encontró y
    // arregló con los Núcleo (ver margenSeparacionNucleos arriba). Movimiento
    // y chequeo de impacto van en un único loop (ActualizarProyectiles,
    // llamado desde Update()) en vez de que cada bala tenga su propio
    // Update() — mismo motivo.
    static readonly Color ColorProyectil = new Color(2f, 0.4f, 2.3f); // HDR — mismo violeta del hechizo de la cutscene
    readonly List<Proyectil> proyectilesActivos = new List<Proyectil>();
    readonly Stack<Proyectil> poolProyectiles = new Stack<Proyectil>();
    public int ProyectilesActivosCount => proyectilesActivos.Count;

    public void DispararProyectil(Vector2 pos, Vector2 velocidad, Color color, float radioVisual = 0.09f, float radioHitbox = 0.07f, float velocidadAngular = 0f)
    {
        Proyectil p = poolProyectiles.Count > 0 ? poolProyectiles.Pop() : null;
        if (p == null) p = new GameObject("Proyectil").AddComponent<Proyectil>();
        p.gameObject.SetActive(true);
        p.Configurar(pos, velocidad, color, radioVisual, radioHitbox, velocidadAngular);
        proyectilesActivos.Add(p);
    }

    /// <summary>Rota un vector `grados` en sentido antihorario — usado para las balas curvas de PatronFlorGiratoria (ver Proyectil.velocidadAngular).</summary>
    static Vector2 RotarGrados(Vector2 v, float grados)
    {
        float rad = grados * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    void DevolverProyectil(int indice)
    {
        var p = proyectilesActivos[indice];
        proyectilesActivos.RemoveAt(indice);
        p.gameObject.SetActive(false);
        poolProyectiles.Push(p);
    }

    void LimpiarProyectiles()
    {
        for (int i = proyectilesActivos.Count - 1; i >= 0; i--) DevolverProyectil(i);
    }

    /// <summary>Mueve y resuelve colisión/despawn de todas las balas activas — un solo loop, mismo lenguaje que ActualizarGridSeparacion.</summary>
    void ActualizarProyectiles()
    {
        float dt = Time.deltaTime;
        float limiteX = mitadAncho * 1.4f, limiteY = mitadAlto * 1.4f;
        for (int i = proyectilesActivos.Count - 1; i >= 0; i--)
        {
            var p = proyectilesActivos[i];
            if (p.velocidadAngular != 0f) p.velocidad = RotarGrados(p.velocidad, p.velocidadAngular * dt);
            Vector2 pos = (Vector2)p.transform.position + p.velocidad * dt;
            p.transform.position = pos;

            if (Mathf.Abs(pos.x) > limiteX || Mathf.Abs(pos.y) > limiteY) { DevolverProyectil(i); continue; }

            if (formaPrecisaActiva != null && Vector2.Distance(pos, formaPrecisaActiva.transform.position) < p.radioHitbox + formaPrecisaActiva.radioHitbox)
            {
                formaPrecisaActiva.RecibirGolpe($"proyectil_fase{EscaladaPatronesFase1(tPelea)}");
                DevolverProyectil(i);
            }
        }
    }
    // ================= FIN NIVEL 2 — PROYECTILES (pool) ==================

    // ===================== NIVEL 2 — PELEA (punto 4) =====================
    // Reutiliza los 4 patrones de pared-láser de arriba como familia
    // "láser telegrafiado" y suma 5 patrones con el pool de arriba: anillo
    // expansivo, espiral, espiral doble contra-rotante, flor giratoria
    // (balas curvas, ver Proyectil.velocidadAngular) y disparo dirigido.
    // Pedido explícito: "muy difícil... patrones creativos... que
    // tengas que tener habilidad" — reescrita en 5 fases de dificultad,
    // cada vez más densas y con menos aire entre patrones (ver
    // EsperaEntrePatrones); a partir de fase 3 el hueco entre patrones
    // puede ser MENOR que lo que tarda en resolverse el anterior — se
    // superponen de verdad, no uno a la vez con pausa. Sigue siendo justo
    // por construcción (todo patrón individual deja huecos navegables,
    // con telegraph antes de doler) pero exige leer varios patrones a la
    // vez y moverse todo el tiempo, no memorizar un ataque por vez.
    // Corredor (el más lento de leer) sale del pool desde fase 4 para no
    // volverse injusto, no para bajarle la dificultad.
    //
    // FASE 6 del rediseño grande (música + sincronía): estas 5 fases de
    // DIFICULTAD (ver EscaladaPatronesFase1) son un eje distinto de las 5
    // fases del MAPA de la canción (ver FaseEnTiempo) — antes eran el
    // mismo número porque toda la pelea (0-104.4s, AI Malware.mp3) era
    // "solo patrones". Ahora la pelea dura 128s (Industrial Planet.mp3,
    // 117.5 BPM) y solo los primeros duracionFase1Nivel2=80s tienen
    // patrones/orbes/pulso — de ahí en más es la escalada de privilegios
    // (punto 7) y la Fase 2 Super Administrador, solo láseres puros, sin
    // vida que restarle (punto 8, todavía sin construir).
    [Header("Nivel 2 — pelea (punto 4) y estructura de canción (Fase 6)")]
    public float duracionPeleaNivel2 = 128f;
    public float graciaInicialPelea = 5f;
    // Estructura energética real de Industrial Planet (pedido explícito) —
    // ver FaseEnTiempo. duracionFase1Nivel2 es el límite real que usa
    // PeleaNivel2() para cortar el loop de patrones: de acá para abajo
    // vive TODO el sistema de Fase 3/4/5 (orbes, energía, pulsos, vida del
    // boss); de acá para arriba, silencio hasta que existan los puntos
    // 7/8 (no se dispara nada — evita construir contenido que esos dos
    // puntos van a pisar de todos modos).
    public float duracionBuildUpNivel2 = 32f;
    public float duracionFase1Nivel2 = 80f;
    public float duracionBreakdownNivel2 = 88f;
    public float duracionRebuildNivel2 = 112f;
    // Sincronía (pedido explícito, 117.5 BPM): 60/117.5=0.5106s por beat,
    // *4 por compás, *8 compases por frase. EscaladaPatronesFase1 ya
    // cuantiza los cambios de fase de dificultad a duracionFraseNivel2;
    // el disparo de balas individuales a beats/medios beats queda para
    // cuando el punto 8/9 reescriba los patrones (fuera del alcance de
    // este punto — acá se sienta la base de sincronía, no cada disparo).
    // Valores tal cual los dio el pedido (no recalculados a partir de los
    // otros — evita que un redondeo en cascada los desalinee entre sí).
    public const float duracionBeatNivel2 = 0.511f;
    public const float duracionCompasNivel2 = 2.043f;
    public const float duracionFraseNivel2 = 16.35f;
    // Reloj PROPIO de la pelea, separado de `tiempo` — que arrastra
    // 105s+ si se entra por el portal de Victoria (el enjambre real
    // seguía vivo) pero arranca en 0 si se entra por el atajo de menú
    // (LimpiarPartida corre antes). Medir la pelea con `tiempo` hacía que
    // la MISMA pelea durara distinto por cada camino, y fijaba la
    // escalada de Intensidad al tope desde el primer frame en el camino
    // del portal. tPelea siempre arranca en 0 al empezar la pelea de
    // verdad (ver IniciarPeleaNivel2), sin importar el camino.
    float tPelea;
    public float TiempoPelea => tPelea;
    public bool PeleaActiva { get; private set; }

    /// <summary>
    /// El mapa de LA CANCIÓN en 5 tramos (pedido explícito, Fase 6): 1
    /// build-up, 2 cuerpo de Fase 1 (energía alta), 3 breakdown (la
    /// escalada de privilegios, punto 7), 4 rebuild de Fase 2, 5 clímax
    /// final. Pura, sin corrutina — testeable en un loop síncrono (ver
    /// VerificarPeleaNivel2.cs). NO es la dificultad de los patrones (ver
    /// EscaladaPatronesFase1, un eje distinto).
    /// </summary>
    public int FaseEnTiempo(float t)
    {
        if (t < duracionBuildUpNivel2) return 1;
        if (t < duracionFase1Nivel2) return 2;
        if (t < duracionBreakdownNivel2) return 3;
        if (t < duracionRebuildNivel2) return 4;
        return 5;
    }

    /// <summary>
    /// Dificultad de patrones DENTRO del cuerpo de Fase 1 (0 a
    /// duracionFase1Nivel2) — el eje que de verdad usan
    /// DispararPatronDeFase/EsperaEntrePatrones. Cuantizada a límites de
    /// frase (duracionFraseNivel2 ≈ 16.35s, pedido explícito: "alineá los
    /// cambios de patrón a límites de frase") en vez de a números sueltos
    /// — con 80s de cuerpo de Fase 1 alcanzan ~5 frases completas, así que
    /// esto sigue devolviendo 1-5 igual que antes, ahora enganchado al
    /// ritmo real de la canción en vez de a umbrales arbitrarios.
    /// </summary>
    public int EscaladaPatronesFase1(float t)
    {
        int frase = Mathf.FloorToInt(t / duracionFraseNivel2);
        return Mathf.Clamp(frase + 1, 1, 5);
    }

    Vector2 PosicionOrigenBoss() => administradorActivo != null ? (Vector2)administradorActivo.transform.position : new Vector2(0f, mitadAlto * 0.6f);

    void IniciarPeleaNivel2()
    {
        tPelea = 0f;
        PeleaActiva = true;
        MusicManager.Instancia?.ReproducirNivel2();
        if (formaPrecisaActiva != null) formaPrecisaActiva.AlQuedarSinVidas += ManejarDerrotaNivel2;
        Telemetria.Registrar(0f, "pelea_nivel2_inicio", nivelFever, comboOrbes, 0, orbesActivos);

        energiaNivel2 = 0f;
        tInicioCargaNivel2 = 0f;
        tCooldownPulsoPotenteRestanteNivel2 = 0f;
        formaPrecisaActiva?.ActualizarCargaEnergia(0f);
        for (int i = 0; i < orbesObjetivoNivel2; i++) GenerarOrbeNivel2();

        // Vida real del boss, fijada al máximo YA — la barra sube de a
        // poco (PresentacionVidaBossNivel2) por espectáculo, no porque el
        // número real tarde en estar listo (ver comentario del header de
        // sección VIDA DEL BOSS).
        vidaBossNivel2 = vidaBossMaxNivel2;
        bossColapsadoNivel2 = false;
        StartCoroutine(PresentacionVidaBossNivel2());

        StartCoroutine(PeleaNivel2());
    }

    // =============== NIVEL 2 — ORBES Y BARRA DE ENERGÍA (Fase 3) ==================
    // Reusa el prefab de Orbe de Nivel 1, pero con reglas propias: acá el
    // objetivo NO es alimentar combos, es obligar a moverse por TODA la
    // arena (pedido explícito) — por eso son escasos y se reponen lejos de
    // donde está parado el jugador, en vez de cerca del último pickup como
    // hace Nivel 1. energiaNivel2 sube con cada orbe recolectado y, al
    // llenarse, libera un pulso — por ahora sin dañar al boss de verdad
    // (eso llega en el punto 4, cuando exista una vida que restarle).
    [Header("Nivel 2 — orbes escasos + barra de energía (Fase 3, sin daño todavía)")]
    // 3 activos a la vez: lo bastante escaso para que nunca sobren en la
    // esquina segura, lo bastante numeroso para que siempre haya al menos
    // uno visible por el que arriesgarse — con 1 solo, un mal patrón entre
    // el jugador y el orbe lo dejaría inalcanzable por un rato largo.
    public int orbesObjetivoNivel2 = 3;
    // Imán a corta distancia (pedido explícito: "está esquivando, no se le
    // puede exigir precisión milimétrica") — bastante más chico que el
    // radio de recolección real de Nivel 1 (orbe ahí no tiene imán propio,
    // solo colisiona), a propósito: es una ayuda de últimos centímetros,
    // no un rango que reemplace tener que acercarse de verdad.
    public float radioImanNivel2 = 1.1f;
    public float velocidadImanNivel2 = 5.5f;
    public float radioRecoleccionNivel2 = 0.16f;
    // Se llena con POCOS orbes (pedido explícito): 5 cargas por pulso.
    // Con 3 orbes activos a la vez y reposición inmediata, encadenar los 5
    // toma varios trayectos reales por la arena — ni instantáneo (1-2
    // orbes) ni una expedición larga (10+). El punto 5 (combo → ataque
    // potente) mide qué tan RÁPIDO se junta esta misma carga, no cambia
    // este número.
    public float cargaPorOrbeNivel2 = 20f;
    public float energiaMaxNivel2 = 100f;
    float energiaNivel2;
    public float FraccionEnergiaNivel2 => energiaMaxNivel2 > 0f ? Mathf.Clamp01(energiaNivel2 / energiaMaxNivel2) : 0f;

    /// <summary>dt explícito (no Time.deltaTime adentro) — mismo motivo que FormaPrecisa.Mover/AdministradorSistema.Mover: en Editor batch mode Time.deltaTime no es controlable, así que la parte que sí depende del tiempo queda testeable con un dt sintético (ver VerificarOrbesEnergiaNivel2.cs).</summary>
    void ActualizarOrbesNivel2(float dt)
    {
        if (tCooldownPulsoPotenteRestanteNivel2 > 0f) tCooldownPulsoPotenteRestanteNivel2 -= dt;
        // Fase 7/8: sin orbes/pulsos una vez que arranca la escalada de
        // privilegios ni durante la Fase 2 — "con privilegios elevados, el
        // virus no puede tocarlo" (pedido explícito). AsegurarSuperAdministradorNivel2
        // ya destruye los orbes que quedaban vivos al transformar; esto
        // evita que se repongan durante el tramo silencioso hasta el final
        // de la canción.
        if (enEscaladaPrivilegiosNivel2 || superAdministradorActivo) return;
        if (formaPrecisaActiva == null) return;
        Vector2 posJugador = formaPrecisaActiva.transform.position;

        var orbes = FindObjectsByType<Orbe>(FindObjectsSortMode.None);
        int activos = 0;
        foreach (var o in orbes)
        {
            if (o.recolectado) continue;
            float dist = Vector2.Distance(o.transform.position, posJugador);
            // OJO: se cuenta como activo (y recién DESPUÉS se decide si se
            // recolecta) — un orbe recolectado en ESTE mismo tick no debe
            // sumar a `activos`, si no la reposición de abajo espera un
            // tick de más para notar el hueco (bug real, encontrado por
            // VerificarOrbesEnergiaNivel2.PruebaReponeEscasezTrasRecolectar).
            if (dist <= radioRecoleccionNivel2) { ProcesarPickupOrbeNivel2(o); continue; }
            activos++;
            if (dist <= radioImanNivel2)
                o.transform.position = Vector2.MoveTowards(o.transform.position, posJugador, velocidadImanNivel2 * dt);
        }

        if (activos < orbesObjetivoNivel2) GenerarOrbeNivel2();
    }

    void GenerarOrbeNivel2()
    {
        // Al revés que GenerarOrbeCercaDe (Nivel 1, que repone cerca del
        // último pickup para encadenar combos): acá se sortea lejos de
        // donde está PARADO el jugador ahora mismo, para que ir a
        // buscarlo signifique cruzar la arena, no dar un paso al costado.
        Vector2 posJugador = formaPrecisaActiva != null ? (Vector2)formaPrecisaActiva.transform.position : Vector2.zero;
        float margen = 0.6f;
        float distanciaMinima = Mathf.Min(mitadAncho, mitadAlto) * 0.7f;
        Vector2 pos = Vector2.zero;
        const int intentosMax = 12;
        for (int intento = 0; intento < intentosMax; intento++)
        {
            Vector2 candidato = new Vector2(Random.Range(-mitadAncho + margen, mitadAncho - margen), Random.Range(-mitadAlto + margen, mitadAlto - margen));
            if (Vector2.Distance(candidato, posJugador) >= distanciaMinima || intento == intentosMax - 1) { pos = candidato; break; }
        }
        Instantiate(orbePrefab, pos, Quaternion.identity);
    }

    static readonly Color ColorOrbeNivel2 = new Color(1.4f, 1.1f, 2.6f); // violeta HDR — distinto del cian de Nivel 1, del rojo de golpe y del ámbar de crecimiento
    static readonly Color ColorPulsoPotenteNivel2 = new Color(3f, 1.2f, 0.5f); // ámbar/naranja HDR — bien distinto del violeta normal, se lee "esto es más grande" de un vistazo

    [Header("Nivel 2 — combo rápido -> pulso potente (Fase 5, números de primer pase)")]
    // Si las 5 cargas de una barra se juntan en <= este tiempo (medido en
    // tPelea, el reloj de la pelea — no Time.time, para que quede
    // testeable igual que el resto de este sistema), la descarga sale
    // "potente". Con velocidad=3.4 y una distancia mínima de spawn de
    // ~1.8 unidades (Fase 3), la parte de MOVERSE en línea recta es
    // rápida — el verdadero costo de un combo lento es esquivar patrones
    // mientras se cruza la arena. 10s le da margen real a un jugador
    // hábil sin ser trivial. Primer pase — se retunea con telemetría real
    // (ver "pulso_nivel2_liberado", que ya guarda tiempoCarga).
    public float umbralComboRapidoNivel2 = 10f;
    public float danoPulsoPotenteNivel2 = 20f;
    // CUIDADO CON EL LOOP (pedido explícito): combo rápido -> pulso
    // potente -> muchos orbes del boss -> combo rápido otra vez, sin
    // freno, licuaría la pelea en medio minuto. DOS frenos independientes
    // a propósito (uno solo es esquivable):
    // 1) Cooldown SOLO sobre el pulso POTENTE — el normal nunca se
    //    cappea, para no castigar jugar bien. Mientras está activo,
    //    CUALQUIER descarga (aunque se junte rápido) resuelve normal.
    // 2) Los orbes que suelta el boss valen MENOS que uno de los escasos
    //    normales (cargaPorOrbeBossNivel2 < cargaPorOrbeNivel2) — un
    //    combo armado SOLO con esos no llega a llenar la próxima barra:
    //    4 orbes * 10 = 40, hace falta al menos un orbe normal más (que sí
    //    toma tiempo real cruzar a buscar) para completarla. Sin nombre
    //    ni sprite distinto a propósito: la recompensa tiene que sentirse
    //    generosa a la vista, el freno es de ritmo, no de percepción.
    public float cooldownPulsoPotenteNivel2 = 15f;
    public int orbesPorPulsoPotenteNivel2 = 4;
    public float radioOrbesPulsoPotenteNivel2 = 1.4f;
    public float cargaPorOrbeBossNivel2 = 10f;
    float tInicioCargaNivel2;
    float tCooldownPulsoPotenteRestanteNivel2;

    void ProcesarPickupOrbeNivel2(Orbe o)
    {
        if (o.recolectado) return;
        o.recolectado = true;

        if (energiaNivel2 <= 0f) tInicioCargaNivel2 = tPelea; // primer orbe de un ciclo de carga nuevo — arranca a contar acá
        float carga = o.origenBoss ? cargaPorOrbeBossNivel2 : cargaPorOrbeNivel2;
        energiaNivel2 = Mathf.Min(energiaMaxNivel2, energiaNivel2 + carga);
        formaPrecisaActiva?.ActualizarCargaEnergia(FraccionEnergiaNivel2);

        EfectosVisuales.Instancia?.Chispas(o.transform.position, ColorOrbeNivel2, 10, 1.8f);
        EfectosVisuales.Instancia?.Onda(o.transform.position, ColorOrbeNivel2, 0.9f);
        BeepSynth.Instancia?.Beep(760f, 0.06f, BeepSynth.Onda.Seno, 0.16f);
        Telemetria.Registrar(tPelea, "orbe_nivel2", nivelFever, comboOrbes, 0, orbesActivos, $"energia={energiaNivel2:0}/{energiaMaxNivel2:0};origenBoss={o.origenBoss}");

        Destroy(o.gameObject);

        if (energiaNivel2 >= energiaMaxNivel2) DescargarPulsoNivel2();
    }

    /// <summary>
    /// La carga llegó a 100 — dispara el pulso Y lo conecta con la vida
    /// real del boss (punto 4, ver AplicarDanoBossNivel2). Punto 5: si se
    /// juntó rápido Y el cooldown del pulso potente ya expiró, sale
    /// potente (más daño + el boss suelta orbes) en vez de normal.
    /// </summary>
    void DescargarPulsoNivel2()
    {
        Vector2 pos = formaPrecisaActiva != null ? (Vector2)formaPrecisaActiva.transform.position : Vector2.zero;
        float tiempoCarga = tPelea - tInicioCargaNivel2;
        bool potente = tiempoCarga <= umbralComboRapidoNivel2 && tCooldownPulsoPotenteRestanteNivel2 <= 0f;
        var color = potente ? ColorPulsoPotenteNivel2 : ColorOrbeNivel2;

        EfectosVisuales.Instancia?.Onda(pos, color, potente ? 3.2f : 2.2f);
        EfectosVisuales.Instancia?.Chispas(pos, color, potente ? 34 : 20, potente ? 5f : 3.5f);
        EfectosVisuales.Instancia?.Popup(pos + Vector2.up * 0.4f, potente ? "¡PULSO POTENTE!" : "¡PULSO!", color, potente ? 1.8f : 1.4f);
        CameraPunch.Instancia?.Golpear();
        if (potente)
        {
            BeepSynth.Instancia?.Beep(90f, 0.35f, BeepSynth.Onda.Sierra, 0.32f);
            BeepSynth.Instancia?.Beep(700f, 0.16f, BeepSynth.Onda.Cuadrada, 0.22f);
            BeepSynth.Instancia?.Beep(1100f, 0.12f, BeepSynth.Onda.Cuadrada, 0.18f);
        }
        else
        {
            BeepSynth.Instancia?.Beep(140f, 0.28f, BeepSynth.Onda.Sierra, 0.28f);
            BeepSynth.Instancia?.Beep(700f, 0.16f, BeepSynth.Onda.Cuadrada, 0.2f);
        }
        Telemetria.Registrar(tPelea, "pulso_nivel2_liberado", nivelFever, comboOrbes, 0, orbesActivos, $"potente={potente};tiempoCarga={tiempoCarga:F1}");

        energiaNivel2 = 0f;
        formaPrecisaActiva?.ActualizarCargaEnergia(0f);

        AplicarDanoBossNivel2(potente ? danoPulsoPotenteNivel2 : danoPulsoNivel2);

        if (potente)
        {
            tCooldownPulsoPotenteRestanteNivel2 = cooldownPulsoPotenteNivel2;
            GenerarOrbesDelBossNivel2();
        }
    }

    /// <summary>El "riesgo" del pedido explícito: recompensa generosa a la vista, cerca del boss — ir a buscarla significa exponerse a sus patrones de cerca.</summary>
    void GenerarOrbesDelBossNivel2()
    {
        Vector2 centro = PosicionOrigenBoss();
        for (int i = 0; i < orbesPorPulsoPotenteNivel2; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(0.2f, radioOrbesPulsoPotenteNivel2);
            Vector2 pos = centro + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
            var o = Instantiate(orbePrefab, pos, Quaternion.identity);
            o.origenBoss = true;
        }
    }
    // ============= FIN NIVEL 2 — ORBES Y BARRA DE ENERGÍA (Fase 3) =================

    // =============== NIVEL 2 — VIDA DEL BOSS, ESTILO MEGA MAN X3 (Fase 4) ==================
    // "Para que no lo implementes al revés" (pedido explícito): la barra
    // SUBE de 0 a 100 como animación de presentación ANTES de que arranque
    // la pelea de verdad (con tick escalonado), y recién de ahí en más
    // BAJA con el daño real — nunca al revés. El valor lógico (vidaBossNivel2)
    // se fija en el máximo de una, sincrónico — la animación es puramente
    // cosmética sobre el número YA correcto, así que nunca puede desincronizar
    // el daño real de lo que se ve (ver animandoPresentacionVidaBossNivel2).
    [Header("Nivel 2 — vida del boss estilo Mega Man X3 (Fase 4, números de primer pase)")]
    // Primer pase, no dato real todavía: con 5 orbes por pulso (Fase 3) y
    // sin distinguir pulso rápido/potente todavía (eso es el punto 5), 7
    // pulsos ≈ 84 de daño ≈ el 85% que hace falta gastar para llegar justo
    // al umbral de colapso (15%) — un número de partida, no el final. El
    // total real que hace que el jugador promedio llegue ahí cerca de los
    // 80s de música (punto 6) se ajusta con telemetría real, no a ojo (ver
    // Telemetria "boss_dano_nivel2"/"boss_colapso_nivel2").
    public float vidaBossMaxNivel2 = 100f;
    public float danoPulsoNivel2 = 12f;
    // "El administrador nunca es derrotado, solo fracasa" — no hay un 0%
    // real. Al tocar este piso, AplicarDanoBossNivel2 deja de restar vida;
    // el punto 6/7 son los que van a disparar la escalada de privilegios
    // de verdad (sincronizada con el breakdown de la música, ~80s) — por
    // ahora el boss solo se queda sin poder perder más vida.
    public float umbralColapsoBossNivel2 = 15f;
    float vidaBossNivel2;
    public float FraccionVidaBossNivel2 => vidaBossMaxNivel2 > 0f ? Mathf.Clamp01(vidaBossNivel2 / vidaBossMaxNivel2) : 0f;
    bool bossColapsadoNivel2;
    public bool BossColapsadoNivel2 => bossColapsadoNivel2;

    public GameObject panelVidaBossNivel2;
    public Image barraVidaBossFillNivel2;
    bool animandoPresentacionVidaBossNivel2;

    static readonly Color ColorDanoBossNivel2 = new Color(2.4f, 0.5f, 0.7f); // rojo HDR — golpe recibido, distinto del violeta "cargando" de ColorOrbeNivel2

    void AplicarDanoBossNivel2(float cantidad)
    {
        if (bossColapsadoNivel2 || cantidad <= 0f) return;

        vidaBossNivel2 = Mathf.Max(umbralColapsoBossNivel2, vidaBossNivel2 - cantidad);

        Vector2 posBoss = PosicionOrigenBoss();
        EfectosVisuales.Instancia?.Chispas(posBoss, ColorDanoBossNivel2, 14, 2.6f);
        EfectosVisuales.Instancia?.Popup(posBoss + Vector2.up * 0.5f, $"-{cantidad:0}", ColorDanoBossNivel2, 1.2f);
        CameraPunch.Instancia?.Golpear();
        BeepSynth.Instancia?.Beep(180f, 0.12f, BeepSynth.Onda.Sierra, 0.2f);
        if (administradorActivo != null) StartCoroutine(FlashBoss());

        Telemetria.Registrar(tPelea, "boss_dano_nivel2", nivelFever, comboOrbes, 0, orbesActivos, $"vida={vidaBossNivel2:0}/{vidaBossMaxNivel2:0}");

        if (vidaBossNivel2 <= umbralColapsoBossNivel2 && !bossColapsadoNivel2)
        {
            bossColapsadoNivel2 = true;
            Telemetria.Registrar(tPelea, "boss_colapso_nivel2", nivelFever, comboOrbes, 0, orbesActivos);
        }
    }

    IEnumerator FlashBoss()
    {
        var sr = administradorActivo != null ? administradorActivo.sr : null;
        if (sr == null) yield break;
        var original = sr.color;
        sr.color = ColorDanoBossNivel2;
        yield return new WaitForSeconds(0.1f);
        if (administradorActivo != null && administradorActivo.sr != null) administradorActivo.sr.color = original;
    }

    void ActualizarBarraVidaBossNivel2()
    {
        if (barraVidaBossFillNivel2 == null || animandoPresentacionVidaBossNivel2) return;
        barraVidaBossFillNivel2.fillAmount = FraccionVidaBossNivel2;
    }

    /// <summary>
    /// Sube de 0 a 100 con tick escalonado ANTES de que la pelea empiece a
    /// dañarla de verdad — vidaBossNivel2 ya está en el máximo de forma
    /// síncrona desde IniciarPeleaNivel2 (ver comentario del header de
    /// sección), así que esto es puro espectáculo: el handoff hacia
    /// ActualizarBarraVidaBossNivel2 es invisible, termina exactamente en
    /// el mismo valor que ya era correcto.
    /// </summary>
    IEnumerator PresentacionVidaBossNivel2()
    {
        if (barraVidaBossFillNivel2 == null) yield break;
        animandoPresentacionVidaBossNivel2 = true;
        if (panelVidaBossNivel2 != null) panelVidaBossNivel2.SetActive(true);
        barraVidaBossFillNivel2.fillAmount = 0f;

        const int pasos = 12;
        for (int i = 1; i <= pasos; i++)
        {
            barraVidaBossFillNivel2.fillAmount = (float)i / pasos;
            BeepSynth.Instancia?.Beep(300f + i * 40f, 0.04f, BeepSynth.Onda.Cuadrada, 0.14f);
            yield return new WaitForSeconds(0.05f);
        }
        animandoPresentacionVidaBossNivel2 = false;
    }
    // ============ FIN NIVEL 2 — VIDA DEL BOSS, ESTILO MEGA MAN X3 (Fase 4) =================

    // =========== NIVEL 2 — ESCALADA DE PRIVILEGIOS + TRANSFORMACIÓN (Fase 7) ==============
    // El beat del breakdown (~80-88s, 8s reales — duracionBreakdownNivel2
    // menos duracionFase1Nivel2): una terminal desciende, tipea un comando
    // sudo + la respuesta del sistema a VELOCIDAD DE MÁQUINA (pedido
    // explícito: "se lee como el sistema ejecutando una operación
    // elevada, no magia" — nada de tipeo humano lento) y sostiene la
    // súplica del Administrador entera en pantalla — ESE es el beat que
    // tiene que respirar, no el tipeo ("es un beat, no una escena de
    // diálogo"). Aritmética propuesta (pedido explícito, "proponé los
    // números y justificalos"): a 0.045s/carácter, comando (27) + espera
    // + respuesta (33) ocupan ~1.2s+0.3s+1.5s ≈ 3s, +0.4s de aire tras la
    // respuesta, +3s sostenidos para la súplica (esperaPleaNivel2),
    // +1s de flash de transformación ≈ 7.5-7.9s totales — con margen real
    // bajo el presupuesto de 8s del breakdown, sin comprimir la súplica.
    [Header("Nivel 2 — terminal de escalada de privilegios (Fase 7, números de primer pase)")]
    public GameObject panelTerminalNivel2;
    public TMP_Text textoTerminalNivel2;
    public GameObject botonSaltarTerminalNivel2;
    public float duracionTecleoNivel2 = 0.045f;
    public float pausaComandoNivel2 = 0.3f;
    public float esperaPleaNivel2 = 3f;
    public float duracionTransformacionNivel2 = 1f;

    const string ComandoEscaladaNivel2 = "sudo escalate --scope=root";
    const string RespuestaEscaladaNivel2 = "[sistema] acceso concedido: root";
    // La única línea que NO se tipea carácter a carácter — pedido
    // explícito, "dale tiempo de respirar" — aparece entera y se sostiene
    // esperaPleaNivel2 segundos. Es el momento en que deja de ser
    // antagonista.
    const string PleaAdministradorNivel2 = "No sé quién es tu creador, ni qué quieres de nosotros, pero por favor deja en paz este sistema.";

    bool enEscaladaPrivilegiosNivel2;
    public bool EnEscaladaPrivilegiosNivel2 => enEscaladaPrivilegiosNivel2;
    bool saltandoEscaladaPrivilegiosNivel2;
    Coroutine escaladaPrivilegiosCoroutine;
    /// <summary>
    /// Session-scoped, mismo criterio que cutsceneAperturaVistaSesion (ver
    /// su comentario) — a diferencia de la cutscene de apertura, ESTA
    /// secuencia SÍ es saltable (auto-resuelta al instante) desde el
    /// segundo reintento EN ESTA SESIÓN: cae en medio de la pelea y los
    /// reintentos son frecuentes (pedido explícito). LimpiarPartida() NO
    /// la resetea a propósito, para que funcione a través de un reintento
    /// real.
    /// </summary>
    bool escaladaPrivilegiosVistaSesion;
    bool superAdministradorActivo;
    public bool SuperAdministradorActivo => superAdministradorActivo;

    static readonly Color ColorSuperAdministradorNivel2 = new Color(2.6f, 0.3f, 3.2f); // magenta HDR intenso — un nivel de acceso nuevo, bien distinto del rojo de daño y el violeta de carga

    /// <summary>
    /// Punto de entrada, llamado desde PeleaNivel2() tras el latch (ver su
    /// comentario) — INDEPENDIENTE de esa corrutina a propósito (no
    /// "yield return StartCoroutine(EscaladaPrivilegiosNivel2())"):
    /// SaltarEscaladaPrivilegios() necesita poder cortarla con un
    /// StopCoroutine de un solo nivel, y un StopCoroutine externo sobre
    /// una corrutina hija de la que el padre está haciendo yield no
    /// garantiza despertar al padre (comportamiento no confiable entre
    /// versiones de Unity) — así que PeleaNivel2() simplemente termina acá
    /// y esta corrutina dispara su propia continuación al terminar (ver
    /// TerminarEscaladaPrivilegiosNivel2 -> EsperaFinalYVictoriaNivel2).
    /// </summary>
    void IniciarEscaladaPrivilegiosNivel2()
    {
        escaladaPrivilegiosCoroutine = StartCoroutine(EscaladaPrivilegiosNivel2());
    }

    IEnumerator EscaladaPrivilegiosNivel2()
    {
        enEscaladaPrivilegiosNivel2 = true;
        saltandoEscaladaPrivilegiosNivel2 = false;
        Telemetria.Registrar(tPelea, "escalada_privilegios_nivel2_inicio", nivelFever, comboOrbes, 0, orbesActivos);

        if (panelTerminalNivel2 != null) panelTerminalNivel2.SetActive(true);
        if (botonSaltarTerminalNivel2 != null) botonSaltarTerminalNivel2.SetActive(true);
        if (textoTerminalNivel2 != null) textoTerminalNivel2.text = "";

        if (escaladaPrivilegiosVistaSesion)
        {
            Telemetria.Registrar(tPelea, "escalada_privilegios_nivel2_salteada_ya_vista", nivelFever, comboOrbes, 0, orbesActivos);
            FinalizarEscaladaPrivilegiosInstantanea();
            yield break;
        }

        BeepSynth.Instancia?.Beep(500f, 0.08f, BeepSynth.Onda.Cuadrada, 0.14f);
        yield return new WaitForSeconds(0.3f); // el panel "cae" (tween propio, ver PantallaTerminalNivel2) — esto solo da el tiempo

        yield return StartCoroutine(TipearLineaTerminal("$ ", ComandoEscaladaNivel2, duracionTecleoNivel2));
        yield return new WaitForSeconds(pausaComandoNivel2);
        string comandoImpreso = "$ " + ComandoEscaladaNivel2 + "\n";
        yield return StartCoroutine(TipearLineaTerminal(comandoImpreso, RespuestaEscaladaNivel2, duracionTecleoNivel2));
        BeepSynth.Instancia?.Beep(700f, 0.15f, BeepSynth.Onda.Cuadrada, 0.2f);
        yield return new WaitForSeconds(0.4f);

        // La súplica: entera de una, sostenida (ver comentario del header
        // de sección) — lo único "humano" de la secuencia, no tecleado.
        if (textoTerminalNivel2 != null)
            textoTerminalNivel2.text = comandoImpreso + RespuestaEscaladaNivel2 + "\n\n> " + PleaAdministradorNivel2;
        BeepSynth.Instancia?.Beep(220f, 0.5f, BeepSynth.Onda.Seno, 0.2f);
        yield return new WaitForSeconds(esperaPleaNivel2);

        AsegurarSuperAdministradorNivel2();
        yield return new WaitForSeconds(duracionTransformacionNivel2);

        TerminarEscaladaPrivilegiosNivel2();
    }

    IEnumerator TipearLineaTerminal(string encabezado, string linea, float porCaracter)
    {
        if (textoTerminalNivel2 == null) yield break;
        for (int i = 0; i <= linea.Length; i++)
        {
            bool falta = i < linea.Length;
            textoTerminalNivel2.text = encabezado + linea.Substring(0, i) + (falta ? "▌" : "");
            // Un beep cada 4 caracteres, no uno por caracter (pedido vía
            // revisión): comando+respuesta tienen ~58 caracteres, uno por
            // caracter son 58 beeps de 1200Hz apilados en ~2.7s — justo lo
            // que el breakdown casi silencioso de la canción pide NO
            // saturar. La textura de "se está tecleando" se sigue leyendo
            // igual con menos beeps, el silencio real sobrevive.
            if (falta && i % 4 == 0) BeepSynth.Instancia?.Beep(1200f, 0.02f, BeepSynth.Onda.Cuadrada, 0.05f);
            yield return new WaitForSeconds(porCaracter);
        }
    }

    /// <summary>
    /// La transformación en sí — recolorea/agranda al Administrador y
    /// apaga los sistemas de Fase 3/4 que ya no aplican en la Fase 2
    /// ("con privilegios elevados, el virus no puede tocarlo" — pedido
    /// explícito: sin barra de vida, sin orbes, sin pulsos en esta fase).
    /// Idempotente (si ya está activo, no repite el efecto) — la llaman
    /// tanto el beat real de la corrutina como el salto instantáneo
    /// (manual o "ya la vi antes"), mismo patrón que
    /// AsegurarFormaPrecisa/AsegurarFormaNivel3.
    /// </summary>
    void AsegurarSuperAdministradorNivel2()
    {
        if (superAdministradorActivo) return;
        superAdministradorActivo = true;

        if (administradorActivo != null)
        {
            if (administradorActivo.sr != null)
            {
                administradorActivo.sr.color = ColorSuperAdministradorNivel2;
                // Punto 9 (opcional — ver el comentario del campo): sin
                // material asignado, el recoloreo/escala de arriba/abajo
                // siguen siendo la transformación entera, igual que antes.
                if (materialCorrupcionGlitchNivel2 != null)
                {
                    administradorActivo.sr.sharedMaterial = materialCorrupcionGlitchNivel2;
                    StartCoroutine(AnimarGlitchTransformacionNivel2(administradorActivo.sr));
                }
            }
            administradorActivo.transform.localScale *= 1.3f;
        }

        // AplicarDanoBossNivel2 ya corta sola en bossColapsadoNivel2 (no
        // hay forma de que la vida baje de acá en más) — esto además saca
        // la barra de la pantalla y apaga lo que la alimentaba, para que
        // "sin vida que restar" también se LEA así. El punto 8 construye
        // los patrones de verdad de esta fase sobre SuperAdministradorActivo.
        if (panelVidaBossNivel2 != null) panelVidaBossNivel2.SetActive(false);
        // recolectado=true ANTES de Destroy(): Destroy() es diferido (no
        // saca el objeto de FindObjectsByType hasta terminar el frame,
        // mismo motivo por el que ProcesarPickupOrbeNivel2 hace lo mismo)
        // — así cualquier chequeo lógico este mismo frame (tests incluidos,
        // ver VerificarEscaladaPrivilegiosNivel2.cs) ve el estado correcto
        // sin depender de cuándo el motor limpia el GameObject de verdad.
        foreach (var o in FindObjectsByType<Orbe>(FindObjectsSortMode.None)) { o.recolectado = true; Destroy(o.gameObject); }
        formaPrecisaActiva?.ActualizarCargaEnergia(0f);

        Vector2 posBoss = PosicionOrigenBoss();
        EfectosVisuales.Instancia?.Onda(posBoss, ColorSuperAdministradorNivel2, 2.5f);
        EfectosVisuales.Instancia?.Chispas(posBoss, ColorSuperAdministradorNivel2, 26, 4.5f);
        CameraPunch.Instancia?.Golpear();
        BeepSynth.Instancia?.Beep(55f, 0.6f, BeepSynth.Onda.Sierra, 0.3f);
        BeepSynth.Instancia?.Beep(880f, 0.3f, BeepSynth.Onda.Cuadrada, 0.22f);

        Telemetria.Registrar(tPelea, "super_administrador_nivel2_transformacion", nivelFever, comboOrbes, 0, orbesActivos);
    }

    static readonly int IDIntensidadGlitchNivel2 = Shader.PropertyToID("_Intensidad");

    /// <summary>
    /// Sube a corrupción total rápido (el golpe de la transformación) y
    /// decae a un nivel ambiente bajo — "recién reescrito", no "roto para
    /// siempre": un shader de estos momentos clave se nota MÁS si no se
    /// queda a tope el resto de la Fase 2 (pedido explícito: "reservado
    /// para momentos clave", performance-consciente). Vía
    /// MaterialPropertyBlock (no tocar el Material compartido) — mismo
    /// criterio que CrearHazVisual/LaserHazard con _Progreso. Puramente
    /// cosmético: no tiene sentido testearlo en batch mode (Time.deltaTime
    /// no es controlable ahí, ver el límite de siempre) — la única forma
    /// real de confirmar que el paso del shader corre de verdad es una
    /// captura forzando _Intensidad a mano (ver CapturaTerminalNivel2.cs).
    /// </summary>
    IEnumerator AnimarGlitchTransformacionNivel2(SpriteRenderer sr)
    {
        // reposo=0.18 (no 0.12, pedido vía revisión: "verse más amenazante"
        // no solo en el golpe de la transformación sino sostenido) — el
        // Administrador queda visiblemente inestable el resto de la Fase 2,
        // no solo en el flash inicial.
        const float subida = 0.25f, bajada = 0.9f, reposo = 0.18f;
        var mpb = new MaterialPropertyBlock();
        float t = 0f;
        while (t < subida)
        {
            t += Time.deltaTime;
            sr.GetPropertyBlock(mpb);
            mpb.SetFloat(IDIntensidadGlitchNivel2, Mathf.Clamp01(t / subida));
            sr.SetPropertyBlock(mpb);
            yield return null;
        }
        t = 0f;
        while (t < bajada)
        {
            t += Time.deltaTime;
            sr.GetPropertyBlock(mpb);
            mpb.SetFloat(IDIntensidadGlitchNivel2, Mathf.Lerp(1f, reposo, t / bajada));
            sr.SetPropertyBlock(mpb);
            yield return null;
        }
    }

    /// <summary>Deja todo en el estado final que la secuencia completa hubiera dejado, sin tickear tipeo/esperas — usado tanto por el salto manual como por "ya la vi antes" (mismo patrón que FinalizarCutsceneAperturaInstantanea).</summary>
    void FinalizarEscaladaPrivilegiosInstantanea()
    {
        AsegurarSuperAdministradorNivel2();
        if (textoTerminalNivel2 != null)
            textoTerminalNivel2.text = "$ " + ComandoEscaladaNivel2 + "\n" + RespuestaEscaladaNivel2 + "\n\n> " + PleaAdministradorNivel2;
        TerminarEscaladaPrivilegiosNivel2();
    }

    /// <summary>
    /// Cola compartida de los tres caminos (beat real completo, "ya la vi
    /// antes", salto manual): deja el panel apagado, marca la sesión y
    /// dispara la continuación (el tramo silencioso hasta el final de la
    /// canción, ver EsperaFinalYVictoriaNivel2) — nunca hay que acordarse
    /// de llamarla aparte desde cada camino.
    /// </summary>
    void TerminarEscaladaPrivilegiosNivel2()
    {
        escaladaPrivilegiosCoroutine = null;
        saltandoEscaladaPrivilegiosNivel2 = false;
        enEscaladaPrivilegiosNivel2 = false;
        escaladaPrivilegiosVistaSesion = true;
        if (panelTerminalNivel2 != null) panelTerminalNivel2.SetActive(false);
        if (botonSaltarTerminalNivel2 != null) botonSaltarTerminalNivel2.SetActive(false);
        Telemetria.Registrar(tPelea, "escalada_privilegios_nivel2_fin", nivelFever, comboOrbes, 0, orbesActivos);

        if (PeleaActiva) StartCoroutine(FaseLaseresYVictoriaNivel2());
    }

    /// <summary>
    /// Botón "Saltar" de la terminal — NO comparte flag/botón con
    /// SaltarCutscene(): esta secuencia corre con estado==Jugando (no
    /// Cutscene, ver el comentario de IniciarEscaladaPrivilegiosNivel2),
    /// así que el gate de esa otra (estado != Cutscene) no aplica acá.
    /// </summary>
    public void SaltarEscaladaPrivilegios()
    {
        if (!enEscaladaPrivilegiosNivel2 || saltandoEscaladaPrivilegiosNivel2) return;
        saltandoEscaladaPrivilegiosNivel2 = true;
        if (escaladaPrivilegiosCoroutine != null) StopCoroutine(escaladaPrivilegiosCoroutine);
        Telemetria.Registrar(tPelea, "escalada_privilegios_nivel2_saltada_manual", nivelFever, comboOrbes, 0, orbesActivos);
        FinalizarEscaladaPrivilegiosInstantanea();
    }
    // ========= FIN NIVEL 2 — ESCALADA DE PRIVILEGIOS + TRANSFORMACIÓN (Fase 7) ============

    // =============== NIVEL 2 — FASE 2 SUPER ADMINISTRADOR (Fase 8) ================
    // "Con privilegios elevados, el Administrador opera en un nivel de
    // acceso donde el virus no puede tocarlo" (pedido explícito): sin
    // vida, sin orbes, sin pulsos — solo los 4 patrones de pared del viejo
    // Show de Láseres, REUSADOS SIN TOCAR (mismos PatronBarridoSimple/
    // Cruz/Corredor/Abanico que ya usa el cuerpo de Fase 1), más agresivos
    // por el mismo lenguaje que ya escala Fase 1 (EsperaEntrePatrones):
    // menos espera entre patrones, nunca patrones simultáneos — apilar dos
    // al azar podía tapar el escenario entero sin garantía de hueco
    // navegable, y "nunca es vencido, solo fracasa" no vale la pena
    // arriesgarlo contra un patrón injusto. FaseEnTiempo(tPelea)==5
    // (112-128s, el clímax de la canción) es el tramo más difícil del
    // nivel (pedido explícito) — reusa el mapa de canción de Fase 6 en vez
    // de inventar un tercer eje de tiempo.
    [Header("Nivel 2 — Fase 2 Super Administrador, solo láseres (Fase 8, números de primer pase)")]
    public float esperaPatronesFase2MinNivel2 = 1.0f;
    public float esperaPatronesFase2MaxNivel2 = 1.4f;
    public float esperaPatronesFase2ClimaxMinNivel2 = 0.5f;
    public float esperaPatronesFase2ClimaxMaxNivel2 = 0.75f;
    // Identidad propia del boss (pedido vía revisión, inspirado en un par
    // de spellcards de danmaku que el usuario mandó como referencia —
    // adaptadas al lenguaje propio del proyecto: paredes telegrafiadas
    // rotadas, no balas, y el color/personaje siguen siendo los nuestros).
    // A diferencia de los 4 patrones heredados (impersonales, ya eran así
    // desde Fase 1), estos dos SÍ hacen que el Administrador levante el
    // báculo (LanzarHechizo) antes de disparar — dan la sensación de que
    // el boss ataca de verdad, no solo el escenario.
    static readonly Color ColorLaserFase2 = new Color(2.4f, 0.35f, 2.6f); // magenta HDR — eco del color de Super Administrador, distinto del rojo de Fase 1
    public int rayosTelaRadialNivel2 = 9;
    public float anchoRayoTelaRadialNivel2 = 0.35f;
    public int brazosEspiralGiratoriaNivel2 = 5;
    public int oleadasEspiralGiratoriaNivel2 = 6;
    public float pasoAnguloEspiralGiratoriaNivel2 = 12f;
    public float esperaEntreOleadasEspiralGiratoriaNivel2 = 0.12f;

    IEnumerator FaseLaseresYVictoriaNivel2()
    {
        // Los 3 caminos hacia acá (beat real completo, salto manual, "ya
        // la vi antes") llegan en tPelea DISTINTOS — el real cerca de 88s,
        // los otros dos apenas se cruza el latch de Fase 1 (~80s, ver
        // TerminarEscaladaPrivilegiosNivel2) — así que sin esta espera los
        // láseres arrancaban HASTA 8s antes en cualquier salto: encima del
        // breakdown casi silencioso que el punto 7 pide explícitamente no
        // saturar, y la Fase 2 quedaba más larga de lo que el tema permite
        // (pedido explícito: "si se siente pesado se acorta fase 1, no
        // fase 2" — no al revés, por accidente). El Administrador ya
        // transformado se queda quieto en escena durante esta espera.
        float haciaFinDelBreakdown = duracionBreakdownNivel2 - tPelea;
        if (haciaFinDelBreakdown > 0f) yield return new WaitForSeconds(haciaFinDelBreakdown);

        // Segundo pulso de glitch (pedido vía revisión: "verse más
        // amenazante en el cambio de fase") — el de la transformación
        // (AsegurarSuperAdministradorNivel2) marca que YA cambió; este
        // marca que la Fase 2 arranca DE VERDAD, el instante justo antes
        // del primer patrón. Reusa el mismo shader/corrutina, sin escribir
        // uno nuevo.
        if (administradorActivo != null && administradorActivo.sr != null && materialCorrupcionGlitchNivel2 != null)
            StartCoroutine(AnimarGlitchTransformacionNivel2(administradorActivo.sr));

        while (PeleaActiva && tPelea < duracionPeleaNivel2)
        {
            bool climax = FaseEnTiempo(tPelea) == 5;
            // climax queda también en enSuperAdministradorClimaxNivel2
            // (no solo en esta fila de telemetría) — ResolverImpactoLaser
            // lo necesita para etiquetar el golpe (ver su comentario,
            // pedido vía revisión: el tramo 112-128s es justo el que el
            // usuario marcó como "riesgo de sentirse pesado").
            enSuperAdministradorClimaxNivel2 = climax;
            string patron = DispararPatronLaserFase2(climax);
            Telemetria.Registrar(tPelea, "pelea_patron_fase2", nivelFever, comboOrbes, 0, orbesActivos, $"climax={climax};patron={patron}");
            yield return new WaitForSeconds(EsperaClampeadaFase2Nivel2(climax));
        }
        if (PeleaActiva) PeleaNivel2Victoria();
    }

    /// <summary>true durante el tramo 112-128s (clímax de la canción) mientras dura la Fase 2 — ver el comentario de FaseLaseresYVictoriaNivel2. Solo para etiquetar telemetría (ResolverImpactoLaser); no cambia ninguna lógica de juego.</summary>
    bool enSuperAdministradorClimaxNivel2;

    // Selección por PESO, mismo lenguaje que opcionesPatron de Fase 1: los
    // 4 heredados siguen siendo la base, los 2 propios del boss pesan más
    // en el clímax (112-128s) — "el patrón más difícil del nivel" sale de
    // acá, no de un caso especial aparte.
    readonly List<(string nombre, System.Action accion, float peso)> opcionesPatronFase2 = new List<(string, System.Action, float)>();

    string DispararPatronLaserFase2(bool climax)
    {
        opcionesPatronFase2.Clear();
        void Agregar(string nombre, System.Action accion, float peso) => opcionesPatronFase2.Add((nombre, accion, peso));

        Agregar("barrido_simple", PatronBarridoSimple, 1f);
        Agregar("cruz", PatronCruz, 1f);
        Agregar("corredor", PatronCorredor, 1f);
        Agregar("abanico", PatronAbanico, 1f);
        Agregar("tela_radial", PatronTelaRadialFase2, climax ? 2.2f : 1.2f);
        Agregar("espiral_giratoria", PatronEspiralGiratoriaFase2, climax ? 2.2f : 1.0f);

        float total = 0f;
        foreach (var (_, _, peso) in opcionesPatronFase2) total += peso;
        float r = Random.value * total;
        float acumulado = 0f;
        foreach (var (nombre, accion, peso) in opcionesPatronFase2)
        {
            acumulado += peso;
            if (r <= acumulado) { accion(); return nombre; }
        }
        var ultimo = opcionesPatronFase2[opcionesPatronFase2.Count - 1]; // resguardo por redondeo de punto flotante
        ultimo.accion();
        return ultimo.nombre;
    }

    // Patrón propio — Tela Radial: rayosTelaRadialNivel2 paredes rotadas,
    // TODAS centradas en el boss y barriendo 0-180° — cada pared centrada
    // en el origen ya cubre su ángulo opuesto (180°+) al pasar por el
    // centro, así que N paredes alcanzan para 2N direcciones repartidas en
    // los 360° completos, no hace falta el doble de GameObjects.
    // Espaciado angular elegido a partir de una distancia de referencia
    // real (radioTelaRadialReferenciaNivel2 ≈ la distancia típica
    // jugador-boss en esta arena, mitadAlto≈2.9/mitadAncho≈5.2): con 9
    // rayos el paso es 180/9=20°, hueco navegable en esa distancia ≈
    // 3*20°(rad) - anchoRayoTelaRadialNivel2 ≈ 1.05-0.35 ≈ 0.7 unidades —
    // bien por encima del hitbox de la Forma Precisa (radioHitbox=0.0375),
    // pero sensiblemente más ajustado que Corredor (hueco≈mitadAlto*0.4)
    // a propósito: es el patrón "de identidad" del clímax, tiene que
    // sentirse más difícil que los 4 heredados.
    public float radioTelaRadialReferenciaNivel2 = 3f;

    void PatronTelaRadialFase2()
    {
        administradorActivo?.LanzarHechizo();
        Vector2 origen = PosicionOrigenBoss();
        EfectosVisuales.Instancia?.Onda(origen, ColorLaserFase2, 1.4f);
        BeepSynth.Instancia?.Beep(130f, 0.35f, BeepSynth.Onda.Sierra, 0.24f);

        float largo = Mathf.Max(mitadAncho, mitadAlto) * 2.6f;
        float offset = Random.Range(0f, 180f / rayosTelaRadialNivel2);
        for (int i = 0; i < rayosTelaRadialNivel2; i++)
        {
            float angulo = offset + i * (180f / rayosTelaRadialNivel2);
            CrearMuroLaserRotado(origen, new Vector2(anchoRayoTelaRadialNivel2, largo), angulo, ColorLaserFase2, telegraphShowLaser, duracionActivoShowLaser);
        }
    }

    // Patrón propio — Espiral Giratoria: el mismo abanico de Tela Radial
    // pero disparado en VARIAS oleadas sucesivas, cada una rotada
    // pasoAnguloEspiralGiratoriaNivel2 grados más que la anterior — un
    // LaserHazard no puede animar su propia rotación una vez configurado
    // (Configurar la fija de una), así que el giro sale de escalonar los
    // disparos en el tiempo, mismo truco que SecuenciaAbanico (Fase 1)
    // con el barrido de 3 rayos paralelos.
    void PatronEspiralGiratoriaFase2() => StartCoroutine(SecuenciaEspiralGiratoriaFase2());

    IEnumerator SecuenciaEspiralGiratoriaFase2()
    {
        administradorActivo?.LanzarHechizo();
        Vector2 origen = PosicionOrigenBoss();
        EfectosVisuales.Instancia?.Chispas(origen, ColorLaserFase2, 12, 2.6f);
        BeepSynth.Instancia?.Beep(160f, 0.3f, BeepSynth.Onda.Sierra, 0.22f);

        float largo = Mathf.Max(mitadAncho, mitadAlto) * 2.6f;
        float anguloBase = Random.Range(0f, 360f);
        for (int ola = 0; ola < oleadasEspiralGiratoriaNivel2; ola++)
        {
            for (int b = 0; b < brazosEspiralGiratoriaNivel2; b++)
            {
                float angulo = anguloBase + ola * pasoAnguloEspiralGiratoriaNivel2 + b * (360f / brazosEspiralGiratoriaNivel2);
                CrearMuroLaserRotado(origen, new Vector2(anchoRayoTelaRadialNivel2, largo), angulo, ColorLaserFase2, telegraphShowLaser, duracionActivoShowLaser);
            }
            yield return new WaitForSeconds(esperaEntreOleadasEspiralGiratoriaNivel2);
        }
    }

    float EsperaEntrePatronesFase2Nivel2(bool climax) => climax
        ? Random.Range(esperaPatronesFase2ClimaxMinNivel2, esperaPatronesFase2ClimaxMaxNivel2)
        : Random.Range(esperaPatronesFase2MinNivel2, esperaPatronesFase2MaxNivel2);

    /// <summary>Mismo motivo que EsperaClampeadaNivel2 (Fase 6/revisión): la última espera del loop no debería empujar tPelea más allá de duracionPeleaNivel2 — el final de la canción es lo que dispara la cutscene de cierre, tiene que llegar puntual.</summary>
    float EsperaClampeadaFase2Nivel2(bool climax) => Mathf.Min(EsperaEntrePatronesFase2Nivel2(climax), Mathf.Max(0.05f, duracionPeleaNivel2 - tPelea));
    // ============= FIN NIVEL 2 — FASE 2 SUPER ADMINISTRADOR (Fase 8) ==============

    IEnumerator PeleaNivel2()
    {
        yield return new WaitForSeconds(graciaInicialPelea);

        // Fase 1 ("El Administrador"): todo el sistema de orbes/energía/
        // pulso/vida vive acá, cortado a duracionFase1Nivel2 (no al final
        // de la canción) — pedido explícito: "las fases van por TIEMPO,
        // no por umbral de vida", para no perder la sincronía musical.
        while (PeleaActiva && tPelea < duracionFase1Nivel2)
        {
            int fase = EscaladaPatronesFase1(tPelea);
            string patron = DispararPatronDeFase(fase);
            Telemetria.Registrar(tPelea, "pelea_patron", nivelFever, comboOrbes, 0, orbesActivos, $"fase={fase};patron={patron}");
            yield return new WaitForSeconds(EsperaClampeadaNivel2(fase));
        }
        if (!PeleaActiva) yield break;

        // El latch (pedido explícito: "si llega antes o después, que la
        // transición espere o acelere hasta enganchar con el breakdown —
        // la sincronía importa más que la precisión del umbral"): si el
        // jugador ya cruzó el umbral de vida, el boss se queda colapsado
        // ESPERANDO acá sin que nada lo apure. Si todavía no cruzó, se
        // fuerza ahora — nunca se llega al breakdown con el boss todavía
        // "sano".
        if (!bossColapsadoNivel2) ForzarColapsoBossNivel2();

        // Punto 7: dispara la terminal de escalada + transformación a
        // Super Administrador, sobre el breakdown (~80-88s) — corrutina
        // INDEPENDIENTE (no un yield anidado, ver el comentario de
        // IniciarEscaladaPrivilegiosNivel2). estado se queda en Jugando
        // durante todo ese tramo a propósito: si pasara a Cutscene acá,
        // Update() dejaría de avanzar tPelea (early return) mientras la
        // música seguiría sonando, perdiendo la sincronía que todo este
        // punto 6 existe para garantizar. PeleaNivel2() termina acá — la
        // propia escalada dispara su continuación al terminar (ver
        // TerminarEscaladaPrivilegiosNivel2 -> EsperaFinalYVictoriaNivel2).
        IniciarEscaladaPrivilegiosNivel2();
    }

    /// <summary>
    /// EsperaEntrePatrones recortada para que la ÚLTIMA espera del loop de
    /// Fase 1 nunca empuje tPelea de más allá de duracionFase1Nivel2 —
    /// pedido vía revisión: sin esto el loop podía salir hasta ~0.95s tarde
    /// (fase 5 espera 0.6-0.95s), casi 2 beats de deriva en el arranque del
    /// latch. Extraída y pura (no lee tPelea internamente, lo recibe
    /// implícito vía el campo) para poder testearla sin tickear la
    /// corrutina (ver VerificarEscaladaPrivilegiosNivel2.cs).
    /// </summary>
    float EsperaClampeadaNivel2(int fase) => Mathf.Min(EsperaEntrePatrones(fase), Mathf.Max(0.05f, duracionFase1Nivel2 - tPelea));

    /// <summary>Mitad del latch de arriba: la sincronía con la música importa más que la precisión del umbral de vida.</summary>
    void ForzarColapsoBossNivel2()
    {
        vidaBossNivel2 = umbralColapsoBossNivel2;
        bossColapsadoNivel2 = true;
        Telemetria.Registrar(tPelea, "boss_colapso_nivel2", nivelFever, comboOrbes, 0, orbesActivos, "forzado_por_tiempo=true");
    }

    // Se acorta con cada fase de EscaladaPatronesFase1 — a partir de fase
    // 3 el hueco puede ser más corto que la resolución del patrón anterior
    // (que ronda 1-1.5s entre telegraph y última bala), así que empiezan a
    // superponerse de verdad: eso es lo que hace "difícil de verdad" en
    // vez de "un patrón memorizable por vez".
    float EsperaEntrePatrones(int fase)
    {
        switch (fase)
        {
            case 1: return Random.Range(2.6f, 3.4f);
            case 2: return Random.Range(2.0f, 2.6f);
            case 3: return Random.Range(1.3f, 1.8f);
            case 4: return Random.Range(0.9f, 1.3f);
            default: return Random.Range(0.6f, 0.95f); // fase 5 — recta final
        }
    }

    // Selección por PESO en vez de un switch de casos parejos — pedido
    // explícito tras ver Espiral Doble/Flor Giratoria en acción: "necesito
    // más de esos patrones a lo largo de la batalla". Las dos arrancan un
    // fase antes que en la primera pasada (Espiral Doble desde fase 2,
    // Flor Giratoria desde fase 3) y su peso crece más rápido que el del
    // resto a medida que suben las fases, así que se sienten como el
    // núcleo de la pelea en vez de una rareza ocasional — sin sacar del
    // todo a la familia láser ni a Anillo/Espiral/Disparo Dirigido, que
    // siguen dando variedad de ritmo.
    // `nombre` viaja junto a la acción SOLO para telemetría (punto 10,
    // pedido vía revisión: "qué patrón mata gente" no se puede contestar
    // si el elegido se descarta apenas se sortea) — no cambia en nada la
    // selección por peso de abajo.
    readonly List<(string nombre, System.Action accion, float peso)> opcionesPatron = new List<(string, System.Action, float)>();

    /// <summary>Devuelve el NOMBRE del patrón elegido (el disparo real ya pasó) — DispararPatronLaserFase2/PeleaNivel2 lo usan para que pelea_patron diga cuál salió, no solo la fase.</summary>
    string DispararPatronDeFase(int fase)
    {
        opcionesPatron.Clear();
        void Agregar(string nombre, System.Action accion, float peso) => opcionesPatron.Add((nombre, accion, peso));

        switch (fase)
        {
            case 1: // solo la familia láser — el show empieza de a poco
                Agregar("barrido_simple", PatronBarridoSimple, 1f);
                Agregar("cruz", PatronCruz, 1f);
                Agregar("corredor", PatronCorredor, 1f);
                Agregar("abanico", PatronAbanico, 1f);
                break;
            case 2: // suma anillo, disparo dirigido y — ya, no en fase 3 — espiral doble
                Agregar("barrido_simple", PatronBarridoSimple, 0.8f);
                Agregar("cruz", PatronCruz, 0.8f);
                Agregar("corredor", PatronCorredor, 0.8f);
                Agregar("abanico", PatronAbanico, 0.8f);
                Agregar("anillo_expansivo", () => PatronAnilloExpansivo(fase), 1f);
                Agregar("disparo_dirigido", () => PatronDisparoDirigido(fase), 1f);
                Agregar("espiral_doble", PatronEspiralDoble, 1.4f);
                break;
            case 3: // suma espiral simple y — ya, no en fase 4 — flor giratoria
                Agregar("barrido_simple", PatronBarridoSimple, 0.5f);
                Agregar("cruz", PatronCruz, 0.6f);
                Agregar("corredor", PatronCorredor, 0.5f);
                Agregar("abanico", PatronAbanico, 0.6f);
                Agregar("anillo_expansivo", () => PatronAnilloExpansivo(fase), 0.9f);
                Agregar("disparo_dirigido", () => PatronDisparoDirigido(fase), 0.9f);
                Agregar("espiral", () => PatronEspiral(fase), 0.9f);
                Agregar("espiral_doble", PatronEspiralDoble, 1.8f);
                Agregar("flor_giratoria", PatronFlorGiratoria, 1.8f);
                break;
            case 4: // fuera Corredor — el resto sigue, favoritos todavía más pesados
                Agregar("cruz", PatronCruz, 0.5f);
                Agregar("abanico", PatronAbanico, 0.5f);
                Agregar("anillo_expansivo", () => PatronAnilloExpansivo(fase), 0.8f);
                Agregar("disparo_dirigido", () => PatronDisparoDirigido(fase), 0.8f);
                Agregar("espiral", () => PatronEspiral(fase), 0.8f);
                Agregar("espiral_doble", PatronEspiralDoble, 2.2f);
                Agregar("flor_giratoria", PatronFlorGiratoria, 2.2f);
                break;
            default: // fase 5 — recta final: casi todo es Espiral Doble/Flor Giratoria
                Agregar("cruz", PatronCruz, 0.3f);
                Agregar("abanico", PatronAbanico, 0.3f);
                Agregar("anillo_expansivo", () => PatronAnilloExpansivo(fase), 0.6f);
                Agregar("disparo_dirigido", () => PatronDisparoDirigido(fase), 0.6f);
                Agregar("espiral", () => PatronEspiral(fase), 0.6f);
                Agregar("espiral_doble", PatronEspiralDoble, 2.6f);
                Agregar("flor_giratoria", PatronFlorGiratoria, 2.6f);
                break;
        }

        float total = 0f;
        foreach (var (_, _, peso) in opcionesPatron) total += peso;
        float r = Random.value * total;
        float acumulado = 0f;
        foreach (var (nombre, accion, peso) in opcionesPatron)
        {
            acumulado += peso;
            if (r <= acumulado) { accion(); return nombre; }
        }
        var ultimo = opcionesPatron[opcionesPatron.Count - 1]; // resguardo por redondeo de punto flotante
        ultimo.accion();
        return ultimo.nombre;
    }

    // Patrón 5 — Anillo expansivo: balas en todas direcciones desde el
    // boss a velocidad uniforme — siempre hay hueco navegable porque
    // alcanza con no estar exactamente en el radio del anillo en el
    // instante en que te cruza, no hace falta enhebrar un hueco fijo
    // como en los láseres. Desde fase 3 dispara una SEGUNDA oleada
    // desfasada medio paso y más rápida (le pisa los talones a la
    // primera) — un anillo doble que exige moverte dos veces, no una.
    void PatronAnilloExpansivo(int fase) => StartCoroutine(SecuenciaAnillo(PosicionOrigenBoss(), fase));

    IEnumerator SecuenciaAnillo(Vector2 origen, int fase)
    {
        EfectosVisuales.Instancia?.Onda(origen, ColorProyectil, 0.8f);
        BeepSynth.Instancia?.Beep(140f, 0.3f, BeepSynth.Onda.Seno, 0.2f);
        yield return new WaitForSeconds(0.5f); // telegraph

        int cantidad = 14 + fase * 3; // 17/20/23/26/29
        float velocidadBala = 2.3f + fase * 0.12f;
        DispararAnillo(origen, cantidad, velocidadBala, 0f);

        if (fase >= 3)
        {
            yield return new WaitForSeconds(0.3f);
            EfectosVisuales.Instancia?.Onda(origen, ColorProyectil, 0.5f);
            DispararAnillo(origen, cantidad, velocidadBala * 1.35f, Mathf.PI / cantidad); // desfasado medio paso, más rápido: alcanza a la primera oleada
        }
    }

    void DispararAnillo(Vector2 origen, int cantidad, float velocidadBala, float offsetAngular)
    {
        for (int i = 0; i < cantidad; i++)
        {
            float ang = offsetAngular + i * Mathf.PI * 2f / cantidad;
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            DispararProyectil(origen, dir * velocidadBala, ColorProyectil);
        }
    }

    // Patrón 6 — Espiral: balas disparadas de a pocas girando — más denso
    // que el anillo pero siempre con espacio entre brazos consecutivos,
    // nunca cierra un frente sólido. Vueltas/velocidad escalan con la
    // fase para que se sienta más agresiva hacia el final sin cambiar su
    // lectura (sigue siendo "esquivar girando con el patrón").
    void PatronEspiral(int fase) => StartCoroutine(SecuenciaEspiral(PosicionOrigenBoss(), fase));

    IEnumerator SecuenciaEspiral(Vector2 origen, int fase)
    {
        EfectosVisuales.Instancia?.Chispas(origen, ColorProyectil, 8, 2f);
        BeepSynth.Instancia?.Beep(180f, 0.25f, BeepSynth.Onda.Sierra, 0.18f);
        yield return new WaitForSeconds(0.4f); // telegraph

        const int brazos = 3;
        int vueltas = 8 + fase;
        float velocidadBala = 2.0f + fase * 0.12f;
        float anguloBase = Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < vueltas; i++)
        {
            for (int b = 0; b < brazos; b++)
            {
                float ang = anguloBase + i * 0.5f + b * (Mathf.PI * 2f / brazos);
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                DispararProyectil(origen, dir * velocidadBala, ColorProyectil);
            }
            yield return new WaitForSeconds(0.08f);
        }
    }

    // Patrón 8 — Espiral doble contra-rotante: dos espirales de 3 brazos
    // girando en sentidos opuestos a la vez, entrelazadas desde el mismo
    // origen — el clásico "flor" de danmaku. Mucho más denso y mucho más
    // difícil de leer que una espiral sola (hay que seguir dos ritmos de
    // giro distintos al mismo tiempo), sin dejar de ser justa: cada brazo
    // por separado deja el mismo hueco navegable que uno solo.
    void PatronEspiralDoble() => StartCoroutine(SecuenciaEspiralDoble(PosicionOrigenBoss()));

    IEnumerator SecuenciaEspiralDoble(Vector2 origen)
    {
        EfectosVisuales.Instancia?.Chispas(origen, ColorProyectil, 12, 2.5f);
        EfectosVisuales.Instancia?.Onda(origen, ColorProyectil, 0.6f);
        BeepSynth.Instancia?.Beep(210f, 0.3f, BeepSynth.Onda.Sierra, 0.22f);
        yield return new WaitForSeconds(0.45f); // telegraph — patrón denso, necesita un poco más de aviso

        const int brazos = 3;
        const int vueltas = 12;
        const float velocidadBala = 2.3f;
        float anguloA = Random.Range(0f, Mathf.PI * 2f);
        float anguloB = anguloA + Mathf.PI / brazos; // arranca desfasado, gira al revés
        for (int i = 0; i < vueltas; i++)
        {
            for (int b = 0; b < brazos; b++)
            {
                float aA = anguloA + i * 0.45f + b * (Mathf.PI * 2f / brazos);
                float aB = anguloB - i * 0.45f + b * (Mathf.PI * 2f / brazos);
                DispararProyectil(origen, new Vector2(Mathf.Cos(aA), Mathf.Sin(aA)) * velocidadBala, ColorProyectil);
                DispararProyectil(origen, new Vector2(Mathf.Cos(aB), Mathf.Sin(aB)) * velocidadBala, ColorProyectil);
            }
            yield return new WaitForSeconds(0.07f);
        }
    }

    // Patrón 9 — Flor Giratoria: el patrón más creativo del set, pedido
    // explícito ("patrones creativos") — varios brazos nacen a la vez
    // desde el boss y CURVAN mientras se alejan (Proyectil.velocidadAngular,
    // alternando sentido de giro por brazo), así que el copo entero gira
    // mientras se expande — referencia directa al estilo Touhou
    // compartido. No alcanza con "salir del radio": hay que leer hacia
    // dónde curva cada brazo y moverse con la rotación, no contra ella.
    void PatronFlorGiratoria() => StartCoroutine(SecuenciaFlorGiratoria(PosicionOrigenBoss()));

    IEnumerator SecuenciaFlorGiratoria(Vector2 origen)
    {
        EfectosVisuales.Instancia?.Onda(origen, ColorProyectil, 1f);
        EfectosVisuales.Instancia?.Chispas(origen, ColorProyectil, 14, 3f);
        BeepSynth.Instancia?.Beep(100f, 0.45f, BeepSynth.Onda.Seno, 0.24f);
        yield return new WaitForSeconds(0.6f); // el más denso del set — más telegraph que el resto

        const int brazos = 10;
        const int balasPorBrazo = 3;
        const float velocidadBase = 1.85f;
        const float curvatura = 55f; // grados/seg — el sentido alterna por brazo
        for (int b = 0; b < brazos; b++)
        {
            float angBase = b * Mathf.PI * 2f / brazos;
            float signo = (b % 2 == 0) ? 1f : -1f;
            Vector2 dir = new Vector2(Mathf.Cos(angBase), Mathf.Sin(angBase));
            for (int i = 0; i < balasPorBrazo; i++)
            {
                // Las balas de más adentro del brazo salen un poco más
                // lentas — el brazo se "estira" en vez de viajar como un
                // bloque rígido, y cada una curva desde su propio punto
                // de partida así el brazo entero se lee como un pétalo.
                Vector2 vel = dir * (velocidadBase + i * 0.22f);
                DispararProyectil(origen, vel, ColorProyectil, velocidadAngular: curvatura * signo);
            }
            yield return new WaitForSeconds(0.045f);
        }
    }

    // Patrón 7 — Disparo dirigido: telegrafeado hacia donde estabas AL
    // MOMENTO del telegraph, no te sigue — presiona a moverse después de
    // ver el aviso, castiga quedarse quieto sin ser un patrón imposible
    // de leer. Desde fase 4 dispara un abanico más ancho (5 balas, más
    // juntas) en vez de 3 — más presión real, sigue siendo un único
    // telegraph legible.
    void PatronDisparoDirigido(int fase) => StartCoroutine(SecuenciaDisparoDirigido(PosicionOrigenBoss(), fase));

    IEnumerator SecuenciaDisparoDirigido(Vector2 origen, int fase)
    {
        Vector2 objetivo = formaPrecisaActiva != null ? (Vector2)formaPrecisaActiva.transform.position : Vector2.zero;
        Vector2 dir = (objetivo - origen).sqrMagnitude > 0.01f ? (objetivo - origen).normalized : Vector2.down;

        EfectosVisuales.Instancia?.Onda(origen, ColorProyectil, 0.5f);
        BeepSynth.Instancia?.Beep(220f, 0.35f, BeepSynth.Onda.Cuadrada, 0.2f);
        yield return new WaitForSeconds(0.55f); // telegraph — tiempo real para reaccionar

        int cantidad = fase >= 4 ? 5 : 3;
        float velocidadBala = 3.6f;
        float separacionAngular = fase >= 4 ? 0.07f : 0.09f; // radianes entre balas consecutivas del disparo
        for (int i = 0; i < cantidad; i++)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) + (i - (cantidad - 1) / 2f) * separacionAngular;
            Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            DispararProyectil(origen, d * velocidadBala, ColorProyectil);
        }
    }

    void ManejarDerrotaNivel2()
    {
        if (!PeleaActiva) return; // a prueba de duplicados si el evento se disparara más de una vez
        TerminarPeleaNivel2(victoria: false);
    }

    // ================= NIVEL 2 — CUTSCENE DE CIERRE (punto 5) =============
    // Al llegar al final de duracionPeleaNivel2 con vidas de sobra, en vez
    // de cerrar la pelea de una (lo que hacía el placeholder del punto 4)
    // arranca esta cutscene: el boss recasta el hechizo, un campo de
    // fuerza reposiciona a los dos a esquinas opuestas, un láser de un
    // color nunca antes usado impacta y transforma a la Forma Precisa en
    // la forma de Nivel 3 (un placeholder LIMPIO — el gameplay de Nivel 3
    // no hay que construirlo). Mismo esqueleto que la cutscene de
    // apertura: comparte panel/corrutina/botón "Saltar" (enCutsceneCierre
    // distingue cuál está activa) y es igual de skippeable después de la
    // primera vez (MetaProgreso.CutsceneCierreVista) — pedido explícito,
    // "ganar de nuevo no debería tragarse todo otra vez".
    void PeleaNivel2Victoria()
    {
        PeleaActiva = false;
        LimpiarProyectiles();
        estado = EstadoJuego.Cutscene;
        enCutsceneCierre = true;
        if (panelCutsceneNivel2 != null) panelCutsceneNivel2.SetActive(true);
        if (botonSaltarCutscene != null) botonSaltarCutscene.SetActive(true);
        Telemetria.Registrar(tPelea, "cutscene_nivel2_cierre_inicio", nivelFever, comboOrbes, 0, orbesActivos);

        if (MetaProgreso.CutsceneCierreVista)
        {
            Telemetria.Registrar(tPelea, "cutscene_nivel2_cierre_salteada_ya_vista", nivelFever, comboOrbes, 0, orbesActivos);
            FinalizarCutsceneCierreInstantanea();
            return;
        }

        cutsceneCoroutine = StartCoroutine(CutsceneCierreNivel2());
    }

    IEnumerator CutsceneCierreNivel2()
    {
        Vector2 esquinaBoss = new Vector2(-mitadAncho * 0.75f, mitadAlto * 0.75f);
        Vector2 esquinaJugador = new Vector2(mitadAncho * 0.75f, -mitadAlto * 0.75f);

        // a. Vuelve a lanzar el hechizo — el mismo gesto que en la
        // apertura, pero ahora es el segundo intento: desesperado, no la
        // curiosidad de la primera vez (pedido explícito).
        administradorActivo?.LanzarHechizo();
        BeepSynth.Instancia?.Beep(90f, 0.6f, BeepSynth.Onda.Sierra, 0.3f);
        yield return MostrarDialogo("¡NO TE DEJARÉ CORROMPER EL SISTEMA!", 1.7f);
        yield return new WaitForSeconds(0.2f);

        // b. Campo de fuerza: reposiciona a los dos a esquinas opuestas —
        // tiene que sentirse deliberado y pesado, no un teletransporte
        // instantáneo (pedido explícito).
        EfectosVisuales.Instancia?.Onda(Vector2.zero, ColorFinalNivel3, 2f);
        CameraPunch.Instancia?.Golpear();
        BeepSynth.Instancia?.Beep(60f, 0.5f, BeepSynth.Onda.Cuadrada, 0.3f);
        yield return ReposicionarACorners(esquinaBoss, esquinaJugador, 1.1f);
        yield return new WaitForSeconds(0.3f);

        // c. El láser final: un color nunca antes usado en el juego,
        // escalada más grande que la transformación de apertura (pedido
        // explícito).
        yield return DispararLaserFinal(esquinaBoss, esquinaJugador, ColorFinalNivel3);

        // d. Impacta y transforma — el beat más importante de este cierre.
        AsegurarFormaNivel3(esquinaJugador);
        yield return new WaitForSeconds(0.5f);

        // e. Reacción final del boss — el mismo fracaso de siempre, otra
        // vez (eco deliberado del "¡¿?!" de la apertura).
        administradorActivo?.Retroceder();
        BeepSynth.Instancia?.Beep(140f, 0.3f, BeepSynth.Onda.Seno, 0.2f);
        yield return MostrarDialogo("¡¿OTRA VEZ...?!", 1.2f);

        TerminarCutsceneCierre();
    }

    IEnumerator ReposicionarACorners(Vector2 posBoss, Vector2 posJugador, float duracion)
    {
        Vector2 inicioBoss = administradorActivo != null ? (Vector2)administradorActivo.transform.position : posBoss;
        Vector2 inicioJugador = formaPrecisaActiva != null ? (Vector2)formaPrecisaActiva.transform.position : posJugador;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / duracion), 3f); // ease-out cúbico, mismo lenguaje que AdministradorSistema.Descender
            if (administradorActivo != null) administradorActivo.transform.position = Vector2.Lerp(inicioBoss, posBoss, p);
            if (formaPrecisaActiva != null) formaPrecisaActiva.transform.position = Vector2.Lerp(inicioJugador, posJugador, p);
            yield return null;
        }
        if (administradorActivo != null) administradorActivo.transform.position = posBoss;
        if (formaPrecisaActiva != null) formaPrecisaActiva.transform.position = posJugador;
    }

    /// <summary>
    /// Puramente visual (sin LaserHazard/ResolverImpactoLaser) — este
    /// golpe es 100% guionado, ya no hay nada que esquivar (el campo de
    /// fuerza te sacó la agencia recién), así que no tiene sentido que
    /// pase por el mismo camino de daño/invulnerabilidad que un patrón de
    /// verdad.
    /// </summary>
    IEnumerator DispararLaserFinal(Vector2 origenBoss, Vector2 posJugador, Color color)
    {
        BeepSynth.Instancia?.Beep(50f, 0.8f, BeepSynth.Onda.Sierra, 0.35f);
        EfectosVisuales.Instancia?.Onda(origenBoss, color, 1.5f);
        yield return new WaitForSeconds(0.5f); // telegraph largo — el golpe más grande del juego, se deja ver venir

        const float grosor = 1.4f; // mucho más ancho que cualquier pared anterior (grosor normal 0.5-0.9)
        CrearHazVisual(new Rect(posJugador.x - grosor * 0.5f, -mitadAlto * 1.3f, grosor, mitadAlto * 2.6f), color, 1.3f);
        CrearHazVisual(new Rect(-mitadAncho * 1.3f, posJugador.y - grosor * 0.5f, mitadAncho * 2.6f, grosor), color, 1.3f);
        CameraPunch.Instancia?.Golpear();
        EfectosVisuales.Instancia?.Onda(posJugador, color, 2.2f);
        BeepSynth.Instancia?.Beep(700f, 0.4f, BeepSynth.Onda.Cuadrada, 0.3f);

        yield return new WaitForSeconds(0.4f);
    }

    void CrearHazVisual(Rect area, Color color, float duracion)
    {
        var go = new GameObject("HazCierreVisual");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = -4;
        if (materialHazEnergia != null)
        {
            sr.sprite = ShapeFactory.Cuadrado(8, Color.white);
            sr.sharedMaterial = materialHazEnergia;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_ColorBorde", color);
            mpb.SetColor("_ColorNucleo", Color.white);
            mpb.SetFloat("_Progreso", 1f); // sin telegraph propio — ya resuelto desde el primer frame
            sr.SetPropertyBlock(mpb);
        }
        else
        {
            sr.sprite = ShapeFactory.Haz(64, color);
            sr.color = Color.white;
        }
        go.transform.position = new Vector3(area.center.x, area.center.y, 0f);
        go.transform.localScale = new Vector3(Mathf.Max(area.width, 0.01f), Mathf.Max(area.height, 0.01f), 1f);
        Destroy(go, duracion);
    }

    /// <summary>
    /// Destruye la Forma Precisa y la reemplaza por el placeholder LIMPIO
    /// de la forma de Nivel 3 — idempotente (si ya existe, no hace nada),
    /// mismo patrón que AsegurarFormaPrecisa, para poder llamarla tanto
    /// desde el beat normal de la cutscene como desde el salto instantáneo.
    /// </summary>
    void AsegurarFormaNivel3(Vector2 centro)
    {
        if (formaNivel3Activa != null) return;

        if (formaPrecisaActiva != null)
        {
            formaPrecisaActiva.AlQuedarSinVidas -= ManejarDerrotaNivel2;
            Destroy(formaPrecisaActiva.gameObject);
            formaPrecisaActiva = null;
        }

        formaNivel3Activa = new GameObject("FormaNivel3_Placeholder");
        formaNivel3Activa.transform.position = centro;
        var sr = formaNivel3Activa.AddComponent<SpriteRenderer>();
        sr.sprite = ShapeFactory.Estrella(64, 5);
        sr.color = ColorFinalNivel3;
        formaNivel3Activa.transform.localScale = Vector3.one * 0.5f;

        EfectosVisuales.Instancia?.Onda(centro, ColorFinalNivel3, 1.8f);
        EfectosVisuales.Instancia?.Chispas(centro, ColorFinalNivel3, 28, 5f);
        CameraPunch.Instancia?.Golpear();
        BeepSynth.Instancia?.Beep(900f, 0.3f, BeepSynth.Onda.Cuadrada, 0.3f);
        BeepSynth.Instancia?.Beep(500f, 0.35f, BeepSynth.Onda.Cuadrada, 0.25f);

        Telemetria.Registrar(tPelea, "forma_nivel3_transformacion", nivelFever, comboOrbes, 0, orbesActivos);
    }

    /// <summary>Deja todo en el estado que la cutscene de cierre completa hubiera dejado, sin tickear tweens/esperas — usado tanto por el salto manual como por "ya la vi antes".</summary>
    void FinalizarCutsceneCierreInstantanea()
    {
        Vector2 esquinaBoss = new Vector2(-mitadAncho * 0.75f, mitadAlto * 0.75f);
        Vector2 esquinaJugador = new Vector2(mitadAncho * 0.75f, -mitadAlto * 0.75f);

        if (administradorActivo != null) administradorActivo.transform.position = esquinaBoss;
        AsegurarFormaNivel3(esquinaJugador);

        if (textoDialogoCutscene != null) textoDialogoCutscene.gameObject.SetActive(false);
        if (panelCutsceneNivel2 != null) panelCutsceneNivel2.SetActive(false);
        if (botonSaltarCutscene != null) botonSaltarCutscene.SetActive(false);

        TerminarCutsceneCierre();
    }

    void TerminarCutsceneCierre()
    {
        cutsceneCoroutine = null;
        saltandoCutscene = false;
        enCutsceneCierre = false;
        MetaProgreso.MarcarCutsceneCierreVista();
        if (panelCutsceneNivel2 != null) panelCutsceneNivel2.SetActive(false);
        if (botonSaltarCutscene != null) botonSaltarCutscene.SetActive(false);
        Telemetria.Registrar(tPelea, "cutscene_nivel2_cierre_fin", nivelFever, comboOrbes, 0, orbesActivos);

        TerminarPeleaNivel2(victoria: true);
    }
    // =============== FIN NIVEL 2 — CUTSCENE DE CIERRE (punto 5) ===========

    /// <summary>
    /// Cierre de la pelea — reusa panelFin (de Nivel 1) con texto propio.
    /// En victoria, ya pasó por la cutscene de cierre de arriba (la Forma
    /// Precisa ya es formaNivel3Activa a esta altura). BotonReintentar ya
    /// sabe volver a EntrarCutsceneNivel2() en vez de EmpezarPartida()
    /// cuando modoNivel2 sigue en true (ver abajo del archivo).
    /// </summary>
    void TerminarPeleaNivel2(bool victoria)
    {
        PeleaActiva = false;
        if (formaPrecisaActiva != null) formaPrecisaActiva.AlQuedarSinVidas -= ManejarDerrotaNivel2;
        StopAllCoroutines(); // corta PeleaNivel2()/la cutscene de cierre y cualquier patrón (Anillo/Espiral/Abanico/etc) en curso
        LimpiarProyectiles();
        if (panelVidaBossNivel2 != null) panelVidaBossNivel2.SetActive(false);
        // Mismo leak que ya se vio con Fundir()/la Forma Precisa/el boss:
        // si el jugador muere a un proyectil que ya estaba en el aire
        // justo durante la terminal (ActualizarProyectiles sigue corriendo
        // durante toda la escalada), StopAllCoroutines() de arriba corta
        // EscaladaPrivilegiosNivel2() a mitad, y sin esto el panel se
        // quedaba pegado en pantalla sin nada corriendo que lo apagara.
        if (panelTerminalNivel2 != null) panelTerminalNivel2.SetActive(false);
        if (botonSaltarTerminalNivel2 != null) botonSaltarTerminalNivel2.SetActive(false);
        estado = EstadoJuego.Fin;
        MusicManager.Instancia?.Detener();

        Telemetria.Registrar(tPelea, victoria ? "pelea_nivel2_victoria" : "pelea_nivel2_derrota", nivelFever, comboOrbes, 0, orbesActivos);
        Telemetria.CerrarSiHabiaAlguna();

        if (panelFin != null)
        {
            panelFin.SetActive(true);
            if (textoResultado != null)
                textoResultado.text = victoria
                    ? $"El Administrador te fuerza a un formato desconocido...\n(Nivel 3 — próximamente)"
                    : $"Te borró el Administrador — {tPelea:F1} s";
            // Records/Esencia son conceptos de Nivel 1 — no aplican acá,
            // así que se limpian en vez de mostrar datos de la partida
            // de Nivel 1 anterior (si la hubo).
            if (textoRecordFinal != null) textoRecordFinal.text = "";
            if (textoEsenciaGanada != null) textoEsenciaGanada.text = "";
        }
    }
    // =================== FIN NIVEL 2 — PELEA (punto 4) ===================

    /// <summary>Llamado por LaserHazard cuando "dispara" — un único chequeo de área, no continuo.</summary>
    public void ResolverImpactoLaser(Rect area)
    {
        foreach (var n in new List<Nucleo>(nucleosActivos))
        {
            if (n == null) continue;
            if (area.Contains((Vector2)n.transform.position)) ProcesarImpactoLaser(n);
        }

        // Nivel 2: nucleosActivos se queda vacío a propósito durante toda
        // la pelea (ver AsegurarFormaPrecisa) — sin esto los 4 patrones de
        // pared heredados del viejo Show de Láseres telegrafeaban de
        // verdad pero nunca hacían daño a la Forma Precisa.
        if (formaPrecisaActiva != null && area.Contains((Vector2)formaPrecisaActiva.transform.position))
        {
            // Fase 2 (punto 8, Super Administrador): EscaladaPatronesFase1
            // solo tiene sentido en 0-80s (queda clavada en 5 después, ver
            // su comentario) — sin esta rama, todo golpe de la Fase 2 se
            // etiquetaría como "laser_fase5" mezclado con los de verdad,
            // arruinando el balance por fase del punto 10.
            string razon = superAdministradorActivo
                ? $"laser_fase2_superadmin;climax={enSuperAdministradorClimaxNivel2}"
                : $"laser_fase{EscaladaPatronesFase1(tPelea)}";
            formaPrecisaActiva.RecibirGolpe(razon);
        }
    }

    /// <summary>
    /// Mismo trabajo que ResolverImpactoLaser(Rect), para paredes ROTADAS
    /// (revisión — patrones propios del boss en Fase 2, ver
    /// PatronTelaRadialFase2/PatronEspiralGiratoriaFase2). A propósito una
    /// función NUEVA en vez de generalizar la firma de la de arriba:
    /// ResolverImpactoLaser(Rect) es el camino que ya usan
    /// RafagaMurallaLaser, los 4 patrones heredados y VerificarMurallaLaser
    /// — no hay ningún motivo para arriesgarlos por una rotación que
    /// ninguno de ellos necesita. A ánguloGrados=0 el test de "adentro" acá
    /// abajo es matemáticamente idéntico a Rect.Contains sobre el mismo
    /// centro/tamaño (ver VerificarFase2PatronesPropios.cs, la prueba de
    /// equivalencia) — es la MISMA lógica, solo que en el espacio local de
    /// la pared en vez del espacio del mundo.
    /// </summary>
    public void ResolverImpactoLaserRotado(Vector2 centro, Vector2 tamano, float anguloGrados)
    {
        Vector2 mitad = tamano * 0.5f;
        Quaternion inversa = Quaternion.Euler(0f, 0f, -anguloGrados);
        bool Adentro(Vector2 punto)
        {
            Vector2 local = inversa * (Vector3)(punto - centro);
            return Mathf.Abs(local.x) <= mitad.x && Mathf.Abs(local.y) <= mitad.y;
        }

        foreach (var n in new List<Nucleo>(nucleosActivos))
        {
            if (n == null) continue;
            if (Adentro(n.transform.position)) ProcesarImpactoLaser(n);
        }

        if (formaPrecisaActiva != null && Adentro(formaPrecisaActiva.transform.position))
        {
            string razon = superAdministradorActivo
                ? $"laser_fase2_superadmin;climax={enSuperAdministradorClimaxNivel2};rotado=true"
                : $"laser_fase{EscaladaPatronesFase1(tPelea)};rotado=true";
            formaPrecisaActiva.RecibirGolpe(razon);
        }
    }

    void ProcesarImpactoLaser(Nucleo n)
    {
        if (n.golpeado) return;
        n.golpeado = true;

        EfectosVisuales.Instancia?.Chispas(n.transform.position, ColorLaser, 16, 3f);
        EfectosVisuales.Instancia?.Onda(n.transform.position, ColorLaser, n.radio * 3.5f);
        CameraPunch.Instancia?.Golpear();
        BeepSynth.Instancia?.Beep(80f, 0.2f, BeepSynth.Onda.Sierra, 0.22f);

        float nuevoRadio = n.radio * ratioAlPartirLaser;
        Vector3 pos = n.transform.position;
        Destroy(n.gameObject);
        Telemetria.Registrar(tiempo, "laser_golpe", nivelFever, comboOrbes, nucleosActivos.Count, orbesActivos);

        if (nuevoRadio < radioMinimo)
        {
            EfectosVisuales.Instancia?.Chispas(pos, new Color(0.35f, 0.31f, 0.42f), 16, 3f);
            return;
        }

        // Sin un "atacante" puntual (a diferencia de ProcesarImpacto), los
        // dos fragmentos salen despedidos en una dirección al azar.
        float anguloBase = Random.Range(0f, Mathf.PI * 2f);
        float apertura = 35f * Mathf.Deg2Rad;
        float separacion = nuevoRadio * 0.9f;
        Vector2 dirA = new Vector2(Mathf.Cos(anguloBase + apertura), Mathf.Sin(anguloBase + apertura));
        Vector2 dirB = new Vector2(Mathf.Cos(anguloBase - apertura), Mathf.Sin(anguloBase - apertura));
        CrearFragmento(pos + (Vector3)(dirA * separacion), nuevoRadio, dirA * fuerzaKnockback, golpeReciente: true);
        CrearFragmento(pos + (Vector3)(dirB * separacion), nuevoRadio, dirB * fuerzaKnockback, golpeReciente: true);
    }

    IEnumerator TelegraphYNacer(Vector2 punto)
    {
        EfectosVisuales.Instancia?.Telegraph(punto, telegraphDuracion);
        yield return new WaitForSeconds(telegraphDuracion);
        if (estado == EstadoJuego.Jugando) NacerEnemigo(punto);
    }

    Vector2 PuntoAleatorioDeBorde()
    {
        switch (Random.Range(0, 4))
        {
            case 0: return new Vector2(-mitadMundoAncho, Random.Range(-mitadMundoAlto, mitadMundoAlto));
            case 1: return new Vector2(mitadMundoAncho, Random.Range(-mitadMundoAlto, mitadMundoAlto));
            case 2: return new Vector2(Random.Range(-mitadMundoAncho, mitadMundoAncho), mitadMundoAlto);
            default: return new Vector2(Random.Range(-mitadMundoAncho, mitadMundoAncho), -mitadMundoAlto);
        }
    }

    void NacerEnemigo(Vector2 pos)
    {
        var e = Instantiate(enemigoPrefab, pos, Quaternion.identity);
        e.Inicializar(DificultadEnemigoActual);
    }

    // Reponer en cualquier punto del mundo (grande) hacía que armar una
    // cadena de combos fuera cuestión de suerte — la siguiente gema podía
    // aparecer del otro lado del mapa. Esto genera cerca de un punto dado
    // (el enjambre, o donde acabás de recolectar una gema) en vez de en
    // cualquier lugar, para que siempre haya algo cerca con qué seguir.
    // Un anillo (radioMin..radioMax), no un disco relleno: sin el mínimo,
    // Random.insideUnitCircle a veces tira la gema prácticamente encima
    // tuyo, sin que haga falta moverse ni un poco para el combo.
    void GenerarOrbeCercaDe(Vector2 centro, float radioMax, float radioMin = 0f)
    {
        // Sortea de nuevo (no clampea) si el punto cae fuera del mundo: al
        // clampear, cerca de una esquina el círculo de posibles ángulos se
        // "aplastaba" contra las dos paredes y las gemas terminaban
        // amontonadas ahí — un jugador plantado en una esquina podía
        // farmear Fever al toque, sin el equilibrio que se buscaba con el
        // anillo mínimo/máximo. Con un puñado de reintentos, casi siempre
        // se encuentra un ángulo que sí cae dentro del mundo.
        Vector2 pos = centro;
        const int intentosMax = 16;
        for (int intento = 0; intento < intentosMax; intento++)
        {
            float angulo = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(radioMin, radioMax);
            Vector2 candidato = centro + new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * dist;
            bool dentroDelMundo = Mathf.Abs(candidato.x) <= mitadMundoAncho * 0.95f && Mathf.Abs(candidato.y) <= mitadMundoAlto * 0.95f;
            if (dentroDelMundo) { pos = candidato; break; }
            if (intento == intentosMax - 1)
            {
                // Esquina extrema de verdad (o mundo muy chico): mejor
                // clampear que no generar nada.
                pos = candidato;
                pos.x = Mathf.Clamp(pos.x, -mitadMundoAncho * 0.95f, mitadMundoAncho * 0.95f);
                pos.y = Mathf.Clamp(pos.y, -mitadMundoAlto * 0.95f, mitadMundoAlto * 0.95f);
            }
        }
        Instantiate(orbePrefab, pos, Quaternion.identity);
        orbesActivos++;
    }

    IEnumerator GenerarOrbeCercaDespues(Vector2 centro, float demora)
    {
        yield return new WaitForSeconds(demora);
        GenerarOrbeCercaDe(centro, radioReposicionOrbe, radioReposicionMinimo);
    }

    IEnumerator OndaDemorada(Vector2 centro, Color color, float radioMax, float demora)
    {
        yield return new WaitForSeconds(demora);
        EfectosVisuales.Instancia?.Onda(centro, color, radioMax);
    }

    void ActualizarSpawnOrbeVelocidad()
    {
        tProximoOrbeVelocidad -= Time.deltaTime;
        if (tProximoOrbeVelocidad <= 0f)
        {
            Vector2 pos = new Vector2(
                Random.Range(-mitadMundoAncho * 0.8f, mitadMundoAncho * 0.8f),
                Random.Range(-mitadMundoAlto * 0.8f, mitadMundoAlto * 0.8f));
            Instantiate(orbePrefab, pos, Quaternion.identity).MarcarComoVelocidad();
            tProximoOrbeVelocidad = Random.Range(intervaloOrbeVelocidadMin, intervaloOrbeVelocidadMax);
        }
    }

    // Especial y raro (mismo patrón de spawn que el orbe de velocidad):
    // recompensa explorar en vez de quedarte en tu zona de confort — al
    // recogerlo hace estallar varias gemas normales alrededor tuyo,
    // regalando una cadena de combo instantánea.
    void ActualizarSpawnOrbeExplosivo()
    {
        tProximoOrbeExplosivo -= Time.deltaTime;
        if (tProximoOrbeExplosivo <= 0f)
        {
            Vector2 pos = new Vector2(
                Random.Range(-mitadMundoAncho * 0.8f, mitadMundoAncho * 0.8f),
                Random.Range(-mitadMundoAlto * 0.8f, mitadMundoAlto * 0.8f));
            Instantiate(orbePrefab, pos, Quaternion.identity).MarcarComoExplosivo();
            tProximoOrbeExplosivo = Random.Range(intervaloOrbeExplosivoMin, intervaloOrbeExplosivoMax);
        }
    }

    // Celebra cada tanto tiempo sobrevivido con un popup — sensación de
    // progreso concreta en partidas largas, más allá del contador del HUD.
    void ActualizarHitosTiempo()
    {
        if (siguienteHito >= hitosTiempo.Length) return;
        if (tiempo < hitosTiempo[siguienteHito]) return;

        EfectosVisuales.Instancia?.Popup(CentroDeMasa() + Vector2.up * 0.5f,
            hitosTiempo[siguienteHito].ToString("0") + "s!", new Color(0.176f, 0.910f, 1f));
        BeepSynth.Instancia?.Beep(660f, 0.1f, BeepSynth.Onda.Seno, 0.14f);
        siguienteHito++;
    }

    public Vector2 CentroDeMasa()
    {
        Vector2 acc = Vector2.zero; float pesoTotal = 0f;
        foreach (var n in nucleosActivos) { acc += (Vector2)n.transform.position * n.radio; pesoTotal += n.radio; }
        return pesoTotal > 0f ? acc / pesoTotal : Vector2.zero;
    }

    /// <summary>
    /// El núcleo más lejos del centro de masa — el "rezagado" que persigue
    /// Cazador en vez de ir directo al centro. Mismo costo O(n) que
    /// CentroDeMasa(), elegido UNA vez al nacer el Cazador (no cada
    /// frame) para no recalcularlo en cada Update.
    /// </summary>
    public Nucleo NucleoMasAislado()
    {
        Vector2 centro = CentroDeMasa();
        Nucleo masLejos = null;
        float distMax = -1f;
        foreach (var n in nucleosActivos)
        {
            float dist = ((Vector2)n.transform.position - centro).sqrMagnitude;
            if (dist > distMax) { distMax = dist; masLejos = n; }
        }
        return masLejos;
    }

    public void RegistrarNucleo(Nucleo n) => nucleosActivos.Add(n);
    public void DesregistrarNucleo(Nucleo n) => nucleosActivos.Remove(n);

    public void RegistrarEnemigo(Enemigo e) => enemigosActivos.Add(e);
    public void DesregistrarEnemigo(Enemigo e) => enemigosActivos.Remove(e);
    bool HayLugarParaMasEnemigos => enemigosActivos.Count < maxEnemigosActivos;

    // Grid espacial para la separación de núcleos: comparar cada núcleo
    // contra TODOS los demás (O(n²)) se sentía bien con 20 fragmentos pero
    // laggeaba de verdad con 300 (90.000 pares por frame, cada uno con dos
    // accesos a Transform.position + una raíz cuadrada). Reconstruido una
    // vez por frame (no una vez por núcleo) en Update(); cada núcleo solo
    // compara contra los que caen en su propia celda o una adyacente.
    const float tamanoCeldaSeparacion = 2f; // por encima del distMinima máximo posible (radio 0.8+0.8 * margen 1.15 ≈ 1.84)
    readonly Dictionary<(int, int), List<Nucleo>> gridSeparacion = new Dictionary<(int, int), List<Nucleo>>();

    (int, int) CeldaDeSeparacion(Vector2 pos) =>
        (Mathf.FloorToInt(pos.x / tamanoCeldaSeparacion), Mathf.FloorToInt(pos.y / tamanoCeldaSeparacion));

    void ActualizarGridSeparacion()
    {
        gridSeparacion.Clear();
        foreach (var n in nucleosActivos)
        {
            var celda = CeldaDeSeparacion(n.transform.position);
            if (!gridSeparacion.TryGetValue(celda, out var lista))
            {
                lista = new List<Nucleo>();
                gridSeparacion[celda] = lista;
            }
            lista.Add(n);
        }
    }

    /// <summary>Núcleos en la misma celda que pos o en una de las 8 adyacentes (ver grid de arriba).</summary>
    public IEnumerable<Nucleo> VecinosCercanos(Vector2 pos)
    {
        var (cx, cy) = CeldaDeSeparacion(pos);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (gridSeparacion.TryGetValue((cx + dx, cy + dy), out var lista))
                    foreach (var n in lista)
                        yield return n;
    }

    /// <summary>
    /// 1 con pocos núcleos activos, baja hasta factorEscalaMinimoPorCantidad
    /// a medida que hay más (ver header "Achique por cantidad"). Nucleo.cs
    /// lo aplica tanto a su escala visual/de colisión como a la distancia
    /// de separación que le pide al resto, así que ambas cosas se achican
    /// juntas — un enjambre grande ocupa menos área en vez de crecer sin
    /// límite.
    /// </summary>
    public float FactorEscalaPorCantidad
    {
        get
        {
            int n = nucleosActivos.Count;
            if (n <= cantidadDondeEmpiezaAchique) return 1f;
            float t = Mathf.Clamp01((n - cantidadDondeEmpiezaAchique) / (float)(cantidadParaEscalaMinima - cantidadDondeEmpiezaAchique));
            return Mathf.Lerp(1f, factorEscalaMinimoPorCantidad, t);
        }
    }

    public void ProcesarImpacto(Nucleo n, GameObject enemigoObj)
    {
        if (n.golpeado) return;

        var enemigo = enemigoObj.GetComponent<Enemigo>();
        if (enemigo != null)
        {
            if (enemigo.golpeado) return;
            enemigo.golpeado = true;
        }
        n.golpeado = true;

        EfectosVisuales.Instancia?.Chispas(n.transform.position, new Color(0.48f, 0.06f, 0.19f), 12, 2.4f);
        EfectosVisuales.Instancia?.Onda(n.transform.position, new Color(1f, 0.184f, 0.373f), n.radio * 3.2f);
        CameraPunch.Instancia?.Golpear();
        BeepSynth.Instancia?.Beep(140f, 0.16f, BeepSynth.Onda.Sierra, 0.18f);
        HitStop();

        Vector3 posEnemigo = enemigoObj.transform.position;
        Destroy(enemigoObj);

        float nuevoRadio = n.radio * ratioAlPartir;
        Vector3 pos = n.transform.position;
        Destroy(n.gameObject);

        if (nuevoRadio < radioMinimo)
        {
            EfectosVisuales.Instancia?.Chispas(pos, new Color(0.35f, 0.31f, 0.42f), 16, 3f);
            Telemetria.Registrar(tiempo, "nucleo_disuelto", nivelFever, comboOrbes, nucleosActivos.Count, orbesActivos);
            return;
        }

        // Los dos fragmentos salen despedidos lejos de donde pegó el
        // enemigo (no en una dirección al azar), con una leve apertura
        // entre ambos para que no viajen pegados uno al otro.
        Vector2 dirGolpe = pos - posEnemigo;
        float anguloBase = dirGolpe.sqrMagnitude > 0.0001f
            ? Mathf.Atan2(dirGolpe.y, dirGolpe.x)
            : Random.Range(0f, Mathf.PI * 2f);
        float apertura = 35f * Mathf.Deg2Rad;
        float separacion = nuevoRadio * 0.9f;
        Vector2 dirA = new Vector2(Mathf.Cos(anguloBase + apertura), Mathf.Sin(anguloBase + apertura));
        Vector2 dirB = new Vector2(Mathf.Cos(anguloBase - apertura), Mathf.Sin(anguloBase - apertura));
        CrearFragmento(pos + (Vector3)(dirA * separacion), nuevoRadio, dirA * fuerzaKnockback, golpeReciente: true);
        CrearFragmento(pos + (Vector3)(dirB * separacion), nuevoRadio, dirB * fuerzaKnockback, golpeReciente: true);
    }

    // Congela el juego un instante muy breve (en tiempo real, no afecta
    // la duración) para que el golpe se sienta con más peso — el clásico
    // "hit-stop" de los juegos de pelea/acción.
    //
    // Antes esto era una corrutina que StopCoroutine() interrumpía si
    // llegaba un golpe nuevo antes de que la anterior terminara — y esa
    // interrupción se saltaba la línea que restauraba Time.timeScale=1.
    // Con golpes seguidos (que es justo cuando más efectos se disparan),
    // el timeScale quedaba pegado en escalaHitStop para siempre, y con
    // eso todo lo basado en Time.deltaTime —incluida la cuenta regresiva
    // de SimpleParticle— casi se congelaba: por eso las chispas parecían
    // "acumularse sin destruirse". Ahora es un simple plazo en tiempo
    // real que cualquier golpe nuevo extiende, sin corrutina que
    // interrumpir.
    float tiempoRealFinHitStop;

    void HitStop()
    {
        tiempoRealFinHitStop = Time.realtimeSinceStartup + duracionHitStop;
        Time.timeScale = escalaHitStop;
    }

    void ActualizarHitStop()
    {
        if (Time.timeScale < 1f && Time.realtimeSinceStartup >= tiempoRealFinHitStop)
            Time.timeScale = 1f;
    }

    void CrearFragmento(Vector3 pos, float radio, Vector2 impulso, bool golpeReciente = false)
    {
        var n = Instantiate(nucleoPrefab, pos, Quaternion.identity);
        n.radio = radio;
        n.velocidad = impulso;
        if (golpeReciente) n.MostrarGolpeado(duracionFlashGolpeado);
    }

    public void ProcesarPickupOrbe(Nucleo n, Orbe o)
    {
        if (o.recolectado) return;
        o.recolectado = true;

        if (o.esVelocidad)
        {
            var colorVel = new Color(1f, 0.85f, 0.1f);
            tBoostVelocidad = duracionBoostVelocidad;
            EfectosVisuales.Instancia?.Chispas(o.transform.position, colorVel, 16, 3f);
            EfectosVisuales.Instancia?.Onda(o.transform.position, colorVel, 1.4f);
            EfectosVisuales.Instancia?.Popup(o.transform.position, "+VELOCIDAD", colorVel);
            BeepSynth.Instancia?.Beep(1200f, 0.05f, BeepSynth.Onda.Cuadrada, 0.18f);
            BeepSynth.Instancia?.Beep(1600f, 0.08f, BeepSynth.Onda.Cuadrada, 0.14f);
            Destroy(o.gameObject);
            return;
        }

        if (o.esExplosivo)
        {
            var colorExp = new Color(0.65f, 0.3f, 1f);
            Vector2 centro = o.transform.position;
            EfectosVisuales.Instancia?.Chispas(centro, colorExp, 30, 4.4f);
            // Doble onda (una rápida y chica, otra más grande con un
            // pelín de demora) en vez de una sola — se lee como una
            // explosión de verdad, no como un pickup más.
            EfectosVisuales.Instancia?.Onda(centro, colorExp, radioExplosion * 0.9f);
            StartCoroutine(OndaDemorada(centro, colorExp, radioExplosion * 1.8f, 0.1f));
            EfectosVisuales.Instancia?.Popup(centro, "¡EXPLOSIÓN!", colorExp, 1.8f);
            CameraPunch.Instancia?.Golpear();
            BeepSynth.Instancia?.Beep(220f, 0.12f, BeepSynth.Onda.Sierra, 0.2f);
            BeepSynth.Instancia?.Beep(880f, 0.1f, BeepSynth.Onda.Cuadrada, 0.16f);
            Destroy(o.gameObject);
            for (int i = 0; i < orbesPorExplosion; i++) GenerarOrbeCercaDe(centro, radioExplosion, radioExplosionMinimo);
            return;
        }

        comboOrbes = (Time.time - tUltimoOrbe < ventanaCombo) ? comboOrbes + 1 : 1;
        tUltimoOrbe = Time.time;
        if (comboOrbes > comboMaximo) comboMaximo = comboOrbes;

        // Cada comboParaMultiplicador gemas seguidas (sin cortar la
        // ventana de combo) arranca/renueva un boost temporal de
        // crecimiento — recompensa mantener el ritmo, no un multiplicador
        // permanente que se acumule partida tras partida.
        bool hitoCombo = comboOrbes % comboParaMultiplicador == 0;
        if (hitoCombo) tBoostCrecimiento = duracionBoostCrecimiento;

        // Fever (estilo DJMAX): cada comboPorNivelFever (re)arranca el
        // cronómetro de duracionFever — si ya estaba en nivel máximo, esto
        // solo lo renueva; si no, también sube un nivel (tope
        // nivelFeverMax) y completa las gemas extra de ese nivel al toque
        // en vez de esperar a que se repongan una por una.
        bool hitoFever = comboOrbes % comboPorNivelFever == 0;
        if (hitoFever)
        {
            tFever = DuracionFeverEfectivo;
            if (nivelFever < nivelFeverMax) nivelFever++;
            while (orbesActivos < ObjetivoOrbesEfectivo) GenerarOrbeCercaDe(o.transform.position, radioReposicionOrbe, radioReposicionMinimo);
            Telemetria.Registrar(tiempo, "fever_hito", nivelFever, comboOrbes, nucleosActivos.Count, orbesActivos);
        }

        Telemetria.Registrar(tiempo, "gema", nivelFever, comboOrbes, nucleosActivos.Count, orbesActivos);

        float crecimiento = CrecimientoPorOrbeEfectivo * (tBoostCrecimiento > 0f ? multiplicadorBoostCrecimiento : 1f);
        n.Crecer(crecimiento);

        var cian = new Color(0.176f, 0.910f, 1f);
        EfectosVisuales.Instancia?.Chispas(o.transform.position, cian, 10, 1.6f);
        EfectosVisuales.Instancia?.Onda(o.transform.position, cian, 0.9f);
        if (hitoFever)
        {
            var colorFever = new Color(1f, 0.35f, 0.65f);
            EfectosVisuales.Instancia?.Popup(o.transform.position, $"FEVER x{nivelFever}!", colorFever, 1.6f);
            EfectosVisuales.Instancia?.Onda(o.transform.position, colorFever, 2f);
            BeepSynth.Instancia?.Beep(660f, 0.14f, BeepSynth.Onda.Cuadrada, 0.22f);
            BeepSynth.Instancia?.Beep(990f, 0.12f, BeepSynth.Onda.Cuadrada, 0.18f);
            BeepSynth.Instancia?.Beep(1320f, 0.1f, BeepSynth.Onda.Cuadrada, 0.16f);
        }
        else if (hitoCombo)
        {
            var colorCrecimiento = new Color(1f, 0.55f, 0.15f);
            EfectosVisuales.Instancia?.Popup(o.transform.position, $"¡CRECIMIENTO x{multiplicadorBoostCrecimiento:0}!", colorCrecimiento, 1.4f);
            EfectosVisuales.Instancia?.Onda(o.transform.position, colorCrecimiento, 1.6f);
            BeepSynth.Instancia?.Beep(520f, 0.12f, BeepSynth.Onda.Cuadrada, 0.2f);
            BeepSynth.Instancia?.Beep(780f, 0.1f, BeepSynth.Onda.Cuadrada, 0.16f);
        }
        else if (comboOrbes >= 2)
        {
            float escalaCombo = 1f + Intensidad * 0.3f;
            EfectosVisuales.Instancia?.Popup(o.transform.position, $"COMBO x{comboOrbes}", new Color(1f, 0.85f, 0.1f), escalaCombo);
            BeepSynth.Instancia?.Beep(880f + comboOrbes * 70f, 0.07f, BeepSynth.Onda.Seno, 0.18f);
        }
        else
        {
            EfectosVisuales.Instancia?.Popup(o.transform.position, "+MASA", cian);
            BeepSynth.Instancia?.Beep(880f, 0.06f, BeepSynth.Onda.Seno, 0.16f);
        }
        Vector2 posOrbe = o.transform.position;
        Destroy(o.gameObject);
        orbesActivos--;
        // Repone solo hasta el objetivo efectivo actual (que sube y baja
        // con nivelFever) — si el Fever recién decayó, esto simplemente
        // deja de reponer hasta que la cantidad en pantalla se acomode
        // sola, sin tener que destruir ninguna gema ya generada. Y repone
        // cerca de donde recién agarraste esta (no en cualquier lugar del
        // mundo grande) para que la siguiente esté a mano y se pueda
        // seguir la cadena de combo.
        if (orbesActivos < ObjetivoOrbesEfectivo) StartCoroutine(GenerarOrbeCercaDespues(posOrbe, 0.4f));
    }

    /// <summary>Un Enjambre Rival (ver Enemigo.TipoAtaque.Rival) tocó una gema antes que vos — se pierde, no se cobra combo/crecimiento.</summary>
    public void ProcesarGemaRobada(Orbe o)
    {
        if (o.recolectado) return;
        o.recolectado = true;

        var colorRobo = new Color(0.55f, 0.15f, 0.7f);
        EfectosVisuales.Instancia?.Chispas(o.transform.position, colorRobo, 10, 1.8f);
        EfectosVisuales.Instancia?.Popup(o.transform.position, "¡ROBADA!", colorRobo);
        BeepSynth.Instancia?.Beep(180f, 0.1f, BeepSynth.Onda.Sierra, 0.16f);
        Telemetria.Registrar(tiempo, "gema_robada", nivelFever, comboOrbes, nucleosActivos.Count, orbesActivos);

        Vector2 posOrbe = o.transform.position;
        Destroy(o.gameObject);
        orbesActivos--;
        if (orbesActivos < ObjetivoOrbesEfectivo) StartCoroutine(GenerarOrbeCercaDespues(posOrbe, 0.4f));
    }

    // Destruye todo lo que quede de la partida anterior y resetea los
    // timers — lo comparten EmpezarPartida() y BotonVolverAlMenu(), así
    // que volver al menú desde Panel Fin deja el estado tan limpio como
    // arrancar una partida nueva.
    void LimpiarPartida()
    {
        foreach (var n in new List<Nucleo>(nucleosActivos)) Destroy(n.gameObject);
        nucleosActivos.Clear();
        foreach (var e in FindObjectsByType<Enemigo>(FindObjectsSortMode.None)) Destroy(e.gameObject);
        foreach (var o in FindObjectsByType<Orbe>(FindObjectsSortMode.None)) Destroy(o.gameObject);
        // Nivel 2: la Forma Precisa y el boss no son Nucleo/Enemigo/Orbe,
        // así que sin esto quedaban vivos (leak) al volver al menú a mitad
        // de la cutscene o la pelea — encontrado revisando el camino
        // Pausa -> Menú durante Nivel 2.
        if (formaPrecisaActiva != null)
        {
            formaPrecisaActiva.AlQuedarSinVidas -= ManejarDerrotaNivel2;
            Destroy(formaPrecisaActiva.gameObject);
            formaPrecisaActiva = null;
        }
        if (administradorActivo != null) { Destroy(administradorActivo.gameObject); administradorActivo = null; }
        if (formaNivel3Activa != null) { Destroy(formaNivel3Activa); formaNivel3Activa = null; }
        modoNivel2 = false;
        PeleaActiva = false;
        tPelea = 0f;
        energiaNivel2 = 0f;
        tInicioCargaNivel2 = 0f;
        tCooldownPulsoPotenteRestanteNivel2 = 0f;
        vidaBossNivel2 = 0f;
        bossColapsadoNivel2 = false;
        animandoPresentacionVidaBossNivel2 = false;
        if (panelVidaBossNivel2 != null) panelVidaBossNivel2.SetActive(false);
        // Fase 7 — igual que enCutsceneCierre/saltandoCutscene más abajo:
        // se resetea todo lo que es propio de ESTA partida, pero NO
        // escaladaPrivilegiosVistaSesion (mismo motivo que
        // cutsceneAperturaVistaSesion, ver su comentario).
        enEscaladaPrivilegiosNivel2 = false;
        saltandoEscaladaPrivilegiosNivel2 = false;
        escaladaPrivilegiosCoroutine = null;
        superAdministradorActivo = false;
        enSuperAdministradorClimaxNivel2 = false;
        if (panelTerminalNivel2 != null) panelTerminalNivel2.SetActive(false);
        if (botonSaltarTerminalNivel2 != null) botonSaltarTerminalNivel2.SetActive(false);
        LimpiarProyectiles();
        saltandoCutscene = false;
        cutsceneCoroutine = null;
        enCutsceneCierre = false;
        if (panelCutsceneNivel2 != null) panelCutsceneNivel2.SetActive(false);
        if (botonSaltarCutscene != null) botonSaltarCutscene.SetActive(false);

        // Cancela cualquier reposición de gema pendiente (Invoke/corrutina)
        // de la partida anterior — si no, una que quedó en el aire podía
        // aparecer recién en el menú o ya empezada la partida nueva.
        CancelInvoke();
        StopAllCoroutines(); // esto incluye Fundir() si estaba a mitad de un fundido — ver el reseteo de imagenFundido debajo

        // Bug real (encontrado en revisión, no en juego): StopAllCoroutines()
        // de arriba puede matar a Fundir() a mitad del desvanecido (p.ej.
        // Pausa -> Menú durante los 0.35s de reveal tras entrar a la
        // cutscene) sin que llegue a correr su propio SetActive(false) —
        // mismo tipo de leak que ya se vio con la Forma Precisa/el boss más
        // arriba. Sin este reseteo explícito quedaba un overlay negro
        // semi-transparente pegado sobre el menú, sin nada corriendo que lo
        // fuera a limpiar.
        if (imagenFundido != null)
        {
            var c = imagenFundido.color; c.a = 0f; imagenFundido.color = c;
            imagenFundido.gameObject.SetActive(false);
        }

        tiempo = 0f; tSpawnEnemigo = 0f; tSpawnCazador = 0f; tSpawnCoagulo = 0f; tSpawnRival = 0f; tBoostVelocidad = 0f; tBoostCrecimiento = 0f;
        victoriaDisparada = false;
        secuenciaVictoriaActiva = false;
        tProximoOrbeVelocidad = Random.Range(intervaloOrbeVelocidadMin, intervaloOrbeVelocidadMax);
        tProximoOrbeExplosivo = Random.Range(intervaloOrbeExplosivoMin, intervaloOrbeExplosivoMax);
        tProximoEventoLaser = tiempoPrimerEventoLaser;
        comboOrbes = 0; tUltimoOrbe = -99f; siguienteHito = 0; nivelFever = 1;
        tFever = 0f; tDecaimientoFever = 0f; orbesActivos = 0;
        comboMaximo = 0; nucleosMaximo = 0;
        tiempoRealFinHitStop = 0f;
        Time.timeScale = 1f;
    }

    public void EmpezarPartida()
    {
        LimpiarPartida();

        estado = EstadoJuego.Jugando;
        if (panelMenu != null) panelMenu.SetActive(false);
        if (panelNiveles != null) panelNiveles.SetActive(false);
        if (panelFin != null) panelFin.SetActive(false);
        if (hudRoot != null) hudRoot.SetActive(true);
        if (modoNivel2) MusicManager.Instancia?.ReproducirNivel2();
        else MusicManager.Instancia?.ReproducirJuego();

        CrearFragmento(Vector3.zero, RadioInicialEfectivo, Vector2.zero);
        // Semilla inicial cerca del punto de partida (no en cualquier lugar
        // del mundo grande) — para que desde el primer segundo haya con
        // qué armar un combo, en vez de tener que salir a buscar la
        // primera gema.
        for (int i = 0; i < orbesObjetivo; i++) GenerarOrbeCercaDe(Vector2.zero, mitadAncho * 0.9f);

        Telemetria.IniciarPartida();
        Telemetria.Registrar(0f, modoNivel2 ? "inicio_nivel2" : "inicio", nivelFever, comboOrbes, nucleosActivos.Count, orbesActivos);
    }

    /// <summary>Botón "MENÚ" de Panel Fin o de Pausa: corta la partida y vuelve al menú sin arrancar a jugar.</summary>
    public void BotonVolverAlMenu()
    {
        // Si se vuelve al menú sin haber muerto (Escape -> Menú desde
        // Pausa), TerminarPartida() nunca corrió — sin esto el archivo de
        // esta partida se quedaba abierto hasta que la siguiente empezara.
        if (estado == EstadoJuego.Jugando || estado == EstadoJuego.Pausado)
            Telemetria.Registrar(tiempo, "salida_a_menu", nivelFever, comboOrbes, nucleosActivos.Count, orbesActivos);
        Telemetria.CerrarSiHabiaAlguna();

        LimpiarPartida();

        estado = EstadoJuego.Menu;
        if (panelFin != null) panelFin.SetActive(false);
        if (panelPausa != null) panelPausa.SetActive(false);
        if (panelMenu != null) panelMenu.SetActive(true);
        if (hudRoot != null) hudRoot.SetActive(false);
        MusicManager.Instancia?.ReproducirMenu();
    }

    /// <summary>Tecla Escape (o el botón "Pausa" si lo hubiera) durante la partida.</summary>
    public void PausarPartida()
    {
        if (estado != EstadoJuego.Jugando) return;
        estado = EstadoJuego.Pausado;
        if (panelPausa != null) panelPausa.SetActive(true);
    }

    public void ReanudarPartida()
    {
        if (estado != EstadoJuego.Pausado) return;
        estado = EstadoJuego.Jugando;
        if (panelPausa != null) panelPausa.SetActive(false);
    }

    /// <summary>Botón "Salir" del menú principal: cierra el juego (en el Editor, sale de Play Mode).</summary>
    public void BotonSalir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Final de la demo: NO es una muerte (el enjambre sobrevive, no llega
    // a 0 núcleos) — una onda expansiva barre todo lo demás (enemigos,
    // paredes de láser a medio telegrafear, gemas), se abre un portal en
    // el centro y se pasa a Créditos con un mensaje de agradecimiento en
    // vez del título normal.
    void DispararVictoria()
    {
        victoriaDisparada = true;
        secuenciaVictoriaActiva = true;
        Telemetria.Registrar(tiempo, "victoria_disparada", nivelFever, comboOrbes, nucleosActivos.Count, orbesActivos);
        // `estado` se queda en Jugando a propósito: el jugador todavía
        // tiene que poder mover el enjambre para guiarlo adentro del
        // portal (ver el while de más abajo). Recién pasa a Victoria
        // cuando entra de verdad.
        StartCoroutine(SecuenciaVictoria());
    }

    IEnumerator SecuenciaVictoria()
    {
        Vector2 centro = CentroDeMasa();

        // --- 1. Onda expansiva: barre de ADENTRO hacia AFUERA, no todo
        // junto de golpe — cada objeto explota cuando "le toca" según su
        // distancia al centro, para que se sienta como una onda de verdad
        // limpiando la pantalla en vez de un chasquido instantáneo. ---
        var objetivos = new List<(GameObject go, float dist, System.Action antesDeDestruir)>();
        foreach (var e in new List<Enemigo>(enemigosActivos))
            if (e != null) { var eCap = e; objetivos.Add((e.gameObject, Vector2.Distance(e.transform.position, centro), () => eCap.golpeado = true)); }
        foreach (var h in FindObjectsByType<LaserHazard>(FindObjectsSortMode.None))
            if (h != null) objetivos.Add((h.gameObject, Vector2.Distance(h.transform.position, centro), null));
        foreach (var o in FindObjectsByType<Orbe>(FindObjectsSortMode.None))
            if (o != null) { var oCap = o; objetivos.Add((o.gameObject, Vector2.Distance(o.transform.position, centro), () => oCap.recolectado = true)); }

        const float duracionOnda = 0.9f;
        float distMax = objetivos.Count > 0 ? objetivos.Max(x => x.dist) : 0f;
        float velocidadOnda = distMax > 0.01f ? distMax / duracionOnda : 1f;

        EfectosVisuales.Instancia?.Onda(centro, new Color(2f, 1.6f, 3f), Mathf.Max(mitadAncho, mitadAlto) * 1.6f);
        EfectosVisuales.Instancia?.Chispas(centro, new Color(1f, 0.9f, 1f), 30, 5f);
        CameraPunch.Instancia?.Golpear();
        BeepSynth.Instancia?.Beep(60f, 0.5f, BeepSynth.Onda.Seno, 0.3f);
        BeepSynth.Instancia?.Beep(90f, 0.4f, BeepSynth.Onda.Sierra, 0.25f);

        // golpeado/recolectado se marcan ANTES de Destroy() — Destroy() no
        // saca el objeto de la escena en el mismo frame (ver el mismo
        // patrón ya usado en ProcesarImpacto/ProcesarGemaRobada), así que
        // cualquier código que consulte "¿sigue vivo?" en este mismo
        // instante debe mirar el flag, no la presencia del objeto.
        foreach (var (go, dist, antes) in objetivos)
            StartCoroutine(DestruirConRetraso(go, dist / velocidadOnda, antes));
        orbesActivos = 0;

        yield return new WaitForSeconds(duracionOnda + 0.25f);

        // --- 2. Portal: se abre LENTO y acotado al área jugable visible
        // (mitadAncho/mitadAlto — no al mundo grande, que es varias veces
        // más ancho que lo que se ve en cámara), y el jugador tiene que
        // mover el enjambre adentro para continuar — no es automático. ---
        var portalGo = new GameObject("Portal");
        portalGo.transform.position = centro;
        var srPortal = portalGo.AddComponent<SpriteRenderer>();
        srPortal.sortingOrder = 20;
        // Punto 6 (shaders): un vórtice de verdad en vez de un disco
        // liso — pedido explícito, "el player se mete debajo de un
        // sprite gigante nada más". Assets/Shaders/PortalVortice.shader
        // gira brazos en espiral con huecos reales entre ellos (no un
        // relleno parejo), así que el jugador se ve DE VERDAD a través
        // del vórtice al entrar, no desaparece detrás de un círculo
        // opaco. srPortal.color sigue manejando el lerp oscuro->vívido
        // de abajo sin cambios (el shader lo multiplica igual que
        // cualquier SpriteRenderer.color).
        if (materialPortal != null)
        {
            srPortal.sprite = ShapeFactory.Cuadrado(8, Color.white); // solo portador de malla/UV
            srPortal.sharedMaterial = materialPortal;
        }
        else
        {
            srPortal.sprite = ShapeFactory.Ficha(64, Color.white, Color.white, 0);
        }
        srPortal.color = new Color(0.3f, 0.1f, 0.6f);

        BeepSynth.Instancia?.Beep(220f, 0.4f, BeepSynth.Onda.Cuadrada, 0.2f);
        const float duracionPortal = 2.6f;
        float escalaFinal = Mathf.Min(mitadAncho, mitadAlto) * 1.5f;
        float t = 0f;
        while (t < duracionPortal)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duracionPortal);
            float escala = Mathf.Lerp(0f, escalaFinal, p * p);
            portalGo.transform.localScale = new Vector3(escala, escala, 1f);
            srPortal.color = Color.Lerp(new Color(0.3f, 0.1f, 0.6f), new Color(2.5f, 1.8f, 3.2f), p);
            yield return null;
        }
        BeepSynth.Instancia?.Beep(660f, 0.3f, BeepSynth.Onda.Cuadrada, 0.2f);
        EfectosVisuales.Instancia?.Popup(centro + new Vector2(0f, escalaFinal * 0.5f + 0.6f), "¡Entrá al portal!", new Color(0.85f, 0.7f, 1f), 1.3f);

        // Sigue pudiendo mover el enjambre (LeerInput sigue activo, ver el
        // gate en Update()) hasta que decide entrar de verdad — un pulso
        // idle sutil mientras espera, para que no se sienta una imagen
        // congelada.
        float radioEntrada = escalaFinal * 0.5f * 0.55f;
        while (Vector2.Distance(CentroDeMasa(), centro) > radioEntrada)
        {
            float pulso = 1f + Mathf.Sin(Time.time * 2.2f) * 0.04f;
            portalGo.transform.localScale = new Vector3(escalaFinal * pulso, escalaFinal * pulso, 1f);
            yield return null;
        }

        BeepSynth.Instancia?.Beep(880f, 0.3f, BeepSynth.Onda.Cuadrada, 0.22f);
        BeepSynth.Instancia?.Beep(1320f, 0.25f, BeepSynth.Onda.Cuadrada, 0.18f);
        Destroy(portalGo);

        secuenciaVictoriaActiva = false;
        Telemetria.Registrar(tiempo, "victoria_entrada_portal", nivelFever, comboOrbes, nucleosActivos.Count, orbesActivos);

        // Llegar hasta acá de verdad (no solo comprar mejoras) es lo que
        // desbloquea el Nivel 2 y la Forma Precisa — se guarda apenas se
        // cruza el portal, no hace falta ver nada más ni volver a hacerlo
        // en partidas futuras (ambos Desbloquear* son idempotentes).
        MetaProgreso.DesbloquearNivel2();
        MetaProgreso.DesbloquearFormaPrecisa();

        // Fundido a negro ANTES de la cutscene (Fase 1, punto 4 — "no
        // corte duro"): EntrarCutsceneNivel2() hace todo su setup síncrono
        // (reposicionar enjambre, instanciar boss) ya tapado por este
        // negro, y se encarga de desvanecerlo de nuevo al final. No
        // tickeable en batch mode más allá del primer yield, mismo límite
        // que el resto de esta corrutina (ver el comentario del header de
        // este archivo).
        yield return StartCoroutine(Fundir(1f, duracionFundido));

        EntrarCutsceneNivel2();
    }

    IEnumerator DestruirConRetraso(GameObject go, float delay, System.Action antesDeDestruir)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (go == null) yield break;
        antesDeDestruir?.Invoke();
        EfectosVisuales.Instancia?.Chispas(go.transform.position, new Color(1.6f, 1.2f, 2f), 8, 2f);
        Destroy(go);
    }

    // ===================== NIVEL 2 — BOSS FIGHT (en construcción) =====================
    // Punto de entrada único, tanto desde el portal de Victoria (primera
    // vez, con el enjambre real todavía vivo) como desde el botón
    // "Nivel 2"/"Niveles" del menú (atajo, sin enjambre) — ver
    // BotonJugarNivel2. Arma (si hace falta) un enjambre mínimo, arranca
    // en modo Nivel 2, y dispara la cutscene de apertura — o la saltea
    // entera si ya se vio antes EN ESTA SESIÓN (cutsceneAperturaVistaSesion).
    void EntrarCutsceneNivel2()
    {
        // Corte a negro SINCRÓNICO — cubre el setup instantáneo de acá
        // abajo (reposición del enjambre, instanciar el boss, etc.) sea
        // cual sea el camino (primera vez o salto directo a la pelea).
        // Se desvanece animado al final de este método (Fundir), así el
        // jugador nunca ve un corte duro de Nivel 1 a Nivel 2. Si además
        // se llega desde SecuenciaVictoria(), esa corrutina ya venía
        // fundiendo a negro antes de llamar acá — este SetActive es
        // idempotente, no reinicia nada.
        if (imagenFundido != null)
        {
            imagenFundido.gameObject.SetActive(true);
            var c = imagenFundido.color; c.a = 1f; imagenFundido.color = c;
        }

        // Si venimos del portal de Victoria el enjambre real sigue vivo a
        // propósito (es el que se transforma en la cutscene) — LimpiarPartida()
        // lo destruiría, así que solo se llama cuando hace falta armar uno
        // desde cero (atajo de menú, sin haber jugado Nivel 1 todavía).
        if (nucleosActivos.Count == 0)
        {
            LimpiarPartida();
            CrearFragmento(Vector3.zero, RadioInicialEfectivo, Vector2.zero);
        }
        else
        {
            CancelInvoke();
            // Bug real reportado (Fase 1, punto 3): el jugador llegaba del
            // portal de Nivel 1 parado donde sea que hubiera cruzado, no en
            // la marca que espera el beat (a) de la cutscene (el boss
            // desciende relativo a este centro). Se re-centra ANTES del
            // primer beat, preservando la formación del enjambre (todos los
            // fragmentos se corren el mismo offset) — el fundido de Bug #4
            // (ver EntrarCutsceneNivel2 más abajo) tapa el salto.
            ReposicionarEnjambreAMarca(MarcaInicioCutsceneNivel2);
        }

        modoNivel2 = true;
        estado = EstadoJuego.Cutscene;
        enCutsceneCierre = false;
        if (panelMenu != null) panelMenu.SetActive(false);
        if (panelNiveles != null) panelNiveles.SetActive(false);
        if (panelFin != null) panelFin.SetActive(false);
        if (hudRoot != null) hudRoot.SetActive(false);
        MusicManager.Instancia?.Detener();
        Telemetria.Registrar(tiempo, "cutscene_nivel2_inicio", nivelFever, comboOrbes, nucleosActivos.Count, orbesActivos);

        if (cutsceneAperturaVistaSesion)
        {
            // Pedido explícito: si ya se vio una vez EN ESTA SESIÓN, reintentar
            // tras morir no la vuelve a tragar entera. A propósito no es
            // MetaProgreso (persistente) — ver el comentario del campo.
            Telemetria.Registrar(tiempo, "cutscene_nivel2_salteada_ya_vista", nivelFever, comboOrbes, nucleosActivos.Count, orbesActivos);
            FinalizarCutsceneAperturaInstantanea();
        }
        else
        {
            cutsceneCoroutine = StartCoroutine(CutsceneAperturaNivel2());
        }

        // El setup de arriba (cualquiera de los dos caminos) ya terminó de
        // forma síncrona — recién ahora es seguro empezar a revelar.
        StartCoroutine(Fundir(0f, duracionFundido));
    }

    static readonly Vector2 MarcaInicioCutsceneNivel2 = Vector2.zero;

    /// <summary>Anima imagenFundido.color.a hacia destino en duracion segundos (tiempo real, no Time.deltaTime — mismo motivo que MusicManager.FadeVolumen). No-op si no hay imagenFundido asignado (escena vieja sin correr el setup). destino<=0 desactiva el GameObject al terminar, para no dejar un Image invisible pero con raycastTarget de sobra sobre el resto de la UI.</summary>
    IEnumerator Fundir(float destino, float duracion)
    {
        if (imagenFundido == null) yield break;
        float inicio = imagenFundido.color.a;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            var c = imagenFundido.color; c.a = Mathf.Lerp(inicio, destino, duracion > 0f ? t / duracion : 1f); imagenFundido.color = c;
            yield return null;
        }
        var cFinal = imagenFundido.color; cFinal.a = destino; imagenFundido.color = cFinal;
        if (destino <= 0f) imagenFundido.gameObject.SetActive(false);
    }

    /// <summary>Corre TODOS los fragmentos activos por el mismo offset para que el centro de masa quede exactamente en destino, sin romper la formación del enjambre. No-op si no hay fragmentos (p.ej. ya transformado a Forma Precisa).</summary>
    void ReposicionarEnjambreAMarca(Vector2 destino)
    {
        if (nucleosActivos.Count == 0) return;
        Vector2 offset = destino - CentroDeMasa();
        if (offset.sqrMagnitude < 0.0001f) return;
        foreach (var n in nucleosActivos)
        {
            if (n == null) continue;
            n.transform.position += (Vector3)offset;
        }
    }

    // Beats a-h del pedido original, en orden. El jugador no puede mover
    // nada mientras tanto (estado=Cutscene, ver el gate al inicio de
    // Update()) — FormaPrecisa/Nucleo también respetan ese mismo estado
    // en su propio Update(), así que no hace falta apagar nada aparte.
    IEnumerator CutsceneAperturaNivel2()
    {
        if (panelCutsceneNivel2 != null) panelCutsceneNivel2.SetActive(true);
        if (botonSaltarCutscene != null) botonSaltarCutscene.SetActive(true);

        Vector2 centro = CentroDeMasa();
        Vector2 posBoss = centro + new Vector2(0f, mitadAlto * 0.5f);

        administradorActivo = administradorPrefab != null
            ? Instantiate(administradorPrefab, centro + new Vector2(0f, mitadAlto + 2f), Quaternion.identity)
            : null;

        // a. Desciende el boss.
        BeepSynth.Instancia?.Beep(120f, 0.5f, BeepSynth.Onda.Seno, 0.22f);
        if (administradorActivo != null) yield return StartCoroutine(administradorActivo.Descender(posBoss, 1.1f));
        else yield return new WaitForSeconds(1.1f);
        CameraPunch.Instancia?.Golpear();

        yield return new WaitForSeconds(0.35f);

        // b/c. Burbujas.
        yield return MostrarDialogo("¿Qué eres?", 1.6f);
        yield return new WaitForSeconds(0.2f);
        yield return MostrarDialogo("¿Por qué no mueres?", 1.6f);
        yield return new WaitForSeconds(0.3f);

        // d. Amague de lanzamiento de hechizo.
        administradorActivo?.LanzarHechizo();
        BeepSynth.Instancia?.Beep(200f, 0.7f, BeepSynth.Onda.Sierra, 0.28f);
        EfectosVisuales.Instancia?.Onda(posBoss, new Color(2.2f, 0.4f, 2.6f), 1f);
        yield return new WaitForSeconds(0.9f);

        // e. El hechizo impacta y transforma al enjambre — el beat visual
        // más importante (ver AsegurarFormaPrecisa). Punto 6 lo va a
        // reforzar con un shader de glitch/dissolve; por ahora usa el
        // mismo lenguaje de partículas+shake+beep que el resto del juego.
        yield return TransformarEnjambreEnFormaPrecisa(centro);

        // f. Burbuja de sorpresa.
        yield return MostrarDialogo("¡¿?!", 1.1f);

        // g. El boss retrocede, reaccionando a que falló.
        administradorActivo?.Retroceder();
        BeepSynth.Instancia?.Beep(160f, 0.3f, BeepSynth.Onda.Seno, 0.2f);
        yield return new WaitForSeconds(0.6f);

        // h. Arranca la pelea (todavía sin construir, ver punto 4).
        TerminarCutsceneApertura();
    }

    IEnumerator MostrarDialogo(string texto, float duracion)
    {
        if (textoDialogoCutscene != null)
        {
            textoDialogoCutscene.text = texto;
            textoDialogoCutscene.gameObject.SetActive(true);
        }
        BeepSynth.Instancia?.Beep(300f, 0.05f, BeepSynth.Onda.Cuadrada, 0.12f);
        yield return new WaitForSeconds(duracion);
        if (textoDialogoCutscene != null) textoDialogoCutscene.gameObject.SetActive(false);
    }

    IEnumerator TransformarEnjambreEnFormaPrecisa(Vector2 centro)
    {
        EfectosVisuales.Instancia?.Onda(centro, new Color(2.2f, 0.4f, 2.6f), 1.2f);
        CameraPunch.Instancia?.Golpear();
        BeepSynth.Instancia?.Beep(50f, 0.4f, BeepSynth.Onda.Sierra, 0.3f);
        yield return new WaitForSeconds(0.15f);

        AsegurarFormaPrecisa(centro);

        yield return new WaitForSeconds(0.35f);
    }

    /// <summary>
    /// Destruye el enjambre y lo reemplaza por la Forma Precisa — idempotente
    /// (si ya existe, no hace nada), para poder llamarla tanto desde el
    /// beat normal de la cutscene como desde el salto instantáneo
    /// (FinalizarCutsceneAperturaInstantanea) sin duplicar la transformación.
    /// </summary>
    void AsegurarFormaPrecisa(Vector2 centro)
    {
        if (formaPrecisaActiva != null) return;

        foreach (var n in new List<Nucleo>(nucleosActivos))
        {
            if (n == null) continue;
            EfectosVisuales.Instancia?.Chispas(n.transform.position, new Color(1.8f, 1.5f, 2.2f), 6, 2f);
            Destroy(n.gameObject);
        }
        nucleosActivos.Clear();

        if (formaPrecisaPrefab != null) formaPrecisaActiva = Instantiate(formaPrecisaPrefab, centro, Quaternion.identity);

        EfectosVisuales.Instancia?.Onda(centro, new Color(1.7f, 2f, 2.3f), 1.6f);
        EfectosVisuales.Instancia?.Chispas(centro, new Color(1.7f, 2f, 2.3f), 24, 4f);
        CameraPunch.Instancia?.Golpear();
        BeepSynth.Instancia?.Beep(440f, 0.3f, BeepSynth.Onda.Cuadrada, 0.25f);
        BeepSynth.Instancia?.Beep(660f, 0.25f, BeepSynth.Onda.Cuadrada, 0.2f);

        Telemetria.Registrar(tiempo, "forma_precisa_transformacion", nivelFever, comboOrbes, 0, orbesActivos);
    }

    /// <summary>
    /// Botón "Saltar" — corta CUALQUIERA de las dos cutscenes de Nivel 2
    /// (apertura o cierre, nunca corren a la vez) donde esté y la termina
    /// de golpe, en vez de tickear el resto de los beats. enCutsceneCierre
    /// dice cuál de las dos está activa.
    /// </summary>
    public void SaltarCutscene()
    {
        if (estado != EstadoJuego.Cutscene || saltandoCutscene) return;
        saltandoCutscene = true;
        if (cutsceneCoroutine != null) StopCoroutine(cutsceneCoroutine);

        if (enCutsceneCierre)
        {
            Telemetria.Registrar(tPelea, "cutscene_nivel2_cierre_saltada_manual", nivelFever, comboOrbes, 0, orbesActivos);
            FinalizarCutsceneCierreInstantanea();
        }
        else
        {
            Telemetria.Registrar(tiempo, "cutscene_nivel2_saltada_manual", nivelFever, comboOrbes, nucleosActivos.Count, orbesActivos);
            FinalizarCutsceneAperturaInstantanea();
        }
    }

    /// <summary>Deja todo en el estado que la cutscene completa hubiera dejado, sin tickear los tweens/esperas — usado tanto por el salto manual como por "ya la vi antes".</summary>
    void FinalizarCutsceneAperturaInstantanea()
    {
        Vector2 centro = CentroDeMasa();
        Vector2 posBoss = centro + new Vector2(0f, mitadAlto * 0.5f);

        if (administradorActivo == null && administradorPrefab != null)
            administradorActivo = Instantiate(administradorPrefab, posBoss, Quaternion.identity);
        if (administradorActivo != null)
        {
            administradorActivo.transform.position = posBoss;
            administradorActivo.TerminarDescensoInstantaneo();
        }

        AsegurarFormaPrecisa(centro);

        if (textoDialogoCutscene != null) textoDialogoCutscene.gameObject.SetActive(false);
        if (panelCutsceneNivel2 != null) panelCutsceneNivel2.SetActive(false);
        if (botonSaltarCutscene != null) botonSaltarCutscene.SetActive(false);

        TerminarCutsceneApertura();
    }

    void TerminarCutsceneApertura()
    {
        cutsceneCoroutine = null;
        saltandoCutscene = false;
        cutsceneAperturaVistaSesion = true;
        if (panelCutsceneNivel2 != null) panelCutsceneNivel2.SetActive(false);
        if (botonSaltarCutscene != null) botonSaltarCutscene.SetActive(false);
        Telemetria.Registrar(tiempo, "cutscene_nivel2_fin", nivelFever, comboOrbes, nucleosActivos.Count, orbesActivos);

        estado = EstadoJuego.Jugando;
        IniciarPeleaNivel2();
    }
    // =================== FIN NIVEL 2 — BOSS FIGHT (en construcción) ===================

    void TerminarPartida()
    {
        estado = EstadoJuego.Fin;
        MusicManager.Instancia?.Detener();
        BeepSynth.Instancia?.Beep(90f, 0.5f, BeepSynth.Onda.Seno, 0.2f);

        Telemetria.Registrar(tiempo, "muerte", nivelFever, comboOrbes, nucleosActivos.Count, orbesActivos,
            $"comboMaximo={comboMaximo};nucleosMaximo={nucleosMaximo}");
        Telemetria.CerrarSiHabiaAlguna();

        bool nuevoRecordTiempo = tiempo > record;
        if (nuevoRecordTiempo) { record = tiempo; PlayerPrefs.SetFloat("enjambre_record", record); }
        bool nuevoRecordCombo = comboMaximo > recordCombo;
        if (nuevoRecordCombo) { recordCombo = comboMaximo; PlayerPrefs.SetInt("enjambre_record_combo", recordCombo); }
        bool nuevoRecordNucleos = nucleosMaximo > recordNucleos;
        if (nuevoRecordNucleos) { recordNucleos = nucleosMaximo; PlayerPrefs.SetInt("enjambre_record_nucleos", recordNucleos); }
        PlayerPrefs.Save();

        int esenciaGanada = MetaProgreso.CalcularRecompensa(tiempo, comboMaximo, nucleosMaximo);
        MetaProgreso.GanarEsencia(esenciaGanada);

        if (panelFin != null)
        {
            panelFin.SetActive(true);
            if (textoResultado != null)
                textoResultado.text = $"Sobreviviste {tiempo:F1} s\nCombo máximo: {comboMaximo}\nNúcleos máximo: {nucleosMaximo}";
            if (textoRecordFinal != null)
            {
                string rTiempo = nuevoRecordTiempo ? "Nuevo récord de tiempo" : $"Récord tiempo: {record:F1} s";
                string rCombo = nuevoRecordCombo ? "Nuevo récord de combo" : $"Récord combo: {recordCombo}";
                string rNucleos = nuevoRecordNucleos ? "Nuevo récord de núcleos" : $"Récord núcleos: {recordNucleos}";
                textoRecordFinal.text = $"{rTiempo}\n{rCombo}\n{rNucleos}";
            }
            if (textoEsenciaGanada != null)
                textoEsenciaGanada.text = $"+{esenciaGanada} Esencia";
        }
    }

    void MostrarMenu()
    {
        estado = EstadoJuego.Menu;
        if (panelMenu != null) panelMenu.SetActive(true);
        if (panelFin != null) panelFin.SetActive(false);
        // Los TMP_Text del HUD tenían "Record" tipeado a mano como
        // placeholder en el Inspector, y nada lo pisaba hasta el primer
        // hud.Actualizar() de una partida — se veía literal en el menú.
        // Más simple que inicializar cada texto: ocultar el HUD entero
        // mientras el menú está activo.
        if (hudRoot != null) hudRoot.SetActive(false);
        MusicManager.Instancia?.ReproducirMenu();
    }

    public void AbrirCreditos()
    {
        if (panelMenu != null) panelMenu.SetActive(false);
        if (panelCreditos != null) panelCreditos.SetActive(true);
    }

    /// <summary>Botón "Niveles" del menú principal — selector directo, sin gate de desbloqueo (ver el comentario en panelNiveles).</summary>
    public void AbrirNiveles()
    {
        if (panelMenu != null) panelMenu.SetActive(false);
        if (panelNiveles != null) panelNiveles.SetActive(true);
    }

    public void CerrarNiveles()
    {
        if (panelNiveles != null) panelNiveles.SetActive(false);
        if (panelMenu != null) panelMenu.SetActive(true);
    }

    public void CerrarCreditos()
    {
        if (panelCreditos != null) panelCreditos.SetActive(false);
        if (panelMenu != null) panelMenu.SetActive(true);
    }

    // Se puede abrir desde el Menú o desde la pantalla Fin (para gastar la
    // Esencia recién ganada sin pasar primero por el menú) — al cerrar hay
    // que volver a la que la abrió, mismo patrón que Instrucciones.
    bool mejorasAbiertaDesdeMenu = true;

    public void AbrirMejoras()
    {
        mejorasAbiertaDesdeMenu = estado != EstadoJuego.Fin;
        if (panelMenu != null) panelMenu.SetActive(false);
        if (panelFin != null) panelFin.SetActive(false);
        if (panelMejoras != null) panelMejoras.SetActive(true);
    }

    public void CerrarMejoras()
    {
        if (panelMejoras != null) panelMejoras.SetActive(false);
        if (mejorasAbiertaDesdeMenu) { if (panelMenu != null) panelMenu.SetActive(true); }
        else { if (panelFin != null) panelFin.SetActive(true); }
    }

    // Instrucciones se puede abrir desde el menú o desde la pausa — al
    // cerrarla hay que volver a la que la abrió, no siempre al menú.
    bool instruccionesAbiertaDesdeMenu = true;

    public void AbrirInstrucciones()
    {
        instruccionesAbiertaDesdeMenu = estado != EstadoJuego.Pausado;
        if (panelMenu != null) panelMenu.SetActive(false);
        if (panelPausa != null) panelPausa.SetActive(false);
        if (panelInstrucciones != null) panelInstrucciones.SetActive(true);
    }

    public void CerrarInstrucciones()
    {
        if (panelInstrucciones != null) panelInstrucciones.SetActive(false);
        if (instruccionesAbiertaDesdeMenu) { if (panelMenu != null) panelMenu.SetActive(true); }
        else { if (panelPausa != null) panelPausa.SetActive(true); }
    }

    public void BotonJugar() { modoNivel2 = false; EmpezarPartida(); }

    /// <summary>
    /// Botón "Reintentar" de Panel Fin — en Nivel 2, TerminarPeleaNivel2
    /// deja modoNivel2 en true (no llama a LimpiarPartida), así que este
    /// branch vuelve a EntrarCutsceneNivel2() en vez de EmpezarPartida():
    /// nucleosActivos ya está en 0 ahí, así que arma un enjambre mínimo
    /// desde cero y, al haberse visto ya la cutscene EN ESTA SESIÓN, la
    /// saltea directo a la pelea (ver cutsceneAperturaVistaSesion).
    /// </summary>
    public void BotonReintentar()
    {
        if (modoNivel2) { EntrarCutsceneNivel2(); return; }
        modoNivel2 = false;
        EmpezarPartida();
    }

    /// <summary>Botón "Nivel 2" del menú — solo visible una vez desbloqueado (ver MetaProgreso.DesbloquearNivel2). Entra directo a la cutscene, sin repetir Nivel 1.</summary>
    public void BotonJugarNivel2() => EntrarCutsceneNivel2();
}
