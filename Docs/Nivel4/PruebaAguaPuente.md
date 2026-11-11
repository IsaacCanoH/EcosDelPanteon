# Prueba de agua y puente — Nivel 4

Fecha: 2 de noviembre de 2026.

Esta escena contiene una muestra pequeña para revisar la dirección visual y el cruce del agua antes de construir el nivel completo.

## Abrir y probar

1. Abre `Assets/Scenes/Cementerio_Nivel4.unity`.
2. Pulsa Play.
3. Mueve al jugador con WASD o las flechas.
4. Cruza el puente hacia la otra orilla y vuelve.
5. Intenta entrar en el agua fuera del puente y salir por los pretiles: el personaje debe detenerse.

La escena quedó abierta fuera de Play, guardada y con el jugador en el inicio oeste.

## Contenido

- Dos orillas, un canal con agua animada a 4 fotogramas por segundo y un puente de piedra.
- Ocho tumbas y algunos árboles, piedras y luminarias del paquete existente.
- Jugador con el mismo sistema de movimiento del proyecto.
- Escala de sprites de 32 píxeles por unidad, filtro Point, sin compresión ni mipmaps.
- Puente reutilizable como prefab; pretil frontal separado para ordenar correctamente al personaje al cruzar.
- Colisiones independientes para agua, pretiles y límites del terreno.

## Archivos

- Escena: `Assets/Scenes/Cementerio_Nivel4.unity`
- Agua: `Assets/Sprites/Level4/WaterBridgeSample/Agua_4Frames.png`
- Puente: `Assets/Sprites/Level4/WaterBridgeSample/Puente_Piedra.png`
- Tile animado: `Assets/Tiles/Level4/Agua_Animada_Prueba.asset`
- Prefab: `Assets/Prefabs/Environment/Level4/PF_Puente_Piedra_Prueba.prefab`
- Tipo de tile: `Assets/Scripts/Level4/CemeteryWaterTile.cs`
- Captura: `Captures/Nivel4_Prueba_AguaPuente.png`
- Originales: `ArtSources/Nivel4/PruebaAguaPuente/Agua_Original.png` y `Puente_Original.png`

## Verificación

Se probaron teclas de Input System con el componente de movimiento y colisiones Rigidbody2D reales:

- Cruce oeste a este: correcto.
- Cruce este a oeste: correcto.
- Agua bloqueada desde la orilla oeste: correcto.
- Agua bloqueada desde la orilla este: correcto.
- Pretil norte: correcto.
- Pretil sur: correcto.
- Animación del agua: se observaron los cuatro fotogramas.
- Un único AudioListener activo: correcto.

Se usó temporalmente un teclado de prueba para automatizar el recorrido con Unity en segundo plano. Se retiró al finalizar y se restauró la configuración de entrada. Las posiciones usadas para comprobar las orillas fueron temporales en Play.

Se corrigió la configuración de tamaño heredada del jugador únicamente en la instancia de esta escena. El prefab original conserva su contenido.

Sin errores de scripts ni componentes faltantes. Al entrar en Play hubo un aviso de reconexión de Unity MCP; la conexión se recuperó y las pruebas terminaron correctamente.

Se comprobaron por hash los niveles 1 y 2, el menú, el material compartido, el jugador original, el controlador de movimiento, Input Actions, Build Settings y LevelFourEnvironmentBuilder: sin cambios. No se ejecutó ni se modificó el constructor del escenario completo que ya existía.

La escena no se añadió al menú ni a Build Settings. No se generó una compilación de Windows.

## Generación de los sprites

Se utilizó la herramienta integrada de generación de imágenes (`image_gen`), sin CLI ni clave API. Los originales se guardaron en ArtSources. La importación a Unity normaliza la resolución por muestreo puntual, recorta el margen transparente del puente y mantiene su canal alfa.

### Prompt del agua

```text
Use case: stylized-concept. Asset type: production 2D RPG pixel-art water tile animation sheet.
Create a perfectly square sprite sheet containing EXACTLY 4 edge-to-edge equal square water tiles in a 2 by 2 grid, no margins and no separators. These are four consecutive animation frames of the SAME seamless repeating dark cemetery canal water. All four frames have identical base color and density; only a few short angular ripples shift slightly from frame to frame. It must tile seamlessly in every direction. Flat opaque dark teal base #193D3B, deepest areas #102B2A, restrained mossy teal ripple highlights #456D61 and #608576. Sparse simple horizontal stepped ripples, quiet water; no foamy waves or gradient lighting. Authentic low-resolution hand-placed pixel art matching a simple 32x32-tile top-down Unity cemetery pack with olive grass and gray stone. Each quadrant represents only 32 by 32 logical pixels, enlarged with crisp nearest-neighbor square pixel clusters; the whole image represents 64 by 64 logical pixels. Simple readable game texture, not an illustration or scene.
No shore, no land, no bridge, no rocks, no sky, no reflections of objects, no grid lines, no text, no labels, no frames, no transparent pixels, no watermarks, no blur, no smooth gradients. Fill the entire image with the 4 equal water frames.
```

### Prompt del puente

```text
Use case: stylized-concept. Asset type: single production 2D RPG pixel-art bridge sprite with transparent background.
A small aged stone footbridge for a Mexican cemetery game, viewed from a fixed top-down RPG camera with just a shallow visible southern/front stone face, matching a simple 32x32-tile Unity 2D pixel-art pack. The walkable deck runs horizontally from LEFT to RIGHT across a narrow canal. Both ends are OPEN so a character can enter from the west and leave east. One continuous gray stone paved deck, two low stone parapets along the NORTH and SOUTH edges, small darker front arch recesses, restrained moss on corners. Keep the central horizontal walking strip clear. Human-scale proportions: about 112 logical pixels wide by 64 logical pixels tall, equivalent to roughly 3.5 by 2 game tiles; not an enormous monumental bridge. Side rails are ordinary low walls, not tall buildings. Chunky crisp pixel clusters, few muted olive-gray stone colors with a dark brown-gray outline and small warm stone highlights. Flat game-sprite lighting with upper-left highlights. Center the one sprite with modest empty transparent padding, no cast shadow beyond its own small underside.
Actual transparent background. Isolated bridge only. No water, no land, no grass scenery, no character, no buildings, no text, no numbers, no UI, no checkerboard background, no perspective vanishing point, no 3D render, no photorealism, no smooth painterly shading. The deck must unmistakably be traversable LEFT TO RIGHT.
```

## Ajuste de altura del jugador

Se comparo el SpriteRenderer con el nivel 1: la altura de referencia es 1.40 unidades. La muestra paso de 1.00 a 1.40 con escala uniforme, manteniendo las proporciones del sprite. La colision conserva sus dimensiones y posicion en el mundo; solo cambia el tamaño visible. La camara y los otros niveles no se modificaron.
