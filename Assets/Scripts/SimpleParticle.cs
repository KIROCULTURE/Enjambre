using UnityEngine;

/// <summary>Chispa suelta: sale disparada, frena, se desvanece y se destruye.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SimpleParticle : MonoBehaviour
{
    public float vidaSegundos = 0.5f;
    public float friccion = 2.2f;

    Vector2 velocidad;
    float vidaRestante;
    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr.sprite == null) sr.sprite = ShapeFactory.Cuadrado(16, Color.white);
        transform.localScale = Vector3.one * 0.08f;
    }

    public void Lanzar(Vector2 vel, Color color)
    {
        velocidad = vel;
        sr.color = color;
        vidaRestante = vidaSegundos;
    }

    void Update()
    {
        vidaRestante -= Time.deltaTime;
        if (vidaRestante <= 0f) { Destroy(gameObject); return; }

        velocidad *= Mathf.Max(0f, 1f - friccion * Time.deltaTime);
        transform.position += (Vector3)(velocidad * Time.deltaTime);

        var c = sr.color;
        c.a = Mathf.Clamp01(vidaRestante / vidaSegundos);
        sr.color = c;
    }
}
