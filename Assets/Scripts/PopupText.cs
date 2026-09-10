using UnityEngine;
using System.Collections;
using TMPro;

/// <summary>
/// Texto flotante tipo "+MASA": entra con rebote elástico, sube y se
/// desvanece. Requiere un componente TextMeshPro (mundo 3D, no de Canvas).
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class PopupText : MonoBehaviour
{
    TextMeshPro texto;
    float overshoot = 1.7f;
    float escalaExtra = 1f;

    void Awake() => texto = GetComponent<TextMeshPro>();

    public void Lanzar(string mensaje, Color color, float escalaExtra = 1f)
    {
        texto.text = mensaje;
        texto.color = color;
        this.escalaExtra = escalaExtra;
        // El overshoot elástico crece un poco con la intensidad del momento
        // (más cerca del clímax, el rebote es más exagerado).
        float intensidad = GameManager.Instancia != null ? GameManager.Instancia.Intensidad : 0f;
        overshoot = Mathf.Lerp(1.3f, 2.1f, intensidad);
        transform.rotation = Quaternion.Euler(0, 0, Random.Range(-8f, 8f));
        StartCoroutine(Animar());
    }

    IEnumerator Animar()
    {
        float duracion = 0.7f, t = 0f;
        Vector3 posInicial = transform.position;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float p = t / duracion;
            float escala = p < 0.22f ? Mathf.Lerp(overshoot, 1f, p / 0.22f) : 1f;
            transform.localScale = Vector3.one * escala * 0.3f * escalaExtra;
            transform.position = posInicial + Vector3.up * (0.5f * p);
            var c = texto.color; c.a = 1f - Mathf.Clamp01((p - 0.5f) / 0.5f); texto.color = c;
            yield return null;
        }
        Destroy(gameObject);
    }
}
