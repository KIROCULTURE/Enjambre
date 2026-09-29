using UnityEngine;

// Script del jugador: se mueve con WASD o las flechas y dispara con espacio
public class Jugador : MonoBehaviour
{
    [SerializeField] private int vidaMaxima = 100;
    [SerializeField] private float velocidad = 5f;
    [SerializeField] private GameObject balaPrefab;
    [SerializeField] private Transform puntoDisparo;

    // la vida es privada para que ningun otro script la pueda cambiar directamente
    private int vida;

    // desde afuera se puede leer la vida pero solo este script la puede cambiar
    public int Vida
    {
        get { return vida; }
        private set
        {
            // nunca menos de 0 y nunca mas que la vida maxima
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

    // no se guarda en ninguna variable, se calcula con la vida cada vez que se pregunta
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
        Mover();

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Disparar();
        }
    }

    void Mover()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        // normalized para que en diagonal no vaya mas rapido
        Vector3 direccion = new Vector3(x, 0, z).normalized;

        transform.position += direccion * velocidad * Time.deltaTime;

        // si se esta moviendo lo giro para que mire hacia donde camina
        if (direccion != Vector3.zero)
        {
            transform.forward = direccion;
        }
    }

    void Disparar()
    {
        Instantiate(balaPrefab, puntoDisparo.position, puntoDisparo.rotation);
    }

    // el enemigo llama a este metodo cuando le pega al jugador
    public void RecibirDaño(int cantidad)
    {
        // si ya esta muerto o el daño es negativo no hago nada
        if (!EstaVivo || cantidad <= 0)
        {
            return;
        }

        Vida = Vida - cantidad;
        Debug.Log("El jugador recibio " + cantidad + " de daño. Vida: " + Vida);

        if (!EstaVivo)
        {
            Morir();
        }
    }

    void Morir()
    {
        Debug.Log("El jugador murio. PERDISTE");
        gameObject.SetActive(false);
    }
}
