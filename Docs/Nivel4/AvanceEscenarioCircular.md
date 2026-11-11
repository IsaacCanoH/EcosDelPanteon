# Escenario cuadrado y boscoso — Nivel 4

Fecha: 4 de noviembre de 2026.

Escena: `Assets/Scenes/Cementerio_Nivel4.unity`. Este avance corresponde al escenario.

## Distribución

- Terreno cuadrado de 55 × 55 unidades (3,025 celdas), centrado en el origen. Los objetos mantienen la escala original.
- Cerco continuo en los cuatro lados, colocado sobre el último cuadro de suelo. Se reutilizan las piezas de piedra del nivel 1; no hay entrada.
- Las cuatro esquinas usan las piezas correspondientes, sin encimar extremos. El TilemapRenderer conserva el modo Individual para respetar la profundidad respecto del jugador.
- Circuito exterior de dos celdas de ancho, conectado con los caminos interiores y las orillas del canal.
- Se adaptaron los objetos que quedaban fuera del cuadrado o sobre los nuevos caminos, aprovechando también las esquinas.
- Se conservan 184 árboles, 228 lápidas, 28 sarcófagos, 155 arbustos, 495 grupos de hierba y 110 flores.
- Los árboles se mantienen en grupos pequeños: la mayor agrupación de copas revisada es de cinco árboles.
- Se conservan el canal, los tres estanques, los tres puentes, el memorial norte y la plaza este.
- Jugador de 1.40 unidades de altura y cámara de tamaño 6, con seguimiento y límites adaptados al cuadrado.
- Iluminación global blanca a intensidad 1, sin efecto de noche ni posprocesado.

## Jerarquía y assets

Se mantiene la estructura ambiental del nivel 1 bajo `SCENE/LAYER 1`, `LAYER 2` y `LAYER 3`: Tilemap, Props y Grass con sus categorías originales, incluido `Bench ` y `Hants`.

Se reutilizaron los assets existentes. Este ajuste no añadió arte, scripts ni carpetas de jerarquía. Los cambios de distribución están en la escena del nivel 4; no se aplicaron cambios a los prefabs ni materiales compartidos.

## Verificación

- Los 20 puntos revisados son accesibles: ambos lados de los tres puentes, los caminos interiores, las plazas, las cuatro esquinas y sectores de los cuatro bordes.
- Muestreo de navegación con el tamaño del collider del jugador: 8,172 de 8,283 posiciones libres conectadas (98.7 %). Los pequeños huecos aislados están entre objetos decorativos.
- Los tres puentes se cruzaron en ambos sentidos usando las flechas del Input System y la física real del jugador.
- Las 227 celdas de agua fuera de los nueve pasos de los puentes conservan colisión. El agua mantiene sus 236 celdas y 63 colliders independientes.
- El contorno rectangular está cerrado y respondió a las 128 comprobaciones radiales de colisión.
- Todos los objetos decorativos, incluidas sus sombras, quedan dentro del terreno con margen respecto del cerco.
- Sin scripts faltantes ni objetos de decoración pendientes de activar.
- Se guardó y recargó la escena: conserva las dimensiones, los recuentos de objetos, el cerco, el tamaño del jugador y el seguimiento de cámara.
- Los dispositivos y ajustes temporales de entrada usados en las pruebas se retiraron al terminar.

Durante un cambio de Play apareció un aviso de reconexión WebSocket de Unity MCP; las consultas y pruebas posteriores funcionaron. No se registraron errores de juego.

## Revisar

La escena queda guardada y abierta fuera de Play. Pulsa Play y recorre con WASD o las flechas. El inicio sigue en el camino sur.

Capturas:

- `Captures/Nivel4_MapaCuadrado_Panoramica.png`
- `Captures/Nivel4_MapaCuadrado_Esquina.png`

Respaldo previo y registros: `Library/CodexBackups/Nivel4_MapaCuadrado_20261104_1793840002101`.
