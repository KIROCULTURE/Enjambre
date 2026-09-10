using UnityEngine;

/// <summary>
/// Cuatro formas de amenaza, todas la misma clase (ver TipoAtaque):
/// Homing (el "imp" que persigue el centro de masa), Cazador (persigue a
/// un núcleo rezagado específico, elegido al nacer), Estatico (el
/// "Coágulo" — no se mueve, obstáculo que hay que rodear) y Rival (persigue
/// la gema más cercana y la roba). Densidad/variedad de estos 4 tipos es
/// la dificultad real de este survivor-like — no patrones de proyectiles
/// a esquivar (ver GameManager.ActualizarSpawnCazadores/Coagulos/Rivales).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class Enemigo : MonoBehaviour
{
    public enum TipoAtaque { Homing, Cazador, Estatico, Rival }

    public float radio = 0.32f;
    public float velocidad = 1.4f;
    public float suavizadoGiro = 3.5f;

    [HideInInspector] public TipoAtaque tipoAtaque = TipoAtaque.Homing;

    // Cazador: el núcleo específico que persigue (el más aislado del
    // enjambre al momento de nacer — ver GameManager.NucleoMasAislado).
    // Si se disuelve antes de que lo alcance, cae de nuevo al centro de
    // masa en vez de quedarse apuntando a un punto muerto.
    Nucleo objetivoCazador;

    // Rival: la gema que persigue. A diferencia de Cazador, se re-elige
    // cada vez que la actual desaparece (la tomó otro, o el jugador la
    // agarró primero) en vez de caer a un fallback fijo — sin esto un
    // Rival se quedaría quieto apuntando a la nada el resto de la partida.
    Orbe objetivoRival;

    /// <summary>
    /// Si no logra impactar a nadie en este tiempo, se autodestruye. Sin
    /// esto, un enemigo que nunca alcanza al enjambre se queda dando
    /// vueltas para siempre — nada lo saca de la escena salvo un golpe.
    /// Estatico no "vuela de largo" (no se mueve), así que no aplica.
    /// </summary>
    public float tiempoVidaMax = 12f;
    float tiempoVivo = 0f;

    /// <summary>
    /// Evita partir dos núcleos a la vez con el mismo enemigo: Destroy()
    /// no es inmediato, así que dos triggers del mismo frame podrían
    /// procesarse antes de que el enemigo desaparezca de la escena.
    /// </summary>
    [HideInInspector] public bool golpeado = false;

    [Header("Visual — el color real se interpola cada frame según GameManager.Intensidad")]
    public Color colorApagado = new Color(0.431f, 0.275f, 0.314f); // #6e4650, al empezar la partida
    public Color colorVivido = new Color(1f, 0.176f, 0.294f); // #ff2d4b, en el clímax

    Color ColorActual => Color.Lerp(colorApagado, colorVivido, GameManager.Instancia != null ? GameManager.Instancia.Intensidad : 0f);

    float angulo;
    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        var col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.42f;
        if (sr.sprite == null) sr.sprite = ShapeFactory.Estrella(64, 5);
        transform.localScale = new Vector3(radio * 2f, radio * 2f, 1f);
        gameObject.tag = "Enemigo";

        // Apunta al enjambre desde el primer frame en vez de arrancar
        // mirando para cualquier lado: si no, con el giro gradual tardaba
        // demasiado en corregir rumbo y muchos terminaban de largo, sin
        // nunca acercarse a tiempo para representar una amenaza.
        var gm = GameManager.Instancia;
        Vector2 centro = gm != null ? gm.CentroDeMasa() : Vector2.zero;
        Vector2 dir = centro - (Vector2)transform.position;
        angulo = dir.sqrMagnitude > 0.001f ? Mathf.Atan2(dir.y, dir.x) : Random.Range(0f, Mathf.PI * 2f);
    }

    void OnEnable() => GameManager.Instancia?.RegistrarEnemigo(this);
    void OnDisable() => GameManager.Instancia?.DesregistrarEnemigo(this);

    /// <summary>Llamado por GameManager justo después de instanciarlo.</summary>
    public void Inicializar(float multiplicadorDificultad)
    {
        velocidad *= multiplicadorDificultad;
        radio = Random.Range(0.28f, 0.4f);
        transform.localScale = new Vector3(radio * 2f, radio * 2f, 1f);
    }

    static readonly Color ColorCazadorApagado = new Color(0.55f, 0.28f, 0.05f);
    static readonly Color ColorCazadorVivido = new Color(1f, 0.5f, 0.1f);

    /// <summary>Depredador rápido que persigue a un rezagado específico, no al centro de masa.</summary>
    public void ConfigurarCazador(Nucleo objetivo)
    {
        tipoAtaque = TipoAtaque.Cazador;
        objetivoCazador = objetivo;
        radio *= 0.75f;
        velocidad *= 1.8f;
        transform.localScale = new Vector3(radio * 2f, radio * 2f, 1f);
        colorApagado = ColorCazadorApagado;
        colorVivido = ColorCazadorVivido;
        if (sr != null) sr.sprite = ShapeFactory.Estrella(56, 3); // dardo de 3 puntas, silueta propia
    }

    // Oscuro y sin HDR a propósito — el bloom lo deja "apagado" en
    // contraste con el enjambre brillante, reforzando que es un obstáculo
    // inerte, no una criatura viva persiguiendo.
    static readonly Color ColorCoaguloApagado = new Color(0.16f, 0.14f, 0.18f);
    static readonly Color ColorCoaguloVivido = new Color(0.28f, 0.24f, 0.32f);

    /// <summary>Obstáculo grande e inmóvil — obliga a rodearlo, no a esquivar un reflejo.</summary>
    public void ConfigurarEstatico()
    {
        tipoAtaque = TipoAtaque.Estatico;
        radio = Random.Range(1.0f, 1.5f);
        velocidad = 0f;
        transform.localScale = new Vector3(radio * 2f, radio * 2f, 1f);
        colorApagado = ColorCoaguloApagado;
        colorVivido = ColorCoaguloVivido;
        tiempoVidaMax = 999999f; // no se auto-destruye por tiempo: está para quedarse
        if (sr != null) sr.sprite = ShapeFactory.Ficha(64, Color.white, Color.white, 0); // círculo relleno
    }

    // Mismo tono que el núcleo del jugador pero corrompido/oscurecido — el
    // espejo invertido del enjambre real.
    static readonly Color ColorRivalApagado = new Color(0.22f, 0.08f, 0.28f);
    static readonly Color ColorRivalVivido = new Color(0.55f, 0.15f, 0.7f);

    /// <summary>Persigue la gema más cercana en vez del enjambre — compite por el recurso, no ataca directo.</summary>
    public void ConfigurarRival()
    {
        tipoAtaque = TipoAtaque.Rival;
        radio *= 0.85f;
        transform.localScale = new Vector3(radio * 2f, radio * 2f, 1f);
        colorApagado = ColorRivalApagado;
        colorVivido = ColorRivalVivido;
        if (sr != null) sr.sprite = ShapeFactory.Ficha(64, Color.white, Color.white, 0);
        BuscarObjetivoRival();
    }

    void BuscarObjetivoRival()
    {
        var orbes = Object.FindObjectsByType<Orbe>(FindObjectsSortMode.None);
        float distMin = float.MaxValue;
        Orbe masCercano = null;
        Vector2 pos = transform.position;
        foreach (var o in orbes)
        {
            float dist = ((Vector2)o.transform.position - pos).sqrMagnitude;
            if (dist < distMin) { distMin = dist; masCercano = o; }
        }
        objetivoRival = masCercano;
    }

    void Update()
    {
        var gm = GameManager.Instancia;
        if (gm == null || gm.estado != EstadoJuego.Jugando) return;

        tiempoVivo += Time.deltaTime;
        if (tiempoVivo >= tiempoVidaMax)
        {
            Autodestruir();
            return;
        }

        if (tipoAtaque == TipoAtaque.Estatico)
        {
            // No persigue, no se mueve — solo se re-pinta con Intensidad.
            sr.color = ColorActual;
            return;
        }

        Vector2 objetivo;
        if (tipoAtaque == TipoAtaque.Cazador)
        {
            objetivo = objetivoCazador != null ? (Vector2)objetivoCazador.transform.position : gm.CentroDeMasa();
        }
        else if (tipoAtaque == TipoAtaque.Rival)
        {
            if (objetivoRival == null) BuscarObjetivoRival();
            objetivo = objetivoRival != null ? (Vector2)objetivoRival.transform.position : gm.CentroDeMasa();
        }
        else // Homing
        {
            objetivo = gm.CentroDeMasa();
        }

        Vector2 pos = transform.position;
        Vector2 dir = objetivo - pos;
        float anguloObjetivo = Mathf.Atan2(dir.y, dir.x);

        angulo = Mathf.LerpAngle(angulo * Mathf.Rad2Deg, anguloObjetivo * Mathf.Rad2Deg,
            suavizadoGiro * Time.deltaTime) * Mathf.Deg2Rad;

        Vector2 avance = new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * velocidad * Time.deltaTime;
        Vector2 nueva = pos + avance;

        // A diferencia del Núcleo, esto no estaba limitado a la zona de
        // juego: un enemigo que se pasaba de largo podía volar fuera de
        // la zona para siempre, sin volver a acercarse ni desaparecer
        // nunca, acumulando "enemigos" que en la práctica ya no
        // amenazaban a nadie.
        float margen = radio * 2f;
        nueva.x = Mathf.Clamp(nueva.x, -gm.mitadMundoAncho - margen, gm.mitadMundoAncho + margen);
        nueva.y = Mathf.Clamp(nueva.y, -gm.mitadMundoAlto - margen, gm.mitadMundoAlto + margen);

        transform.position = nueva;
        transform.rotation = Quaternion.Euler(0, 0, angulo * Mathf.Rad2Deg);
        sr.color = ColorActual;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Solo Rival necesita detectar sus propias colisiones — el resto
        // de los impactos (Enemigo vs Núcleo) los detecta el lado del
        // Núcleo (ver Nucleo.OnTriggerEnter2D -> GameManager.ProcesarImpacto).
        if (tipoAtaque != TipoAtaque.Rival) return;
        if (!other.CompareTag("Orbe")) return;
        var o = other.GetComponent<Orbe>();
        if (o == null) return;
        GameManager.Instancia?.ProcesarGemaRobada(o);
        objetivoRival = null;
    }

    void Autodestruir()
    {
        if (golpeado) return; // ya está siendo procesado por un impacto este mismo frame
        golpeado = true;
        EfectosVisuales.Instancia?.Chispas(transform.position, ColorActual * 0.6f, 6, 1.1f);
        Destroy(gameObject);
    }
}
