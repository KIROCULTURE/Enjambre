using UnityEngine;

/// <summary>
/// El personaje jugable del Nivel 2 — la contracara del enjambre: un solo
/// cuerpo, no una masa de fragmentos. Narrativamente es el virus
/// recompilado a la fuerza por el hechizo fallido del administrador (ver
/// la cutscene de apertura, GameManager.EntrarCutsceneNivel2).
///
/// Movimiento deliberadamente distinto al de Nucleo: sin aceleración ni
/// fricción, respuesta directa al input (estilo danmaku clásico —
/// mantener una dirección mueve a velocidad constante, soltarla frena en
/// seco), y solo por teclado — un mouse-follow como el del enjambre no
/// sirve para esquivar patrones con precisión. El área de movimiento es
/// el rectángulo FIJO que la cámara muestra (gm.mitadAncho/mitadAlto), no
/// el mundo grande estilo Agar.io del enjambre: la pelea es en una arena
/// acotada, no un mundo para recorrer.
///
/// Mover() está separado de Update() (que solo lee Input y le pasa una
/// dirección) para poder probarlo en batch mode sin necesitar simular
/// teclas — ver VerificarFormaPrecisa.cs.
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class FormaPrecisa : MonoBehaviour
{
    [Header("Movimiento — directo, sin inercia (ver comentario de arriba)")]
    public float velocidad = 3.4f;
    // Mantener enfocar (Shift) mueve más lento y muestra el hitbox real
    // (ver radioHitbox) — convención estándar de danmaku para esquivar con
    // precisión cerca de un patrón denso.
    public float velocidadEnfocado = 1.5f;

    [Header("Hitbox real vs. sprite visual — a propósito bien chico")]
    // El sprite se lee como personaje; lo que realmente colisiona es
    // mucho más chico (mismo lenguaje que cualquier shmup/danmaku serio),
    // para que esquivar dependa de leer el patrón, no de pelear contra un
    // hitbox que no coincide con lo que el jugador VE. Fase 2 del
    // rediseño: el sprite pasó de un cuadrado grande a un polígono más
    // chico (ver SpriteCuerpo) — el hitbox se achicó en la misma
    // proporción (antes 0.055/0.22 ≈ 25%), no es un número nuevo suelto.
    public float radioVisual = 0.15f;
    public float radioHitbox = 0.0375f;

    [Header("Vidas — sin barra de vida del boss, la Forma Precisa sí puede morir")]
    public int vidas = 3;
    public float duracionInvulnerable = 1f;
    bool invulnerable;
    float tInvulnerable;
    float tFlash;
    const float duracionFlash = 0.15f;

    /// <summary>Point 4 (la pelea real) se suscribe acá para saber cuándo terminar la partida.</summary>
    public event System.Action AlQuedarSinVidas;

    [Header("Visual — incluida como ShapeFactory igual que el resto del juego, sin arte importado")]
    public Transform visual;
    public Transform hitboxVisual;
    static readonly Color ColorGolpeado = new Color(1f, 0.55f, 0.15f);
    // Blanco-cian, deliberadamente distinto del azul del enjambre (ver
    // Nucleo.colorVivido) — "compilado/preciso" en vez de "masa orgánica".
    // HDR a propósito para el mismo Bloom que ya usa el resto del juego.
    public Color colorApagado = new Color(0.55f, 0.58f, 0.62f);
    public Color colorVivido = new Color(1.7f, 2.0f, 2.15f);

    // Fase 2 del rediseño de Nivel 2 (pedido explícito): reemplaza al
    // cuadrado grande por un polígono tipo rombo/diamante con más lados
    // (octágono alargado en vertical) — sigue leyéndose "compilado" (ver
    // colorApagado/colorVivido), pero ya no es una primitiva tan genérica.
    static Sprite SpriteCuerpo => ShapeFactory.Poligono(64, 8, Color.white, 1.3f);
    static Sprite SpriteHitbox => ShapeFactory.Ficha(32, Color.white, Color.white, 0);

    [Header("Fase 3 — aura de carga de energía (pedido explícito: se lee en EL JUGADOR, el HUD es secundario)")]
    // Hijo opcional (igual que hitboxVisual): si el prefab no lo trae,
    // sigue funcionando sin aura, no rompe nada. GameManager.ProcesarPickupOrbeNivel2
    // llama a ActualizarCargaEnergia — este script no sabe nada de orbes,
    // solo dibuja la fracción 0-1 que le pasan.
    public Transform auraEnergia;
    static Sprite SpriteAura => ShapeFactory.Aura(64);
    // Violeta HDR — mismo color que GameManager.ColorOrbeNivel2, para que
    // el jugador conecte de un vistazo "esto es lo que estoy juntando" con
    // "esto es lo que se está cargando encima mío".
    static readonly Color ColorAura = new Color(1.4f, 1.1f, 2.6f);
    SpriteRenderer srAura;

    SpriteRenderer srCuerpo;
    SpriteRenderer srHitbox;
    CircleCollider2D col;
    /// <summary>Expuesto para la cutscene de cierre (revisión) — el shader de glitch sobre el jugador en el momento de la transformación final necesita tocar el material real, no solo el .color que ya expone ActualizarVisual.</summary>
    public SpriteRenderer SpriteCuerpoRenderer => srCuerpo;

    void Awake()
    {
        col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = radioHitbox;

        if (visual == null)
        {
            var t = transform.Find("Visual");
            visual = t != null ? t : transform;
        }
        srCuerpo = visual.GetComponent<SpriteRenderer>();
        if (srCuerpo == null) srCuerpo = visual.gameObject.AddComponent<SpriteRenderer>();
        if (srCuerpo.sprite == null) srCuerpo.sprite = SpriteCuerpo;
        visual.localScale = new Vector3(radioVisual * 2f, radioVisual * 2f, 1f);

        if (hitboxVisual == null)
        {
            var t = transform.Find("Hitbox");
            hitboxVisual = t != null ? t : null;
        }
        if (hitboxVisual != null)
        {
            srHitbox = hitboxVisual.GetComponent<SpriteRenderer>();
            if (srHitbox == null) srHitbox = hitboxVisual.gameObject.AddComponent<SpriteRenderer>();
            if (srHitbox.sprite == null) srHitbox.sprite = SpriteHitbox;
            srHitbox.color = new Color(2f, 0.3f, 0.4f); // HDR rojo — el punto real donde te pueden tocar
            // Mismo sortingLayer/Z que Visual (ambos en el origen del root)
            // — sin esto el orden de dibujado entre los dos queda
            // indefinido, y el punto quedaba tapado por el cuerpo entero.
            srHitbox.sortingOrder = srCuerpo.sortingOrder + 1;
            hitboxVisual.localScale = new Vector3(radioHitbox * 2f, radioHitbox * 2f, 1f);
            // Fase 2 (pedido explícito): antes solo se mostraba enfocado
            // (Shift) — con los patrones densos de la pelea real el
            // jugador necesita ver SIEMPRE qué punto exacto le pega, no
            // solo cuando ya decidió jugar con precisión. Queda activo
            // desde el arranque; ActualizarVisual() ya no lo apaga.
            hitboxVisual.gameObject.SetActive(true);
        }

        if (auraEnergia == null)
        {
            var t = transform.Find("Aura");
            auraEnergia = t != null ? t : null;
        }
        if (auraEnergia != null)
        {
            srAura = auraEnergia.GetComponent<SpriteRenderer>();
            if (srAura == null) srAura = auraEnergia.gameObject.AddComponent<SpriteRenderer>();
            if (srAura.sprite == null) srAura.sprite = SpriteAura;
            // Detrás del cuerpo (sortingOrder MENOR) — es un glow que rodea
            // a FormaPrecisa, no algo que la tape a ella o al punto de hitbox.
            srAura.sortingOrder = srCuerpo.sortingOrder - 1;
            ActualizarCargaEnergia(0f);
        }
    }

    Color ColorActual => Color.Lerp(colorApagado, colorVivido, GameManager.Instancia != null ? GameManager.Instancia.Intensidad : 0f);

    /// <summary>
    /// Fase 3 del rediseño de Nivel 2 (pedido explícito): el estado de
    /// carga de la barra de energía se lee PRINCIPALMENTE en el jugador
    /// (crece y brilla más), no en una esquina del HUD — "el jugador tiene
    /// la vista clavada en los proyectiles". GameManager es dueño del
    /// valor real (orbes, umbral, descarga); esto solo dibuja la fracción
    /// que le pasan, sin guardar estado propio.
    /// </summary>
    public void ActualizarCargaEnergia(float fraccion01)
    {
        if (srAura == null) return;
        fraccion01 = Mathf.Clamp01(fraccion01);

        // Un pulso sutil que se acelera con la carga — la misma idea que
        // el respiro de FondoNebulosa, para que "casi lista" se sienta
        // distinto de "recién empezando" incluso sin mirar ningún número.
        float pulso = 1f + Mathf.Sin(Time.time * Mathf.Lerp(2f, 6f, fraccion01)) * 0.08f * fraccion01;
        float escala = Mathf.Lerp(radioVisual * 1.7f, radioVisual * 3.4f, fraccion01) * pulso;
        auraEnergia.localScale = new Vector3(escala * 2f, escala * 2f, 1f);
        srAura.color = new Color(ColorAura.r, ColorAura.g, ColorAura.b, Mathf.Lerp(0f, 0.8f, fraccion01));
    }

    void Update()
    {
        var gm = GameManager.Instancia;
        if (gm == null || gm.estado != EstadoJuego.Jugando) return;

        Vector2 dir = Vector2.zero;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) dir.x -= 1;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) dir.x += 1;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) dir.y += 1;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) dir.y -= 1;
        bool enfocado = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        Mover(dir, enfocado, Time.deltaTime);
        ActualizarInvulnerabilidad(Time.deltaTime);
        ActualizarVisual(enfocado);
    }

    /// <summary>
    /// Núcleo testeable del movimiento: sin estado propio entre llamadas
    /// (nada de velocidad acumulada) — la posición nueva depende solo de
    /// la dirección de ESTE frame, por diseño (ver comentario de clase).
    /// </summary>
    public void Mover(Vector2 direccionInput, bool enfocado, float dt)
    {
        var gm = GameManager.Instancia;
        Vector2 dir = direccionInput.sqrMagnitude > 0.001f ? direccionInput.normalized : Vector2.zero;
        float v = enfocado ? velocidadEnfocado : velocidad;

        Vector2 pos = (Vector2)transform.position + dir * v * dt;
        if (gm != null)
        {
            pos.x = Mathf.Clamp(pos.x, -gm.mitadAncho + radioVisual, gm.mitadAncho - radioVisual);
            pos.y = Mathf.Clamp(pos.y, -gm.mitadAlto + radioVisual, gm.mitadAlto - radioVisual);
        }
        transform.position = pos;
    }

    void ActualizarInvulnerabilidad(float dt)
    {
        if (tInvulnerable > 0f)
        {
            tInvulnerable -= dt;
            if (tInvulnerable <= 0f) invulnerable = false;
        }
        if (tFlash > 0f) tFlash -= dt;
    }

    void ActualizarVisual(bool enfocado)
    {
        // Siempre visible desde Fase 2 (ver Awake) — enfocado ahora solo
        // lo agranda un poco, para no perder del todo la señal de "estoy
        // esquivando con precisión" que antes daba aparecer/desaparecer.
        if (hitboxVisual != null)
        {
            float escala = radioHitbox * 2f * (enfocado ? 1.4f : 1f);
            hitboxVisual.localScale = new Vector3(escala, escala, 1f);
        }

        if (srCuerpo == null) return;
        if (tFlash > 0f) srCuerpo.color = ColorGolpeado;
        // Parpadeo simple mientras dura la invulnerabilidad post-golpe —
        // mismo lenguaje que cualquier shmup para que se lea "no te puedo
        // tocar todavía" sin necesitar un ícono aparte.
        else if (invulnerable) srCuerpo.color = Color.Lerp(ColorActual, Color.clear, Mathf.PingPong(Time.unscaledTime * 10f, 1f) * 0.6f);
        else srCuerpo.color = ColorActual;
    }

    /// <summary>
    /// Punto de entrada único de daño — cada patrón/proyectil de la pelea
    /// (punto 4) llama a esto pasando su propio nombre en `fuente`, para
    /// poder telemetrar más adelante "qué patrón mata más" sin tener que
    /// tocar esta firma de nuevo. Ignora golpes mientras dura la
    /// invulnerabilidad post-impacto (evita perder varias vidas de golpe
    /// contra un mismo patrón ancho).
    /// </summary>
    public void RecibirGolpe(string fuente = "desconocido")
    {
        if (invulnerable || vidas <= 0) return;

        vidas--;
        invulnerable = true;
        tInvulnerable = duracionInvulnerable;
        tFlash = duracionFlash;

        EfectosVisuales.Instancia?.Chispas(transform.position, ColorGolpeado, 14, 2.5f);
        CameraPunch.Instancia?.Golpear();
        BeepSynth.Instancia?.Beep(90f, 0.2f, BeepSynth.Onda.Sierra, 0.22f);

        var gm = GameManager.Instancia;
        Telemetria.Registrar(gm != null ? gm.Tiempo : 0f, "forma_precisa_golpe", 0, 0, 0, 0, $"vidas_restantes={vidas};fuente={fuente}");

        if (vidas <= 0) AlQuedarSinVidas?.Invoke();
    }
}
