using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Hace que un botón se "hunda" en su propia sombra dura al presionarlo:
/// se desplaza hacia la sombra (offsetHundido) al bajar el dedo/mouse y
/// vuelve a su lugar al soltar, en vez de solo cambiar de color.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class BotonHundido : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Vector2 offsetHundido = new Vector2(6, -6);

    RectTransform rt;
    Vector2 posOriginal;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        posOriginal = rt.anchoredPosition;
    }

    public void OnPointerDown(PointerEventData eventData) => rt.anchoredPosition = posOriginal + offsetHundido;
    public void OnPointerUp(PointerEventData eventData) => rt.anchoredPosition = posOriginal;
    public void OnPointerExit(PointerEventData eventData) => rt.anchoredPosition = posOriginal;
}
