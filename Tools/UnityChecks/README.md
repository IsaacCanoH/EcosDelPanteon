# Pruebas del nivel 2

Las clases de esta carpeta son un arnés de pruebas para una **copia temporal** del proyecto, fuera del juego distribuido.

1. Copia `Level2RuntimeProbe.cs` a `Assets/Scripts` en la copia temporal.
2. Copia `Level2PlayVerification.cs` a `Assets/Editor` en esa copia.
3. Ejecuta Unity con `-batchmode -projectPath <copia> -executeMethod Level2PlayVerification.Begin -logFile <registro>`. No uses `-quit`: el arnés cierra Unity al terminar. La captura necesita gráficos, por lo que esta ejecución omite `-nographics`.

El arnés verifica recolección sin duplicados, acecho sin daño, golpes, lanzamiento a distancia media/larga, cadencia entre fases, impactos contra obstáculos y colisión/orden visual de árboles y bancas. Cambia temporalmente la vida y las posiciones para aislar las comprobaciones; no guarda esos cambios en la escena.

El resultado se escribe en `Documentation/Nivel2-pruebas-jugables.txt`, y Unity devuelve código 0 si todas las comprobaciones pasan. La validación estática también está disponible en `Herramientas > Nivel 2 > Validar escena`, sin instalar este arnés.
