using UnityEngine;

namespace Cainos.PixelArtTopDown_Basic
{
    public class TopDownCharacterController : MonoBehaviour
    {
        public float speed;

        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private Rigidbody2D body;

        private void Start()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            Vector2 dir = Time.timeScale > 0f
                ? Vector2.ClampMagnitude(GameControls.Move, 1f) : Vector2.zero;
            if (dir.x < 0f)
            {
                animator.SetInteger("Direction", 3);
                spriteRenderer.flipX = true;
            }
            else if (dir.x > 0f)
            {
                animator.SetInteger("Direction", 2);
                spriteRenderer.flipX = false;
            }

            if (dir.y > 0f)
                animator.SetInteger("Direction", 1);
            else if (dir.y < 0f)
                animator.SetInteger("Direction", 0);

            animator.SetBool("IsMoving", dir.sqrMagnitude > 0f);
            body.velocity = speed * dir;
        }

        private void OnDisable()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (body != null) body.velocity = Vector2.zero;
            if (animator != null) animator.SetBool("IsMoving", false);
        }
    }
}
