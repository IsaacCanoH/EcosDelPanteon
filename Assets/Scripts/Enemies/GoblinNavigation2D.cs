using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
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
    [Tooltip("Incluir los colliders estaticos de todas las capas, sin bloquear a los personajes.")]
    public bool includeStaticSceneColliders;

    private int width, height;
    private Vector2 minimum, probeSize;
    private bool[] walkable;
    private SearchBuffers immediateSearch;
    private SearchBuffers queuedSearch;
    private readonly LinkedList<PathRequest> requests = new LinkedList<PathRequest>();
    private IEnumerator<int> activeSearch;
    private const double PathBudgetMs = 1.25;

    internal bool QueueCombatPaths { get; private set; }
    private byte[] testedEdges, clearEdges;
    private ContactFilter2D filter;

    private readonly Collider2D[] overlaps = new Collider2D[32];
    private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
    internal sealed class PathRequest
    {
        internal readonly List<Vector2> Route = new List<Vector2>();
        internal readonly LinkedListNode<PathRequest> Node;
        internal Vector2 From;
        internal Vector2 Target;
        internal bool Ready;

        internal PathRequest() => Node = new LinkedListNode<PathRequest>(this);

        internal bool Take(List<Vector2> destination)
        {
            if (!Ready) return false;

            Ready = false;
            destination.Clear();
            destination.AddRange(Route);
            return true;
        }
    }

    private sealed class SearchBuffers
    {
        internal readonly bool[] Closed;
        internal readonly float[] Cost;
        internal readonly int[] Parent;
        internal readonly SortedSet<Entry> Open = new SortedSet<Entry>();
        internal int Closest;

        internal SearchBuffers(int count)
        {
            Closed = new bool[count];
            Cost = new float[count];
            Parent = new int[count];
        }
    }

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
        QueueCombatPaths = gameObject.scene.name == "Cementerio_Nivel4";
        Rebuild();
    }

    internal bool RequestPath(PathRequest request, Vector2 from, Vector2 target)
    {
        if (request.Node.List != null || request.Ready) return false;

        request.From = from;
        request.Target = target;
        requests.AddLast(request.Node);
        return true;
    }

    internal void CancelPath(PathRequest request)
    {
        request.Ready = false;
        if (request.Node.List != requests) return;

        if (requests.First == request.Node)
        {
            activeSearch?.Dispose();
            activeSearch = null;
        }
        requests.Remove(request.Node);
    }

    private void Update()
    {
        if (requests.First == null || Time.timeScale == 0f) return;

        long deadline = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * PathBudgetMs / 1000.0);
        do
        {
            var request = requests.First.Value;
            if (activeSearch == null)
            {
                if (queuedSearch == null) queuedSearch = new SearchBuffers(walkable.Length);
                activeSearch = Search(request.From, request.Target, request.Route, queuedSearch).GetEnumerator();
            }

            if (!activeSearch.MoveNext())
            {
                activeSearch.Dispose();
                activeSearch = null;
                requests.RemoveFirst();
                request.Ready = true;
            }
        }
        while (requests.First != null && Stopwatch.GetTimestamp() < deadline);
    }

    private void OnDisable()
    {
        activeSearch?.Dispose();
        activeSearch = null;
        requests.Clear();
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
        filter.SetLayerMask(includeStaticSceneColliders ? ~0 : obstacleMask.value);
        filter.useTriggers = false;

        Physics2D.SyncTransforms();

        int count = width * height;

        activeSearch?.Dispose();
        activeSearch = null;
        requests.Clear();
        walkable = new bool[count];
        immediateSearch = new SearchBuffers(count);
        queuedSearch = null;
        testedEdges = new byte[count];
        clearEdges = new byte[count];

        for (int i = 0; i < count; i++)
        {
            int overlapCount = Physics2D.OverlapBox(
                Point(i), probeSize, 0f, filter, overlaps);
            walkable[i] = true;
            for (int hit = 0; hit < overlapCount; hit++)
            {
                if (!IsObstacle(overlaps[hit])) continue;
                walkable[i] = false;
                break;
            }
        }
    }

    public bool Clear(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        if (delta.sqrMagnitude < 0.000001f)
        {
            int count = Physics2D.OverlapBox(
                from, probeSize, 0f, filter, overlaps);
            for (int i = 0; i < count; i++)
                if (IsObstacle(overlaps[i])) return false;
            return true;
        }

        int hitsCount = Physics2D.BoxCast(
            from, probeSize, 0f, delta.normalized, filter,
            hits, delta.magnitude);
        for (int i = 0; i < hitsCount; i++)
            if (IsObstacle(hits[i].collider)) return false;
        return true;
    }

    public bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        if (walkable == null) Rebuild();
        int count = Physics2D.Linecast(from, to, filter, hits);
        for (int i = 0; i < count; i++)
            if (IsObstacle(hits[i].collider)) return false;
        return true;
    }

    private bool IsObstacle(Collider2D collider)
    {
        if (collider == null) return false;
        if (!includeStaticSceneColliders) return true;
        Rigidbody2D attachedBody = collider.attachedRigidbody;
        return attachedBody == null || attachedBody.bodyType == RigidbodyType2D.Static;
    }

    private Vector2 Point(int index)
    {
        return minimum + new Vector2(
            index % width + 0.5f,
            index / width + 0.5f) * cellSize;
    }

    private IEnumerable<int> FindClosest(Vector2 position, bool connectStart, SearchBuffers search)
    {
        int result = -1;

        float best = connectStart
            ? cellSize * cellSize * 9f
            : float.PositiveInfinity;

        for (int i = 0; i < walkable.Length; i++)
        {
            if (i != 0 && (i & 127) == 0) yield return 0;
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

        search.Closest = result;
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
        if (walkable == null)
            Rebuild();

        foreach (int slice in Search(from, target, path, immediateSearch)) { }
        return path.Count > 0;
    }

    private IEnumerable<int> Search(Vector2 from, Vector2 target, List<Vector2> path, SearchBuffers search)
    {
        path.Clear();

        foreach (int slice in FindClosest(from, true, search)) yield return slice;
        int start = search.Closest;
        if (start < 0) yield break;

        foreach (int slice in FindClosest(target, false, search)) yield return slice;
        int goal = search.Closest;
        if (goal < 0) yield break;

        var open = search.Open;
        var cost = search.Cost;
        var parent = search.Parent;
        var closed = search.Closed;

        open.Clear();
        Array.Clear(closed, 0, closed.Length);
        for (int i = 0; i < cost.Length; i++)
            cost[i] = float.PositiveInfinity;

        parent[start] = -1;

        cost[start] = 0f;
        open.Add(new Entry(start, Estimate(start, goal)));

        int best = start;
        float bestDistance = (Point(start) - target).sqrMagnitude;

        int expansions = 0;
        while (open.Count > 0)
        {
            if (++expansions == 9)
            {
                expansions = 1;
                yield return 0;
            }

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