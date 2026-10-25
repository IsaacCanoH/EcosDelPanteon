using Cainos.PixelArtTopDown_Basic;
using UnityEngine;

[DefaultExecutionOrder(50)]
[RequireComponent(typeof(PlayerHealth), typeof(GoldCoinCollector))]
public class PlayerStoneThrow : MonoBehaviour
{
    [Min(1)] public int startingStones = 6;
    [Min(0.1f)] public float throwCooldown = 0.7f;
    public StoneProjectile projectilePrefab;
    public IrisInNivel opening;

    [Header("Sonidos de las piedras")]
    public AudioSource throwAudio;
    public AudioClip throwSound;
    public AudioSource impactAudio;
    public AudioClip impactSound;

    private PlayerHealth health;
    private GoldCoinCollector collector;
    private PlayerCoinSearch search;
    private TopDownCharacterController movement;
    private Animator animator;
    private Collider2D body;
    private float nextThrowAt;
    private float throwAnimationUntil;
    private bool hasThrowAnimation;
    private static readonly int Throwing = Animator.StringToHash("IsThrowing");

    public int RemainingStones { get; private set; }
    public float CooldownRemaining { get { return Mathf.Max(0f, nextThrowAt - Time.time); } }
    public bool CanProjectilesContinue
    {
        get { return isActiveAndEnabled && health != null && health.currentHealth > 0 &&
            collector != null && !collector.IsLevelFinished; }
    }

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        collector = GetComponent<GoldCoinCollector>();
        search = GetComponent<PlayerCoinSearch>();
        movement = GetComponent<TopDownCharacterController>();
        animator = GetComponent<Animator>();
        body = GetComponent<Collider2D>();
        RemainingStones = startingStones;
        hasThrowAnimation = animator != null && animator.HasState(0,
            Animator.StringToHash("Base Layer.Player_Throw_Down"));
    }

    private void Update()
    {
        if (hasThrowAnimation && animator.GetBool(Throwing) &&
            (Time.time >= throwAnimationUntil || !CanProjectilesContinue ||
             (movement != null && !movement.enabled)))
            animator.SetBool(Throwing, false);
        if (GameControls.ThrowPressed) TryThrow();
    }

    public Vector2 FacingDirection
    {
        get
        {
            int facing = animator != null ? animator.GetInteger("Direction") : 0;
            switch (facing)
            {
                case 1: return Vector2.up;
                case 2: return Vector2.right;
                case 3: return Vector2.left;
                default: return Vector2.down;
            }
        }
    }

    public bool TryThrow()
    {
        if (!CanProjectilesContinue || Time.timeScale <= 0f ||
            RemainingStones <= 0 || Time.time < nextThrowAt ||
            projectilePrefab == null || (opening != null && opening.isActiveAndEnabled) ||
            (search != null && search.IsSearching) ||
            (movement != null && !movement.enabled))
            return false;

        Vector2 origin = body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;
        StoneProjectile stone = Instantiate(projectilePrefab, new Vector3(origin.x, origin.y, transform.position.z), Quaternion.identity);
        stone.Launch(this, FacingDirection);
        if (throwAudio != null && throwSound != null)
            throwAudio.PlayOneShot(throwSound);
        if (hasThrowAnimation)
        {
            int facing = animator.GetInteger("Direction");
            string suffix = facing == 1 ? "Up" : facing == 2 ? "Right" :
                facing == 3 ? "Left" : "Down";
            animator.SetBool(Throwing, true);
            animator.Play("Player_Throw_" + suffix, 0, 0f);
            throwAnimationUntil = Time.time + 0.28f;
        }
        RemainingStones--;
        nextThrowAt = Time.time + Mathf.Max(0.1f, throwCooldown);
        return true;
    }
    public void PlayImpact(Vector2 position)
    {
        if (!CanProjectilesContinue || Time.timeScale <= 0f ||
            impactAudio == null || impactSound == null) return;
        impactAudio.transform.position = new Vector3(position.x, position.y, transform.position.z);
        impactAudio.PlayOneShot(impactSound);
    }

    private void OnDisable()
    {
        if (hasThrowAnimation && animator != null) animator.SetBool(Throwing, false);
        if (throwAudio != null) throwAudio.Stop();
        if (impactAudio != null) impactAudio.Stop();
    }
}
