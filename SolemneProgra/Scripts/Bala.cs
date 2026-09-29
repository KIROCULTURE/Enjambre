using UnityEngine;

// La bala avanza derecho y si choca con el enemigo le hace daño
public class Bala : MonoBehaviour
{
    [SerializeField] private float velocidad = 15f;
    [SerializeField] private int daño = 10;
    [SerializeField] private float tiempoDeVida = 3f;

    void Start()
    {
        // se destruye sola despues de unos segundos para que no se acumulen balas
        Destroy(gameObject, tiempoDeVida);
    }

    void Update()
    {
        transform.Translate(Vector3.forward * velocidad * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        Enemigo enemigo = other.GetComponent<Enemigo>();

        // solo le hace daño si lo que toco es el enemigo
        if (enemigo != null)
        {
            enemigo.RecibirDaño(daño);
            Destroy(gameObject);
        }
    }
}
