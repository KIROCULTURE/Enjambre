using UnityEngine;

/// <summary>
/// Genera los beeps por código (como el Web Audio de la versión web),
/// sin necesitar ningún archivo de audio importado.
/// </summary>
public class BeepSynth : MonoBehaviour
{
    public static BeepSynth Instancia { get; private set; }

    AudioSource fuente;

    public enum Onda { Seno, Cuadrada, Sierra }

    void Awake()
    {
        Instancia = this;
        fuente = GetComponent<AudioSource>();
        if (fuente == null) fuente = gameObject.AddComponent<AudioSource>();
    }

    public void Beep(float frecuencia, float duracion, Onda tipo, float volumen)
    {
        int muestreo = 44100;
        int total = Mathf.CeilToInt(muestreo * duracion);
        float[] datos = new float[total];

        for (int i = 0; i < total; i++)
        {
            float tNorm = (float)i / muestreo;
            float fase = frecuencia * tNorm * Mathf.PI * 2f;
            float valor;
            switch (tipo)
            {
                case Onda.Cuadrada: valor = Mathf.Sin(fase) >= 0f ? 1f : -1f; break;
                case Onda.Sierra: valor = 2f * ((frecuencia * tNorm) % 1f) - 1f; break;
                default: valor = Mathf.Sin(fase); break;
            }
            float sobre = 1f - (float)i / total; // caída simple, no exponencial exacta
            datos[i] = valor * volumen * sobre;
        }

        var clip = AudioClip.Create("beep", total, 1, muestreo, false);
        clip.SetData(datos, 0);
        fuente.PlayOneShot(clip);
    }
}
