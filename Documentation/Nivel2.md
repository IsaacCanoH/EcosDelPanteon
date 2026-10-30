# Cementerio · Nivel 2

Abre `Assets/Scenes/Cementerio_Nivel2.unity` y pulsa Play. Si la escena estaba abierta antes de recibir los cambios, vuelve a abrirla desde Project para cargar la versión guardada en disco.

- **WASD:** movimiento.
- **Esc:** volver al menú.
- **R:** reintentar después de perder las cinco vidas.
- **E en la entrada:** salir con los tres juguetes recuperados.

## Mapa

El mapa se reconstruyó con una distribución asimétrica distinta de la del nivel 1. Conserva el terreno actual de **44 × 26 celdas**, entre `(-22, -13)` y `(22, 13)`. La entrada está en la barda oeste, con una puerta y un pasillo corto que desemboca directamente en los senderos del cementerio. El jugador aparece junto a esa puerta y regresa a ella para salir.

Los objetos siguen dentro de `SCENE/LAYER 1/Props/Tombs`, `Bench`, `Statues` y `Decoration`; la hierba está dentro de `SCENE/LAYER 1/Grass`. Cada zona tiene subgrupos descriptivos. `Enemies` contiene al niño y `Relics` conserva los juguetes. Se respetaron los layouts y sus tilemaps; se retiraron la cripta y su plataforma elevada.

Los caminos curvos conectan una arboleda con pozo al noroeste, una plaza circular con estatua hacia el centro este, panteones familiares al sureste y un memorial al noreste. Un circuito por el sur ofrece otro recorrido entre las zonas. Hay árboles, tumbas de distintas formas, sarcófagos, bancas, bardas interiores rotas, arbustos, piedras, flores y 300 instancias de pasto. Los juguetes se distribuyen entre los extremos noroeste, sureste y noreste; se pueden recoger en cualquier orden.

La iluminación copia los parámetros del ambiente nocturno y la lámpara del nivel 1: intensidades de 0.55 y 2.5, respectivamente, y radio exterior de lámpara de 4 unidades. También se igualaron los tintes y materiales de los tilemaps, objetos y sombras. Las luces adicionales del niño y los juguetes están desactivadas para conservar ese aspecto. Estos ajustes son locales a la escena del nivel 2; no se editan materiales ni prefabs originales compartidos.

El jugador conserva su lámpara, audio de pasos, sonido de recolección y cinco vidas. El HUD reutiliza los corazones, la fuente pixelada, los contadores y el pergamino del nivel 1, con iconos de los tres juguetes y avisos sobre los cambios del enemigo. La plantilla se encuentra en `Assets/Prefabs/UI/PF_Level2HUD.prefab`.

## Niño espectral

| Juguetes recogidos | Comportamiento |
| --- | --- |
| 0 | Sigue desde el inicio, intenta mantenerse a 5 unidades y no ataca. |
| 1 | Persigue y golpea a menos de 0.9 unidades. |
| 2 | Intenta mantenerse a 4.5 unidades; puede lanzar hasta 7 unidades. |
| 3 | Intenta mantenerse a 8 unidades; puede lanzar hasta 12 unidades. |

La cadencia de lanzamiento está fijada en `GhostChildAI.StoneInterval = 4f`; no depende de la dificultad. La transición al tercer juguete conserva el reloj del lanzamiento anterior. Antes de lanzar hay una anticipación de 0.3 segundos. Las piedras viajan a **10 unidades por segundo** (antes 5.5), apuntan a la posición del jugador al soltarlas, tienen tiempo de vida limitado y se destruyen al chocar con un objeto sólido o con el jugador. Cada golpe o piedra resta una vida.

El niño utiliza la navegación existente para rodear objetos y sólo ataca cuando tiene línea de visión. Recoger el último juguete aumenta su alcance; el enemigo continúa activo hasta que el jugador sale por la entrada.

El PNG original permanece en `Assets/Sprites/Enemies/Niño/sprites_niño.png`. Se recortó mediante el importador de Unity en 32 sprites con pivotes alineados a los pies y filtro Point. En `Animations` hay 16 clips: reposo, caminata, golpe y lanzamiento en cuatro direcciones. La hoja proporcionada contiene poses de reposo/caminata; los gestos de ataque reutilizan esas poses con una inclinación breve del objeto `Visual`.

## Colisiones y profundidad

`WorldDepth2D` ordena cada personaje y prop completo por su base mediante un `SortingGroup`. La copa y el tronco de cada árbol se dibujan juntos; las categorías de la jerarquía ya no agrupan visualmente todas las tumbas o bancas como si fueran un único objeto. Pasar al norte de un objeto dibuja al jugador detrás y pasar al sur lo dibuja delante.

Los árboles del nivel 2 tienen una colisión de tronco más amplia, y sus obstáculos sólidos pertenecen a la capa `Obstacles`. El movimiento del jugador de este nivel se aplica en `FixedUpdate`, mediante `Level2CharacterController`. El mapa nuevo se recorre en `LAYER 1`; las bardas mantienen su parte superior en `LAYER 2`.

El nivel 1, los prefabs originales de Cainos, sus scripts de movimiento/escaleras y su fuente se restauraron a la versión original. El nivel 2 utiliza copias independientes de los prefabs en `Assets/Prefabs/Level2` y de la fuente en `Assets/Fonts/Level2`. Las herramientas del nivel 2 ya no guardan ni reordenan el nivel 1.

## Verificación

`Herramientas > Nivel 2 > Validar escena` comprueba referencias, scripts, sprites, animaciones, tamaño, puerta oeste, luces activas y rutas a los juguetes. Los resultados están en `Nivel2-validacion.txt`. La reconstrucción también verifica rutas hacia los puntos principales, compara las luces con la referencia y comprueba mediante hashes que los archivos del nivel 1 y los recursos originales no cambiaron: `Nivel2-redisenado-validacion.txt`. La vista general actual está en [Nivel2-redisenado.png](Nivel2-redisenado.png).

`Herramientas > Nivel 2 > Rehacer mapa con entrada oeste` ejecuta `Level2MapDesigner`: reconstruye el escenario del nivel 2 y recoloca jugador, salida y juguetes, conservando la interfaz y las mecánicas. Es una acción manual que reemplaza la distribución de ese nivel; no se ejecuta automáticamente al abrir Unity. Para cambios puntuales, edita directamente los objetos de la escena. `Level2Builder` conserva el procedimiento inicial y rechaza una escena ya preparada para evitar duplicados.

La copia anterior al rediseño se conserva en `Backups/RedisenoNivel2/Cementerio_Nivel2-antes.unity`, fuera de Assets y excluida de Git. `Tools/UnityChecks/verify-level-isolation.cjs` comprueba los archivos originales contra Git y las referencias a prefabs independientes. Los informes anteriores de pruebas jugables corresponden al mapa previo; la validación del mapa actual es la indicada arriba.
