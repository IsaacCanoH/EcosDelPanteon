using UnityEngine;

namespace Cainos.PixelArtTopDown_Basic
{
    public class TopDownCharacterController : MonoBehaviour
    {
        public float speed = 3.5f;

        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private Rigidbody2D body;

        private void Awake()
        {
            if (speed <= 0.1f) speed = 3.5f;
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (animator == null) animator = GetComponent<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            // Quitar WorldDepth2D y asegurar SortingGroup nativo en Layer 1, orden 2
            var depth = GetComponent<WorldDepth2D>();
            if (depth != null) Destroy(depth);

            var group = GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (group == null) group = gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            group.sortingLayerName = "Layer 1";
            group.sortingOrder = 2;

            if (spriteRenderer != null)
            {
                spriteRenderer.sortingLayerName = "Layer 1";
                spriteRenderer.sortingOrder = 2;
                spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            }
        }

        private void Start()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (speed <= 0.1f) speed = 3.5f;

            // Asegurar que el jugador esté en Layer 1, orden 2 con SortingGroup
            var depth = GetComponent<WorldDepth2D>();
            if (depth != null) Destroy(depth);

            var group = GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (group == null) group = gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            group.sortingLayerName = "Layer 1";
            group.sortingOrder = 2;

            if (spriteRenderer != null)
            {
                spriteRenderer.sortingLayerName = "Layer 1";
                spriteRenderer.sortingOrder = 2;
                spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0f)
            {
                if (body != null) body.velocity = Vector2.zero;
                if (animator != null && animator.enabled) animator.SetBool("IsMoving", false);
                return;
            }

            Vector2 dir = Vector2.ClampMagnitude(GameControls.Move, 1f);

            if (animator != null && animator.enabled)
            {
                if (AndroidTouchInput.IsAndroid)
                {
                    int facing = AndroidTouchInput.ResolveFacingDirection(dir, animator.GetInteger("Direction"));
                    animator.SetInteger("Direction", facing);
                    if (spriteRenderer != null && (facing == 2 || facing == 3))
                        spriteRenderer.flipX = facing == 3;
                }
                else
                {
                    if (dir.x < -0.05f)
                    {
                        animator.SetInteger("Direction", 3);
                        if (spriteRenderer != null) spriteRenderer.flipX = true;
                    }
                    else if (dir.x > 0.05f)
                    {
                        animator.SetInteger("Direction", 2);
                        if (spriteRenderer != null) spriteRenderer.flipX = false;
                    }

                    if (dir.y > 0.05f)
                        animator.SetInteger("Direction", 1);
                    else if (dir.y < -0.05f)
                        animator.SetInteger("Direction", 0);
                }

                animator.SetBool("IsMoving", dir.sqrMagnitude > 0f);
            }

            if (body == null) body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                float curSpeed = speed > 0.1f ? speed : 3.5f;
                body.velocity = curSpeed * dir;
            }
        }

        private void OnDisable()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (body != null) body.velocity = Vector2.zero;
            if (animator != null && animator.enabled) animator.SetBool("IsMoving", false);
        }
    }
}
