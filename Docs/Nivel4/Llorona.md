# La Llorona — enemigo y animaciones del nivel 4

Fecha: 5 de noviembre de 2026.

## Assets

- Prefab reutilizable: `Assets/Prefabs/Enemies/PF_Llorona.prefab`.
- Controlador: `Assets/Animations/Llorona/Llorona.controller`.
- Comportamiento: `Assets/Scripts/Enemies/LloronaEnemyAI.cs`.
- Arte original: `Assets/Sprites/Enemies/Llorona`.

Hay 32 clips: reposo, movimiento, ataque, grito, golpe, muerte, aparición y desaparición, cada uno en cuatro direcciones. Se usan las hojas principales; las versiones de hitboxes de depuración no se muestran.

Las hojas se cortaron en cuadros de 80 × 80, a 24 píxeles por unidad, con filtro Point, sin compresión ni mipmaps. El pivote común está en (0.5, 0.2), alineado con el suelo. Las imágenes originales no se editaron.

En las hojas, las filas son abajo, izquierda, derecha y arriba. En el Animator, Direction usa 0 = abajo, 1 = derecha, 2 = izquierda, 3 = arriba. ActionState corresponde al enum EnemyState del script. Solo reposo y movimiento se repiten; los demás clips finalizan mediante eventos de animación.

## Integración

La instancia `Llorona` está en la plaza este de `Cementerio_Nivel4`, en (11, 0.25). Como en el nivel 1, el enemigo y `Navigation` son raíces independientes; la jerarquía ambiental se conserva.

La navegación reutiliza GoblinNavigation2D, con área 56 × 56, celda 0.35 y agente 0.42 × 0.28. Tiene en cuenta los colliders estáticos del escenario. El enemigo usa un Rigidbody2D dinámico sin gravedad y un collider pequeño en los pies.

Detecta al jugador con línea de visión, lo persigue rodeando obstáculos y puede usar ataque cercano o grito. Los impactos ocurren en el cuadro correspondiente del ataque, una sola vez por acción. Al perder al objetivo puede regresar a su posición inicial.

Admite IDamageable, tiene reacción de golpe y termina la animación de muerte antes de retirarse. Despawn() reproduce la desaparición antes de retirar el objeto.

## Alcance de este avance

El jugador actual del nivel 4 todavía no tiene PlayerHealth ni armas. La Llorona detecta, persigue y reproduce sus ataques en Play; el daño al jugador se aplica cuando el objetivo dispone de PlayerHealth. No se añadieron vida, armas, interfaz ni reglas de victoria o derrota al jugador.

El daño usa la cantidad de vida de un corazón de PlayerHealth para respetar la escala de salud del objetivo. Los valores iniciales de salud, velocidad y alcance se pueden ajustar en el Inspector.

## Validación

- Los 32 clips conservan sus cuadros y direcciones correctos, sin sprites faltantes.
- Los ocho estados se observaron en Play, con 35 cuadros distintos registrados.
- Se probó persecución y daño de ataque y grito sobre un objetivo temporal con PlayerHealth: un impacto de un corazón por acción.
- Se probó IDamageable, reacción al daño, muerte completa y evento de muerte único.
- Se probó aparición sin colisión hasta completar la animación y desaparición completa antes de retirar el objeto.
- La navegación encontró una ruta completa de 83 puntos desde la plaza hasta el jugador; todos sus tramos resultaron despejados para el agente.
- Se recargó la escena guardada y se verificaron la instancia, el prefab, los sprites y las referencias a jugador y navegación.
- Sin scripts faltantes. Se comprobaron 21 archivos protegidos, incluidas las imágenes originales, otras escenas y scripts compartidos: todos conservaron su contenido.
- Los objetivos y actores temporales de prueba se retiraron al salir de Play. Se observó un aviso transitorio de reconexión de Unity MCP; no se registraron errores de juego.

Captura: `Captures/Nivel4_Llorona_Integrada.png`.

Respaldo y registros: `Library/CodexBackups/Nivel4_Llorona_Continuacion_20261105_1793923899828`.

## Revisar

Selecciona Llorona en la jerarquía para verla en Scene. En Play, acércate a la plaza del lado derecho del mapa para observar detección, persecución y ataques. El jugador conserva su punto de inicio habitual.
