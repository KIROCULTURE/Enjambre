using TMPro;
using UnityEngine;

/// <summary>
/// Controla el panel Mejoras: refresca las 4 filas (Núcleo Inicial,
/// Crecimiento, Duración Fever, Gemas en pantalla) contra MetaProgreso cada
/// vez que el panel se activa, y procesa cada botón "Comprar". Autocontenido
/// — no le agrega responsabilidades a GameManager más allá de mostrar/
/// ocultar el panel (ver GameManager.AbrirMejoras/CerrarMejoras).
/// </summary>
public class ControladorMejoras : MonoBehaviour
{
    public TMP_Text textoEsencia;

    public TMP_Text textoNivelNucleo;
    public TMP_Text textoCostoNucleo;
    public TMP_Text textoNivelCrecimiento;
    public TMP_Text textoCostoCrecimiento;
    public TMP_Text textoNivelFever;
    public TMP_Text textoCostoFever;
    public TMP_Text textoNivelGemas;
    public TMP_Text textoCostoGemas;

    void OnEnable() => Refrescar();

    void Refrescar()
    {
        if (textoEsencia != null) textoEsencia.text = $"Esencia: {MetaProgreso.Esencia}";
        RefrescarFila("nucleo", MetaProgreso.NivelNucleo, textoNivelNucleo, textoCostoNucleo);
        RefrescarFila("crecimiento", MetaProgreso.NivelCrecimiento, textoNivelCrecimiento, textoCostoCrecimiento);
        RefrescarFila("fever", MetaProgreso.NivelFever, textoNivelFever, textoCostoFever);
        RefrescarFila("gemas", MetaProgreso.NivelGemas, textoNivelGemas, textoCostoGemas);
    }

    void RefrescarFila(string id, int nivelActual, TMP_Text textoNivel, TMP_Text textoCosto)
    {
        if (textoNivel != null) textoNivel.text = $"Nivel {nivelActual}/{MetaProgreso.NivelMaximo}";
        if (textoCosto == null) return;
        int costo = MetaProgreso.CostoNivel(id, nivelActual);
        textoCosto.text = costo < 0 ? "MÁXIMO" : $"{costo} Esencia";
    }

    void Comprar(string id)
    {
        MetaProgreso.ComprarMejora(id); // si falla (sin esencia/al tope) no hace nada más — Refrescar ya muestra el estado real
        Refrescar();
    }

    public void ComprarNucleo() => Comprar("nucleo");
    public void ComprarCrecimiento() => Comprar("crecimiento");
    public void ComprarFever() => Comprar("fever");
    public void ComprarGemas() => Comprar("gemas");
}
