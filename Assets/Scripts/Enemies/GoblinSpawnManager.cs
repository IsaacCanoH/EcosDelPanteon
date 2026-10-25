using System.Collections.Generic;
using UnityEngine;

// Navegacion (-200), apariciones (-150), jugador (-100), IA (0).
[DefaultExecutionOrder(-150)]
public class GoblinSpawnManager : MonoBehaviour
{
    public GoblinAI[] goblins;
    public Transform[] spawnPoints;
    public Transform player;
    public GoblinNavigation2D navigation;
    [Min(0f)] public float minimumPlayerDistance = 7f;
    [Min(0f)] public float minimumSeparation = 6f;

    private void Awake()
    {
        if (!PlaceGoblins())
            Debug.LogWarning(
                "No hay suficientes puntos seguros para los duendes. Se conservan sus posiciones originales.",
                this);
    }

    private bool PlaceGoblins()
    {
        if (!Application.isPlaying || player == null || navigation == null ||
            goblins == null || spawnPoints == null ||
            goblins.Length == 0 || spawnPoints.Length < goblins.Length)
            return false;

        var uniqueGoblins = new HashSet<GoblinAI>();
        foreach (GoblinAI goblin in goblins)
            if (goblin == null || !uniqueGoblins.Add(goblin))
                return false;

        // Validar toda la distribucion antes de mover un solo personaje.
        bool[,] allowed = new bool[goblins.Length, spawnPoints.Length];
        var route = new List<Vector2>();
        for (int g = 0; g < goblins.Length; g++)
        {
            BoxCollider2D collider = goblins[g].GetComponent<BoxCollider2D>();
            if (collider == null) return false;
            Vector2 offset = goblins[g].transform.TransformVector(collider.offset);
            Vector2 size = Vector2.Scale(collider.size, goblins[g].transform.lossyScale);
            size = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y)) +
                Vector2.one * navigation.clearance * 2f;

            for (int p = 0; p < spawnPoints.Length; p++)
            {
                Transform point = spawnPoints[p];
                if (point == null || !point.gameObject.activeInHierarchy ||
                    Vector2.Distance(point.position, player.position) < minimumPlayerDistance)
                    continue;

                Vector2 center = (Vector2)point.position + offset;
                if (!navigation.Clear(center, center)) continue;

                bool blocked = false;
                foreach (Collider2D obstacle in Physics2D.OverlapBoxAll(center, size, 0f))
                {
                    if (!obstacle.isTrigger && obstacle.GetComponentInParent<GoblinAI>() == null)
                    {
                        blocked = true;
                        break;
                    }
                }
                if (blocked) continue;

                Vector2 goal = (Vector2)player.position + offset;
                // FindPath puede devolver una ruta parcial: exigir llegar al destino.
                if (!navigation.FindPath(center, goal, route) || route.Count == 0 ||
                    Vector2.Distance(route[route.Count - 1], goal) > 0.35f)
                    continue;

                allowed[g, p] = true;
            }
        }

        int[] order = new int[spawnPoints.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        for (int i = order.Length - 1; i > 0; i--)
        {
            int next = Random.Range(0, i + 1);
            int swap = order[i];
            order[i] = order[next];
            order[next] = swap;
        }

        int[] chosen = new int[goblins.Length];
        if (!AssignPoint(0, order, allowed, chosen)) return false;

        for (int g = 0; g < goblins.Length; g++)
        {
            Vector3 position = spawnPoints[chosen[g]].position;
            position.z = goblins[g].transform.position.z;
            goblins[g].transform.position = position;
            Rigidbody2D body = goblins[g].GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = position;
                body.velocity = Vector2.zero;
            }
        }
        Physics2D.SyncTransforms();
        return true;
    }

    private bool AssignPoint(int goblin, int[] order, bool[,] allowed, int[] chosen)
    {
        if (goblin == goblins.Length) return true;
        foreach (int point in order)
        {
            if (!allowed[goblin, point]) continue;
            bool separated = true;
            for (int previous = 0; previous < goblin; previous++)
            {
                if (point == chosen[previous] ||
                    Vector2.Distance(spawnPoints[point].position,
                        spawnPoints[chosen[previous]].position) < minimumSeparation)
                {
                    separated = false;
                    break;
                }
            }
            if (!separated) continue;
            chosen[goblin] = point;
            if (AssignPoint(goblin + 1, order, allowed, chosen)) return true;
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;
        Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.8f);
        foreach (Transform point in spawnPoints)
            if (point != null)
                Gizmos.DrawWireSphere(point.position, 0.25f);
    }
}
