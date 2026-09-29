using UnityEngine;

// Script del enemigo: persigue al jugador y cuando esta cerca le pega cada cierto tiempo
public class Enemigo : MonoBehaviour
{
    [SerializeField] private int vidaMaxima = 100;
    [SerializeField] private float velocidad = 2.5f;
    [SerializeField] private int daño = 10;
    [SerializeField] private float distanciaAtaque = 1.5f;
    [SerializeField] private float tiempoEntreAtaques = 1f;
    [SerializeField] private Jugador jugador;

    private int vida;
    private float contadorAtaque = 0f;

    // igual que en el jugador, la vida solo se cambia desde este script
    public int Vida
    {
        get { return vida; }
        private set
        {
            if (value < 0)
            {
                vida = 0;
            }
            else if (value > vidaMaxima)
            {
                vida = vidaMaxima;
            }
            else
            {
                vida = value;
            }
        }
    }

    public bool EstaVivo
    {
        get { return vida > 0; }
    }

    void Start()
    {
        Vida = vidaMaxima;
    }

    void Update()
    {
        // si el jugador ya murio el enemigo se queda quieto
        if (jugador == null || !jugador.EstaVivo)
        {
            return;
        }

        contadorAtaque += Time.deltaTime;

        float distancia = Vector3.Distance(transform.position, jugador.transform.position);

        if (distancia > distanciaAtaque)
        {
            Perseguir();
        }
        else
        {
            Atacar();
        }
    }

    void Perseguir()
    {
        Vector3 destino = jugador.transform.position;
        destino.y = transform.position.y; // para que no suba ni baje, solo se mueve por el piso

        transform.position = Vector3.MoveTowards(transform.position, destino, velocidad * Time.deltaTime);
    }

    void Atacar()
    {
        // si no esperara entre ataques le pegaria en todos los frames y lo mataria al tiro
        if (contadorAtaque >= tiempoEntreAtaques)
        {
            jugador.RecibirDaño(daño);
            contadorAtaque = 0f;
        }
    }

    // la bala llama a este metodo cuando choca con el enemigo
    public void RecibirDaño(int cantidad)
    {
        if (!EstaVivo || cantidad <= 0)
        {
            return;
        }

        Vida = Vida - cantidad;
        Debug.Log("El enemigo recibio " + cantidad + " de daño. Vida: " + Vida);

        if (!EstaVivo)
        {
            Morir();
        }
    }

    void Morir()
    {
        Debug.Log("El enemigo murio. GANASTE");
        Destroy(gameObject);
    }
}
