using UnityEngine;
using TMPro;

/// <summary>
/// Actualiza los textos del HUD: tiempo, núcleos, récord y el indicador
/// de Fever (solo visible cuando hay uno activo). Sin barra de
/// integridad — la intensidad de la partida ya se ve en todo lo demás en
/// pantalla (color, pulso de fondo, sacudida de cámara), así que una barra
/// aparte sería información redundante.
/// </summary>
public class HudController : MonoBehaviour
{
    public TMP_Text textoTiempo;
    public TMP_Text textoNucleos;
    public TMP_Text textoRecord;
    public TMP_Text textoFever;

    public void Actualizar(float tiempo, int nucleos, float record, int nivelFever)
    {
        if (textoTiempo != null) textoTiempo.text = tiempo.ToString("F1");
        if (textoNucleos != null) textoNucleos.text = nucleos.ToString();
        if (textoRecord != null) textoRecord.text = record.ToString("F1");
        if (textoFever != null)
        {
            bool activo = nivelFever > 1;
            textoFever.gameObject.SetActive(activo);
            if (activo)
            {
                textoFever.text = $"Fever x{nivelFever}";
                AnimarFever();
            }
        }
    }

    // Pulso de escala + color que gira por el espectro mientras el Fever
    // está activo — mismo lenguaje "modo fiesta" que FondoNebulosa le da al
    // fondo (ver GameManager.NivelFever), para que el HUD también se sienta
    // parte de la fiesta en vez de una etiqueta estática. Time.unscaledTime
    // para que no se note ni se corte durante un hit-stop.
    void AnimarFever()
    {
        float pulso = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.12f;
        textoFever.transform.localScale = Vector3.one * pulso;
        float hue = Mathf.Repeat(Time.unscaledTime * 0.5f, 1f);
        textoFever.color = Color.HSVToRGB(hue, 0.7f, 1f);
    }
}
