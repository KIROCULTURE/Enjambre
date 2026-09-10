using UnityEngine;
using System.Collections;

/// <summary>
/// Anillo que se expande y se desvanece. Se usa tanto para el telegraph
/// de aparición de enemigos como para las ondas de choque de impacto.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class RingPulse : MonoBehaviour
{
    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr.sprite == null) sr.sprite = ShapeFactory.Anillo(64, Color.white);
    }

    public void Lanzar(Color color, float radioMax)
    {
        sr.color = color;
        StartCoroutine(Expandir(0.02f, radioMax * 2f, 0.4f));
    }

    public void LanzarTelegraph(Color color, float radioMax, float duracion)
    {
        sr.color = color;
        StartCoroutine(Expandir(0.04f, radioMax * 2f, duracion));
    }

    IEnumerator Expandir(float escalaInicial, float escalaFinal, float duracion)
    {
        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float p = t / duracion;
            float escala = Mathf.Lerp(escalaInicial, escalaFinal, 1f - Mathf.Pow(1f - p, 3f));
            transform.localScale = Vector3.one * escala;
            var c = sr.color; c.a = 1f - p; sr.color = c;
            yield return null;
        }
        Destroy(gameObject);
    }
}
