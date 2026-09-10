using UnityEngine;

/// <summary>
/// Un "trozo" del enjambre del jugador: la ficha dorada. Se mueve hacia
/// GameManager.Objetivo, y al chocar con un Enemigo le pide a GameManager
/// que la parta en dos (o la disuelva si ya es muy pequeña).
///
/// El squash&amp;stretch visual se aplica solo al hijo "Visual": el
/// CircleCollider2D vive en la raíz con escala siempre uniforme, porque
/// Unity mantiene circular un CircleCollider2D bajo escala no-uniforme
/// usando el eje mayor, lo que agrandaría la hitbox durante el estirado
/// si escalábamos la misma raíz que lleva el collider.
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Nucleo : MonoBehaviour
{
    [Header("Estado (lo asigna GameManager al crear el fragmento)")]
    public float radio = 0.5f;
    public Vector2 velocidad;

    /// <summary>
    /// Evita partirse dos veces a la vez: Destroy() no es inmediato, así
    /// que dos enemigos distintos podrían tocar este mismo núcleo en el
    /// mismo frame y generar 4 fragmentos en vez de 2.
    /// </summary>
    [HideInInspector] public bool golpeado = false;

    [Header("Movimiento — valores de partida, ajusta jugando")]
    public float aceleracion = 6f;
    public float friccion = 8f;
    public float velocidadBase = 2.4f;

    [Header("Visual — el color real se interpola cada frame según GameManager.Intensidad")]
    public Transform visual;
    // Círculo relleno generado por código (ver SpriteCirculo abajo): blanco
    // liso, igual que Estrella/Diamante — el color de rol real vive acá,
    // no horneado en la imagen. El mismo azul que usa el contador de
    // núcleos del HUD (EstiloMinimal.ColorVida), para que el número grande
    // arriba se lea como "esto sos vos".
    public Sprite spriteJugador;
    public Color colorApagado = new Color(0.25f, 0.32f, 0.5f);
    // HDR a propósito (canales > 1): con Bloom.threshold=1 (ver
    // AplicarPostProcesado.cs), esto es lo que hace que el núcleo brille
    // de verdad al llegar al clímax, en vez de solo ser un azul saturado.
    public Color colorVivido = new Color(0.63f, 1.35f, 1.8f);

    // Al nacer de un impacto (ver GameManager.CrearFragmento), el
    // fragmento nuevo muestra esta variante (la misma forma, en naranja)
    // un instante en vez del sprite normal — un destello de daño que se lee
    // fuerte sin importar en qué momento de la partida estés.
    public Sprite spriteGolpeado;
    static readonly Color ColorGolpeado = new Color(1f, 0.55f, 0.15f);
    float tFlashGolpeado;

    Color ColorActual => Color.Lerp(colorApagado, colorVivido, GameManager.Instancia != null ? GameManager.Instancia.Intensidad : 0f);

    // Generado en tiempo real (ShapeFactory ya cachea por forma, así que
    // esto no repite trabajo entre fragmentos) — a diferencia de un Sprite
    // importado de una textura real, uno creado en memoria por
    // ShapeFactory no sobrevive guardado como referencia en el prefab de
    // un proceso de Unity al siguiente, así que no puede asignarse desde
    // un script de editor: tiene que generarse acá, cada vez.
    static Sprite SpriteCirculo => ShapeFactory.Ficha(64, Color.white, Color.white, 0);

    SpriteRenderer sr;
    CircleCollider2D col;

    void Awake()
    {
        col = GetComponent<CircleCollider2D>();
        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        if (visual == null)
        {
            var t = transform.Find("Visual");
            visual = t != null ? t : transform;
        }
        sr = visual.GetComponent<SpriteRenderer>();
        if (sr == null) sr = visual.gameObject.AddComponent<SpriteRenderer>();

        // Círculo relleno generado por código (mismo patrón que Enemigo/
        // Orbe: blanco liso, se tiñe en tiempo real vía ColorActual) en vez
        // de la ilustración importada — con muchos fragmentos en pantalla
        // un aro con agujero se leía delgado; un disco relleno se lee como
        // una masa de verdad. spriteJugador/spriteGolpeado quedan como
        // override manual si algún día se quiere volver a arte importado
        // (un Sprite de una textura real SÍ persiste en el prefab; uno
        // generado en memoria por ShapeFactory no, por eso esto se decide
        // en código y no asignando el campo desde un script de editor).
        if (sr.sprite == null) sr.sprite = spriteJugador != null ? spriteJugador : SpriteCirculo;
        col.isTrigger = true;
        col.radius = 0.5f;
        AplicarEscala();
    }

    void OnEnable() => GameManager.Instancia?.RegistrarNucleo(this);
    void OnDisable() => GameManager.Instancia?.DesregistrarNucleo(this);

    void Update()
    {
        var gm = GameManager.Instancia;
        if (gm == null || gm.estado != EstadoJuego.Jugando) return;

        Vector2 pos = transform.position;
        Vector2 dir = gm.Objetivo - pos;
        float dist = dir.magnitude;
        if (dist > 0.001f) dir /= dist;

        float boost = gm.MultiplicadorVelocidadActual;
        velocidad += dir * aceleracion * boost * Time.deltaTime;
        velocidad += ComputarSeparacion(gm, pos) * gm.fuerzaSeparacionNucleos * Time.deltaTime;
        velocidad *= Mathf.Max(0f, 1f - friccion * Time.deltaTime);

        float tope = velocidadBase * (gm.radioInicial / radio) * boost;
        if (velocidad.magnitude > tope) velocidad = velocidad.normalized * tope;

        Vector2 nueva = pos + velocidad * Time.deltaTime;
        nueva.x = Mathf.Clamp(nueva.x, -gm.mitadMundoAncho + radio, gm.mitadMundoAncho - radio);
        nueva.y = Mathf.Clamp(nueva.y, -gm.mitadMundoAlto + radio, gm.mitadMundoAlto - radio);
        transform.position = nueva;

        if (tFlashGolpeado > 0f)
        {
            tFlashGolpeado -= Time.deltaTime;
            sr.sprite = spriteGolpeado != null ? spriteGolpeado : SpriteCirculo;
            sr.color = spriteGolpeado != null ? Color.white : ColorGolpeado;
            if (tFlashGolpeado <= 0f) sr.sprite = spriteJugador != null ? spriteJugador : SpriteCirculo;
        }
        else
        {
            sr.color = boost > 1f ? Color.Lerp(ColorActual, new Color(1f, 0.85f, 0.1f), 0.55f) : ColorActual;
        }

        float rapidez = velocidad.magnitude;
        if (rapidez > 0.05f)
        {
            float ang = Mathf.Atan2(velocidad.y, velocidad.x) * Mathf.Rad2Deg;
            visual.rotation = Quaternion.Euler(0, 0, ang);
            float est = Mathf.Clamp(rapidez * 0.1f, 0f, 0.25f);
            visual.localScale = new Vector3(1 + est, 1 - est, 1f);
        }
        else
        {
            visual.rotation = Quaternion.identity;
            visual.localScale = Vector3.one;
        }

        // Se recalcula todos los frames (no solo al crecer) porque el
        // factor de achique por cantidad depende de cuántos núcleos hay
        // activos AHORA, y esa cantidad cambia todo el tiempo (se parten,
        // se disuelven) sin pasar por Crecer().
        AplicarEscala();
    }

    // Separación tipo boids: todos los núcleos persiguen el mismo
    // GameManager.Objetivo, así que sin esto terminaban apilados unos
    // sobre otros en vez de leerse como un enjambre disperso. Empuja lejos
    // de cualquier otro núcleo que esté más cerca que la distancia mínima
    // combinada (que también se achica con FactorEscalaPorCantidad, para
    // que el "espacio personal" se reduzca junto con el tamaño visual).
    //
    // Tres optimizaciones para que esto no laggeara con ~300 fragmentos:
    // VecinosCercanos usa el grid espacial de GameManager en vez de
    // comparar contra TODO el enjambre, la distancia se compara al
    // cuadrado (sqrMagnitude, sin raíz) antes de considerar un par, y — la
    // que de verdad importa cuando el enjambre viene compacto (persiguiendo
    // el mismo Objetivo, no esparcido por el mundo, así que el grid solo no
    // alcanza: medido en batch, sin este tope 300 núcleos juntos tardaban
    // ~37ms, muy por encima de los 16.6ms de un frame a 60fps) — se corta
    // apenas se procesan maxVecinosSeparacion candidatos. Es una fuerza
    // sumada entre varios vecinos, no una regla exacta: falta contribución
    // de algunos vecinos lejanos dentro de la celda no cambia la dirección
    // de la separación, solo la afina un poco menos.
    Vector2 ComputarSeparacion(GameManager gm, Vector2 pos)
    {
        Vector2 separacion = Vector2.zero;
        float factorEscala = gm.FactorEscalaPorCantidad;
        int procesados = 0;
        foreach (var otro in gm.VecinosCercanos(pos))
        {
            if (otro == this || otro == null) continue;
            if (procesados++ >= gm.maxVecinosSeparacion) break;
            Vector2 delta = pos - (Vector2)otro.transform.position;
            float distCuadrada = delta.sqrMagnitude;
            float distMinima = (radio + otro.radio) * gm.margenSeparacionNucleos * factorEscala;
            if (distCuadrada > 0.00000001f && distCuadrada < distMinima * distMinima)
            {
                float distActual = Mathf.Sqrt(distCuadrada);
                separacion += (delta / distActual) * (distMinima - distActual);
            }
        }
        return separacion;
    }

    /// <summary>Llamado por GameManager al crear un fragmento recién golpeado.</summary>
    public void MostrarGolpeado(float duracion) => tFlashGolpeado = duracion;

    public void Crecer(float cantidad)
    {
        var gm = GameManager.Instancia;
        radio = Mathf.Min(gm != null ? gm.radioMaximo : 0.95f, radio + cantidad);
        AplicarEscala();
    }

    // El factor de achique por cantidad (ver GameManager) se aplica acá,
    // sobre la escala real de la raíz (la misma que lleva el collider) —
    // así un enjambre con muchos fragmentos también los hace colisionar en
    // un área más chica, no solo verse más chicos.
    void AplicarEscala()
    {
        var gm = GameManager.Instancia;
        float factor = gm != null ? gm.FactorEscalaPorCantidad : 1f;
        transform.localScale = new Vector3(radio * 2f * factor, radio * 2f * factor, 1f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemigo"))
        {
            GameManager.Instancia?.ProcesarImpacto(this, other.gameObject);
        }
        else if (other.CompareTag("Orbe"))
        {
            var o = other.GetComponent<Orbe>();
            if (o != null) GameManager.Instancia?.ProcesarPickupOrbe(this, o);
        }
    }
}
