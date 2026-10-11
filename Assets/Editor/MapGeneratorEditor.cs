using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;

public class MapGeneratorEditor : EditorWindow
{
    Tilemap targetTilemap;
    TileBase floorTile;
    Vector2Int mapSize = new Vector2Int(30, 30);
    Vector2Int startPosition = new Vector2Int(0, 0);

    Transform propsParent;
    GameObject[] propPrefabs = new GameObject[1];
    int propCount = 30;

    [MenuItem("Herramientas/Generador de Mapa")]
    public static void ShowWindow()
    {
        GetWindow<MapGeneratorEditor>("Generador de Mapa");
    }

    void OnGUI()
    {
        GUILayout.Label("1. Expandir Suelo (Tilemap)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Arrastra aquí el Tilemap (ej. 'Layer 1 - Grass') y el bloque de suelo que quieres pintar.", MessageType.Info);
        
        targetTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap a expandir", targetTilemap, typeof(Tilemap), true);
        floorTile = (TileBase)EditorGUILayout.ObjectField("Tile a usar", floorTile, typeof(TileBase), false);
        
        startPosition = EditorGUILayout.Vector2IntField("Posición de Inicio (X,Y)", startPosition);
        mapSize = EditorGUILayout.Vector2IntField("Tamaño a expandir (Ancho, Alto)", mapSize);

        if (GUILayout.Button("Pintar Suelo", GUILayout.Height(30)))
        {
            PaintFloor();
        }

        GUILayout.Space(20);
        
        GUILayout.Label("2. Esparcir Objetos (Tumbas, Árboles, etc)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Arrastra aquí el Objeto 'Props' de tu jerarquía para agruparlos, y luego añade los Prefabs del asset Cainos.", MessageType.Info);
        
        propsParent = (Transform)EditorGUILayout.ObjectField("Objeto Padre ('Props')", propsParent, typeof(Transform), true);
        
        int arraySize = EditorGUILayout.IntField("Tipos de Prefabs distintos", propPrefabs.Length);
        if (arraySize != propPrefabs.Length)
        {
            System.Array.Resize(ref propPrefabs, Mathf.Max(0, arraySize));
        }

        for (int i = 0; i < propPrefabs.Length; i++)
        {
            propPrefabs[i] = (GameObject)EditorGUILayout.ObjectField("Prefab " + (i + 1), propPrefabs[i], typeof(GameObject), false);
        }

        propCount = EditorGUILayout.IntField("Cantidad total a generar", propCount);

        if (GUILayout.Button("Esparcir Objetos al Azar", GUILayout.Height(30)))
        {
            ScatterProps();
        }
    }

    void PaintFloor()
    {
        if (targetTilemap == null || floorTile == null)
        {
            EditorUtility.DisplayDialog("Error", "Falta asignar el Tilemap o el Tile de suelo.", "OK");
            return;
        }

        // Permite usar Ctrl+Z para deshacer
        Undo.RecordObject(targetTilemap, "Pintar Suelo");

        for (int x = 0; x < mapSize.x; x++)
        {
            for (int y = 0; y < mapSize.y; y++)
            {
                Vector3Int pos = new Vector3Int(startPosition.x + x, startPosition.y + y, 0);
                targetTilemap.SetTile(pos, floorTile);
            }
        }
        
        EditorUtility.SetDirty(targetTilemap);
        Debug.Log($"Se pintaron {mapSize.x * mapSize.y} tiles correctamente en {targetTilemap.name}.");
    }

    void ScatterProps()
    {
        if (propsParent == null)
        {
            EditorUtility.DisplayDialog("Error", "Falta asignar el Objeto Padre donde irán los props.", "OK");
            return;
        }
        if (propPrefabs.Length == 0 || propPrefabs[0] == null)
        {
            EditorUtility.DisplayDialog("Error", "Falta asignar al menos un Prefab.", "OK");
            return;
        }

        for (int i = 0; i < propCount; i++)
        {
            GameObject prefabToSpawn = propPrefabs[Random.Range(0, propPrefabs.Length)];
            if (prefabToSpawn == null) continue;

            // Calcular posición aleatoria basada en el área del suelo definido
            float randomX = Random.Range((float)startPosition.x, startPosition.x + mapSize.x);
            float randomY = Random.Range((float)startPosition.y, startPosition.y + mapSize.y);
            
            Vector3 spawnPos = new Vector3(randomX, randomY, 0);

            GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(prefabToSpawn, propsParent);
            newObj.transform.position = spawnPos;
            
            // Permite usar Ctrl+Z para deshacer la creación de los objetos
            Undo.RegisterCreatedObjectUndo(newObj, "Crear Prop Aleatorio");
        }
        
        Debug.Log($"Se generaron {propCount} props aleatoriamente dentro de {propsParent.name}.");
    }
}
