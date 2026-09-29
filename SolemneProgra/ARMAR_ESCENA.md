# Cómo armar la escena en Unity

(Esta guía es para ti, no es parte de la entrega.)

## 0. Configurar el input (importante)

Los scripts usan `Input.GetAxisRaw` y `Input.GetKeyDown`. En Unity 6 los proyectos nuevos vienen solo con el Input System nuevo y eso tira este error al darle play:

`InvalidOperationException: You are trying to read Input using the UnityEngine.Input class...`

Para arreglarlo:

1. Edit > Project Settings > Player > Other Settings
2. Buscar **Active Input Handling** y ponerlo en **Both**
3. Unity pide reiniciar, aceptar

Esto queda guardado en el proyecto, así que en el PC de clases no hay que volver a hacerlo.

## 1. Scripts

1. En la ventana Project, dentro de `Assets`, crear una carpeta `Scripts`.
2. Copiar ahí `Jugador.cs`, `Enemigo.cs` y `Bala.cs`.
3. Esperar a que Unity compile (no debería salir nada rojo en la consola).

## 2. Piso

1. GameObject > 3D Object > Plane
2. Nombre: `Piso`
3. Position (0, 0, 0), Scale (3, 1, 3)

## 3. Jugador

1. GameObject > 3D Object > Cube
2. Nombre: `Jugador`
3. Position (0, 0.5, -5)
4. Add Component > `Jugador`

Cañón (sirve para ver hacia dónde mira y de ahí salen las balas):

1. Click derecho sobre `Jugador` en la Hierarchy > 3D Object > Cube (así queda como hijo)
2. Nombre: `Cañon`
3. Position (0, 0, 0.6), Scale (0.2, 0.2, 0.4)
4. En el inspector quitarle el Box Collider (click en los tres puntitos > Remove Component)

## 4. Bala (prefab)

1. GameObject > 3D Object > Sphere
2. Nombre: `Bala`
3. Scale (0.3, 0.3, 0.3)
4. En el **Sphere Collider** marcar **Is Trigger**
5. Add Component > **Rigidbody** y desmarcar **Use Gravity**
6. Add Component > `Bala`
7. Crear una carpeta `Assets/Prefabs` y arrastrar la `Bala` desde la Hierarchy a esa carpeta (se vuelve azul, ya es prefab)
8. Borrar la `Bala` de la Hierarchy

## 5. Conectar el jugador

Seleccionar `Jugador` y en el componente Jugador:

- **Bala Prefab**: arrastrar el prefab `Bala` desde la carpeta Prefabs
- **Punto Disparo**: arrastrar el `Cañon` desde la Hierarchy

## 6. Enemigo

1. GameObject > 3D Object > Sphere
2. Nombre: `Enemigo`
3. Position (0, 0.5, 5)
4. Add Component > `Enemigo`
5. En el campo **Jugador** arrastrar el objeto `Jugador` desde la Hierarchy

## 7. Cámara

Seleccionar `Main Camera`:

- Position (0, 15, -10)
- Rotation (55, 0, 0)

## 8. Colores (opcional)

Para diferenciarlos: en Project click derecho > Create > Material. Hacer uno azul y arrastrarlo al Jugador, y uno rojo para el Enemigo. Son materiales normales hechos en Unity, nada externo.

## 9. Probar

Darle play y dejar abierta la ventana Console.

- WASD para moverse, espacio para disparar.
- Con 10 balas el enemigo muere y sale "GANASTE".
- Si te quedas al lado del enemigo te baja 10 por segundo y a los 10 golpes sale "PERDISTE".

## Si algo falla

| Problema | Solución |
|---|---|
| Error de `InvalidOperationException` con Input | Paso 0 |
| `UnassignedReferenceException` al disparar | Falta arrastrar Bala Prefab o Punto Disparo (paso 5) |
| Las balas atraviesan al enemigo | A la bala le falta Is Trigger o el Rigidbody (paso 4) |
| La bala se cae al piso | En el Rigidbody de la bala falta desmarcar Use Gravity |
| El enemigo no se mueve | Falta arrastrar el Jugador al campo Jugador del enemigo (paso 6) |
| "Can't add script" al agregar un script | El nombre del archivo tiene que ser igual al de la clase (Jugador.cs = class Jugador) |
