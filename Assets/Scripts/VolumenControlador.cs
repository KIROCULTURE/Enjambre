using UnityEngine;

/// <summary>
/// Volumen general del juego (música + todos los beeps, vía
/// AudioListener.volume — así no hay que tocar cada AudioSource por
/// separado). Se guarda en PlayerPrefs y se aplica apenas arranca el
/// juego, antes de que suene nada.
/// </summary>
public class VolumenControlador : MonoBehaviour
{
    public static VolumenControlador Instancia { get; private set; }

    const string Clave = "enjambre_volumen";

    void Awake()
    {
        Instancia = this;
        AudioListener.volume = PlayerPrefs.GetFloat(Clave, 0.8f);
    }

    public float VolumenActual => AudioListener.volume;

    /// <summary>Conectado al Slider del menú (OnValueChanged).</summary>
    public void SetVolumen(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(Clave, v);
        PlayerPrefs.Save();
    }
}
