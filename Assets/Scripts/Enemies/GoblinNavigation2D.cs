using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class GoblinNavigation2D : MonoBehaviour
{
    public Vector2 areaSize = new Vector2(34f, 24f);
    public float cellSize = 0.25f;
    public Vector2 agentSize = new Vector2(0.26f, 0.28f);
    public float clearance = 0.02f;
    public LayerMask obstacleMask = 1 << 6;
    public bool showBlockedCells;

    private int width, height;
    private Vector2 minimum, probeSize;
    private bool[] walkable, closed;
    private float[] cost;
    private int[] parent;
    private byte[] testedEdges, clearEdges;
    private ContactFilter2D filter;

    private readonly Collider2D[] overlaps = new Collider2D[1];
    private readonly RaycastHit2D[] hits = new RaycastHit2D[1];
    private readonly SortedSet<Entry> open = new SortedSet<Entry>();

    private static readonly int[] DX =
        { -1, 0, 1, -1, 1, -1, 0, 1 };

    private static readonly int[] DY =
        { -1, -1, -1, 0, 0, 1, 1, 1 };

    private struct Entry : IComparable<Entry>
    {
        public int index;
        public float priority;

        public Entry(int index, float priority)
        {
            this.index = index;
            this.priority = priority;
        }

        public int CompareTo(Entry other)
        {
            int result = priority.CompareTo(other.priority);

            return result != 0
                ? result
                : index.CompareTo(other.index);
        }
    }

    private void Awake()
    {
        Rebuild();
    }

    [ContextMenu("Reconstruir navegacion")]
    public void Rebuild()
    {
        cellSize = Mathf.Max(0.1f, cellSize);

        width = Mathf.Max(
            1, Mathf.CeilToInt(areaSize.x / cellSize));

        height = Mathf.Max(
            1, Mathf.CeilToInt(areaSize.y / cellSize));

        minimum = (Vector2)transform.position -
                  new Vector2(width, height) * cellSize * 0.5f;

        probeSize = agentSize + Vector2.one * clearance * 2f;

        filter = new ContactFilter2D();
        filter.SetLayerMask(obstacleMask);
        filter.useTriggers = false;

        Physics2D.SyncTransforms();

        int count = width * height;

        walkable = new bool[count];
        closed = new bool[count];
        cost = new float[count];
        parent = new int[count];
        testedEdges = new byte[count];
        clearEdges = new byte[count];

        for (int i = 0; i < count; i++)
        {
            walkable[i] = Physics2D.OverlapBox(
                Point(i), probeSize, 0f, filter, overlaps) == 0;
        }
    }

    public bool Clear(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;

        if (delta.sqrMagnitude < 0.000001f)
        {
            return Physics2D.OverlapBox(
                from, probeSize, 0f, filter, overlaps) == 0;
        }

        return Physics2D.BoxCast(
            from,
            probeSize,
            0f,
            delta.normalized,
            filter,
            hits,
            delta.magnitude) == 0;
    }

    private Vector2 Point(int index)
    {
        return minimum + new Vector2(
            index % width + 0.5f,
            index / width + 0.5f) * cellSize;
    }

    private int Closest(Vector2 position, bool connectStart)
    {
        int result = -1;

        float best = connectStart
            ? cellSize * cellSize * 9f
            : float.PositiveInfinity;

        for (int i = 0; i < walkable.Length; i++)
        {
            if (!walkable[i])
                continue;

            float distance = (Point(i) - position).sqrMagnitude;

            if (distance >= best)
                continue;

            if (connectStart && !Clear(position, Point(i)))
                continue;

            best = distance;
            result = i;
        }

        return result;
    }

    private float Estimate(int a, int b)
    {
        int x = Mathf.Abs(a % width - b % width);
        int y = Mathf.Abs(a / width - b / width);

        return Mathf.Max(x, y) +
               0.41421356f * Mathf.Min(x, y);
    }

    private bool EdgeClear(int a, int b, int direction)
    {
        int bit = 1 << direction;

        if ((testedEdges[a] & bit) != 0)
            return (clearEdges[a] & bit) != 0;

        bool result = Clear(Point(a), Point(b));
        int opposite = 1 << (7 - direction);

        testedEdges[a] |= (byte)bit;
        testedEdges[b] |= (byte)opposite;

        if (result)
        {
            clearEdges[a] |= (byte)bit;
            clearEdges[b] |= (byte)opposite;
        }

        return result;
    }

    public bool FindPath(
        Vector2 from,
        Vector2 target,
        List<Vector2> path)
    {
        path.Clear();

        if (walkable == null)
            Rebuild();

        int start = Closest(from, true);
        int goal = Closest(target, false);

        if (start < 0 || goal < 0)
            return false;

        open.Clear();

        for (int i = 0; i < cost.Length; i++)
        {
            cost[i] = float.PositiveInfinity;
            parent[i] = -1;
            closed[i] = false;
        }

        cost[start] = 0f;
        open.Add(new Entry(start, Estimate(start, goal)));

        int best = start;
        float bestDistance = (Point(start) - target).sqrMagnitude;

        while (open.Count > 0)
        {
            Entry entry = open.Min;
            open.Remove(entry);

            int current = entry.index;

            if (closed[current])
                continue;

            closed[current] = true;

            float distance =
                (Point(current) - target).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = current;
            }

            if (current == goal)
            {
                best = current;
                break;
            }

            int x = current % width;
            int y = current / width;

            for (int d = 0; d < 8; d++)
            {
                int nx = x + DX[d];
                int ny = y + DY[d];

                if (nx < 0 || ny < 0 ||
                    nx >= width || ny >= height)
                    continue;

                int next = ny * width + nx;

                if (!walkable[next] || closed[next])
                    continue;

                bool diagonal = DX[d] != 0 && DY[d] != 0;

                // Impedir que atraviese esquinas en diagonal.
                if (diagonal &&
                    (!walkable[y * width + nx] ||
                     !walkable[ny * width + x]))
                    continue;

                if (!EdgeClear(current, next, d))
                    continue;

                float candidate = cost[current] +
                    (diagonal ? 1.41421356f : 1f);

                if (candidate >= cost[next])
                    continue;

                cost[next] = candidate;
                parent[next] = current;

                open.Add(new Entry(
                    next,
                    candidate + Estimate(next, goal)));
            }
        }

        for (int i = best; i >= 0; i = parent[i])
            path.Add(Point(i));

        path.Reverse();

        Vector2 max =
            minimum + new Vector2(width, height) * cellSize;

        bool inside =
            target.x >= minimum.x &&
            target.y >= minimum.y &&
            target.x <= max.x &&
            target.y <= max.y;

        if (best == goal && inside &&
            Clear(path[path.Count - 1], target))
        {
            path.Add(target);
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        Gizmos.DrawWireCube(
            transform.position,
            new Vector3(areaSize.x, areaSize.y, 0f));

        if (!showBlockedCells || walkable == null)
            return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);

        for (int i = 0; i < walkable.Length; i++)
        {
            if (!walkable[i])
            {
                Gizmos.DrawCube(
                    Point(i),
                    Vector3.one * cellSize * 0.85f);
            }
        }
    }
}