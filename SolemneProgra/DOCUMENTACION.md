# Solemne 1 - Documentación técnica

Programación de Videojuegos II

Nombre: ____________________

## Idea del proyecto

Es un juego simple visto desde arriba. El jugador es un cubo que se mueve por el piso y dispara balas. El enemigo es una esfera que persigue al jugador y cuando llega cerca le pega cada un segundo. Si la vida del enemigo llega a 0 el jugador gana, y si la vida del jugador llega a 0 pierde.

No usé modelos ni animaciones, todo son GameObjects básicos de Unity (cubo, esfera y plano).

Controles:

- WASD o flechas: moverse
- Espacio: disparar (la bala sale hacia donde está mirando el jugador)

## Intención detrás del código

Hice tres scripts: `Jugador`, `Enemigo` y `Bala`.

**La vida está encapsulada.** En el jugador y en el enemigo la variable `vida` es `private`, así que ningún otro script puede hacer algo como `enemigo.vida = -100` (no compila). Para poder ver la vida desde afuera hice una propiedad `Vida` que tiene el `get` público pero el `set` privado, entonces tampoco se puede hacer `enemigo.Vida = -100` desde otro script. Dentro del `set` se valida el valor: si es menor que 0 queda en 0 y si es mayor que `vidaMaxima` queda en `vidaMaxima`. Así la vida nunca puede quedar negativa ni pasarse del máximo.

**El estado EstaVivo se calcula solo.** `EstaVivo` es una propiedad que devuelve `vida > 0`. No es un bool que yo tenga que cambiar a mano cuando alguien muere, cada vez que se pregunta se calcula con la vida que tiene en ese momento. Los dos personajes lo tienen.

**El daño se hace con un método.** La única forma de quitarle vida a un personaje desde otro script es llamando a `RecibirDaño(int cantidad)`. La bala llama a `enemigo.RecibirDaño(daño)` y el enemigo llama a `jugador.RecibirDaño(daño)`. Ningún script toca la variable `vida` de otro. Dentro de `RecibirDaño` se revisa que el personaje siga vivo y que el daño no sea negativo (porque si no un daño negativo curaría), y después se resta usando la propiedad `Vida`, para que pase por la validación.

## Qué hace cada script y cada función

### Jugador.cs

Va en el cubo del jugador.

Variables que se ven en el inspector:

- `vidaMaxima`: la vida con la que parte (100).
- `velocidad`: qué tan rápido se mueve (5).
- `balaPrefab`: el prefab de la bala que se dispara.
- `puntoDisparo`: el objeto desde donde salen las balas (el "Cañon", que es hijo del jugador).

Funciones:

- `Vida` (propiedad): devuelve la vida. El `set` es privado y no deja que la vida baje de 0 ni que suba de `vidaMaxima`.
- `EstaVivo` (propiedad): devuelve true si la vida es mayor que 0.
- `Start()`: al empezar el juego deja la vida en el máximo.
- `Update()`: todos los frames llama a `Mover()`, y si se apretó espacio llama a `Disparar()`.
- `Mover()`: lee los ejes Horizontal y Vertical (WASD o flechas), arma un vector de dirección y lo normaliza para que en diagonal no vaya más rápido. Mueve al jugador con esa dirección multiplicada por la velocidad y por `Time.deltaTime`, y si se está moviendo lo gira para que mire hacia donde camina.
- `Disparar()`: crea una bala en la posición del punto de disparo y con su misma rotación, así la bala sale hacia adelante.
- `RecibirDaño(int cantidad)`: la llama el enemigo. Si el jugador ya está muerto o el daño es 0 o negativo no hace nada. Si no, le resta el daño a la vida, muestra la vida en la consola y si con eso murió llama a `Morir()`.
- `Morir()`: muestra "PERDISTE" en la consola y desactiva el GameObject del jugador, así desaparece y deja de moverse.

### Enemigo.cs

Va en la esfera del enemigo.

Variables que se ven en el inspector:

- `vidaMaxima`: la vida con la que parte (100).
- `velocidad`: qué tan rápido persigue (2.5, más lento que el jugador para que se pueda escapar).
- `daño`: cuánto le quita al jugador en cada ataque (10).
- `distanciaAtaque`: a qué distancia del jugador empieza a atacar (1.5).
- `tiempoEntreAtaques`: cada cuántos segundos puede atacar (1).
- `jugador`: referencia al script del jugador, se arrastra en el inspector.

Además tiene `contadorAtaque`, que es privada y va sumando el tiempo que pasó desde el último ataque.

Funciones:

- `Vida` y `EstaVivo`: funcionan igual que en el jugador.
- `Start()`: deja la vida en el máximo.
- `Update()`: si el jugador no existe o ya está muerto no hace nada (el enemigo se queda quieto). Si no, suma el tiempo al contador, calcula la distancia hasta el jugador y si está lejos lo persigue y si está cerca lo ataca.
- `Perseguir()`: mueve al enemigo hacia el jugador con `Vector3.MoveTowards`. Le dejo la misma altura (y) que ya tenía para que se mueva solo por el piso.
- `Atacar()`: si el contador ya llegó al tiempo entre ataques, llama a `jugador.RecibirDaño(daño)` y vuelve el contador a 0. Si no esperara, le pegaría en todos los frames y el jugador moriría al tiro.
- `RecibirDaño(int cantidad)`: la llama la bala. Hace lo mismo que la del jugador: revisa que esté vivo y que el daño sea positivo, resta la vida y si llegó a 0 llama a `Morir()`.
- `Morir()`: muestra "GANASTE" en la consola y destruye el GameObject del enemigo.

### Bala.cs

Va en el prefab de la bala (una esfera chica).

Variables que se ven en el inspector:

- `velocidad`: qué tan rápido avanza (15).
- `daño`: cuánto le quita al enemigo (10).
- `tiempoDeVida`: a los cuántos segundos se destruye sola (3).

Funciones:

- `Start()`: programa que la bala se destruya después de `tiempoDeVida` segundos, así no quedan balas infinitas si no le pegan a nada.
- `Update()`: mueve la bala hacia adelante (su propio adelante, que es hacia donde miraba el jugador cuando disparó).
- `OnTriggerEnter(Collider other)`: se llama cuando la bala toca algo. Busca si lo que tocó tiene el script `Enemigo`. Si lo tiene, le llama a `RecibirDaño` y la bala se destruye. Si toca otra cosa (el piso o el mismo jugador) no pasa nada.

Para que `OnTriggerEnter` funcione la bala tiene el collider con Is Trigger activado y un Rigidbody sin gravedad.

## Comportamiento esperado

- Al darle play el enemigo empieza a seguir al jugador.
- El jugador se mueve con WASD y dispara con espacio.
- Cada bala que le llega al enemigo le quita 10 de vida. Con 10 balas el enemigo muere, desaparece y sale "GANASTE" en la consola.
- Si el enemigo está cerca del jugador le quita 10 de vida cada un segundo. Si la vida del jugador llega a 0 el jugador desaparece y sale "PERDISTE".
- Cuando el jugador muere el enemigo se queda quieto.
- La vida nunca queda negativa: si a alguien le quedan 5 de vida y le hacen 10 de daño queda en 0, no en -5.
- En la consola se va mostrando la vida de cada uno cada vez que recibe daño.
