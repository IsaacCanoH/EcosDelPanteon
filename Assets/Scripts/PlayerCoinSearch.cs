using Cainos.PixelArtTopDown_Basic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(GoldCoinCollector), typeof(PlayerHealth))]
public class PlayerCoinSearch : MonoBehaviour
{
    public GoldCoinPickup[] coins;
    public CoinSearchUI searchUI;
    public IrisInNivel opening;
    public LayerMask obstacleMask;
    [Min(0.1f)] public float searchSeconds = 1.5f;

    private GoldCoinCollector collector;
    private PlayerHealth health;
    private TopDownCharacterController movement;
    private Rigidbody2D body;
    private Animator animator;
    private GoldCoinPickup activeTarget;
    private Vector2 searchOrigin;
    private float elapsed;
    private bool movementLocked;
    private bool requireRelease;

    public bool IsSearching { get { return activeTarget != null; } }
    public float Progress { get { return Mathf.Clamp01(elapsed / Mathf.Max(0.1f, searchSeconds)); } }

    private void Awake()
    {
        collector = GetComponent<GoldCoinCollector>();
        health = GetComponent<PlayerHealth>();
        movement = GetComponent<TopDownCharacterController>();
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (health == null) health = GetComponent<PlayerHealth>();
        if (health != null) health.OnDamaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (health != null) health.OnDamaged -= OnDamaged;
        CancelSearch();
        if (searchUI != null) searchUI.SetSearchTarget(null, 0f, false);
    }

    private void Update()
    {
        bool move = GameControls.Move.sqrMagnitude > 0.001f;
        ProcessInput(GameControls.SearchHeld, move, Time.deltaTime);
    }

    private GoldCoinPickup FindTarget()
    {
        if (coins == null || collector == null) return null;

        foreach (GoldCoinPickup coin in coins)
        {
            if (coin == null || !coin.CanSearch(collector)) continue;
            Vector2 point = coin.transform.position;
            if (Vector2.Distance(transform.position, point) > coin.searchRadius) continue;
            // No permite buscar a traves de paredes.
            Vector2 origin = (Vector2)transform.position + Vector2.up * 0.15f;
            if (Physics2D.Linecast(origin, point, obstacleMask).collider != null) continue;
            return coin;
        }
        return null;
    }

    private void ProcessInput(bool held, bool wantsToMove, float deltaTime)
    {
        if (!held) requireRelease = false;

        if (Time.timeScale <= 0f || health == null || health.currentHealth <= 0 ||
            collector == null || collector.collectedCoins >= collector.requiredCoins ||
            (opening != null && opening.isActiveAndEnabled) ||
            (movement != null && !movement.enabled && !movementLocked))
        {
            CancelSearch();
            if (searchUI != null) searchUI.SetSearchTarget(null, 0f, false);
            return;
        }

        GoldCoinPickup target = FindTarget();
        bool displaced = IsSearching &&
            Vector2.Distance(searchOrigin, transform.position) > 0.05f;

        if (activeTarget != null && (activeTarget != target || displaced))
        {
            CancelSearch();
            requireRelease = held;
        }

        if (target == null || !held || wantsToMove || requireRelease)
        {
            CancelSearch();
            if (searchUI != null) searchUI.SetSearchTarget(target, 0f, false);
            return;
        }

        if (activeTarget == null)
        {
            activeTarget = target;
            elapsed = 0f;
            searchOrigin = transform.position;
            if (movement != null && movement.enabled)
            {
                movement.enabled = false;
                movementLocked = true;
            }
        }

        if (body != null) body.velocity = Vector2.zero;
        if (animator != null) animator.SetBool("IsMoving", false);

        elapsed += Mathf.Max(0f, deltaTime);
        if (searchUI != null) searchUI.SetSearchTarget(target, Progress, true);

        if (elapsed < Mathf.Max(0.1f, searchSeconds)) return;

        target.TryCollect(collector);
        CancelSearch();
        requireRelease = true;
        if (searchUI != null) searchUI.SetSearchTarget(null, 0f, false);
    }

    private void OnDamaged()
    {
        InterruptSearch();
    }

    public void InterruptSearch()
    {
        CancelSearch();
        requireRelease = true;
        if (searchUI != null) searchUI.SetSearchTarget(null, 0f, false);
    }

    private void CancelSearch()
    {
        activeTarget = null;
        elapsed = 0f;
        if (movementLocked && movement != null)
            movement.enabled = true;
        movementLocked = false;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused) return;
        InterruptSearch();
    }
}
