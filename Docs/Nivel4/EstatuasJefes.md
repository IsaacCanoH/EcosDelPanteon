# Estatuas de los jefes — Nivel 4

La plaza de Cementerio_Nivel4 tiene la estatua del Niño a la izquierda (8, -2.7) y la Bruja a la derecha (14, -2.7). Sustituyen únicamente las dos estatuas de esa plaza.

## Prefabs

- Assets/Prefabs/Environment/Level4/PF_Estatua_Nino.prefab
- Assets/Prefabs/Environment/Level4/PF_Estatua_Bruja.prefab

Cada prefab contiene Figura, Pedestal y Shadow. Conserva el PolygonCollider2D original del pedestal, sin componentes de combate ni animación.

## Apariencia

El Niño utiliza el primer cuadro de Child_Idle_Down.anim y su textura original. La Bruja utiliza el cuadro 0 de CreateWitchSpriteFrame, el generador que usa el jefe del nivel 3; esa textura se conserva en TX_Bruja_Original_Referencia.asset.

Las figuras son mallas planas con UV sobre esos dibujos. La silueta del Niño excluye el resplandor exterior; la Bruja excluye la sombra y partículas externas. El material BossStatueStone convierte los colores a una paleta de piedra y conserva la iluminación 2D. El pedestal y su sombra proceden de la estatua original.

## Revisión

Se revisó la apariencia en Play y se volvió a cargar la escena guardada. Los dos prefabs conservan sus mallas y materiales. Sus colisiones coinciden con las originales y las otras dos estatuas del norte permanecen intactas.

Captura: Captures/Nivel4_EstatuasJefes.png.
