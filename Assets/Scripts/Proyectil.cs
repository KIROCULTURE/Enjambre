using UnityEngine;

/// <summary>
/// Bala pooled de los patrones del Administrador del Sistema (punto 4).
/// Sin collider ni física real — el chequeo de impacto es una simple
/// distancia contra FormaPrecisa, resuelto en GameManager.ActualizarProyectiles
/// junto con el movimiento en un único loop (mismo lenguaje que el grid de
/// separación de Nucleo) para minimizar el costo por bala cuando hay
/// varias decenas activas a la vez durante un anillo/espiral.
///
/// GameManager la crea/recicla con un pool propio (Stack de inactivas +
/// List de activas) en vez de Instantiate/Destroy por bala — con la
/// cantidad de balas que dispara un patrón de danmaku, esa churn sería el
/// mismo problema de rendimiento que ya se encontró y arregló con los
/// Núcleo. Por eso Configurar() no depende de Awake() (que en batch
/// mode/Edit Mode no corre en objetos recién creados, ver comentarios de
/// GameManager) — arma su propio SpriteRenderer perezosamente la primera
/// vez que se usa, y ya queda listo para reciclarse.
/// </summary>
public class Proyectil : MonoBehaviour
{
    public Vector2 velocidad;
    public float radioHitbox = 0.07f;
    // Grados/segundo de rotación del vector `velocidad` — 0 es una bala
    // recta de toda la vida; distinto de 0 la hace curvar mientras viaja
    // (patrones "flor", ver GameManager.PatronFlorGiratoria). Aplicado en
    // GameManager.ActualizarProyectiles, no acá, para que el pool entero
    // se mueva en un solo loop.
    public float velocidadAngular;

    SpriteRenderer sr;

    public void Configurar(Vector2 pos, Vector2 vel, Color color, float radioVisual, float radioHitboxNuevo, float velocidadAngularNueva = 0f)
    {
        transform.position = pos;
        velocidad = vel;
        radioHitbox = radioHitboxNuevo;
        velocidadAngular = velocidadAngularNueva;

        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        if (sr.sprite == null) sr.sprite = ShapeFactory.Diamante(20);
        sr.color = color;
        transform.localScale = Vector3.one * radioVisual * 2f;
    }
}
