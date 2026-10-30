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
            }
        }

        private void Start()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (speed <= 0.1f) speed = 3.5f;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f)
            {
                if (body != null) body.velocity = Vector2.zero;
                if (animator != null && animator.enabled) animator.SetBool("IsMoving", false);
                return;
            }

            Vector2 dir = Vector2.zero;

            // 1. Teclas directas WASD y Flechas (garantiza respuesta instantánea con teclado)
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) dir.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dir.x += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) dir.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) dir.y -= 1f;

            // 2. Soporte GameControls (Input System / Mandos)
            if (dir == Vector2.zero)
            {
                Vector2 gc = GameControls.Move;
                if (gc.sqrMagnitude > 0.001f)
                    dir = gc;
            }

            // 3. Fallback a ejes virtuales de Unity
            if (dir == Vector2.zero)
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                if (Mathf.Abs(h) > 0.05f || Mathf.Abs(v) > 0.05f)
                    dir = new Vector2(h, v);
            }

            dir = Vector2.ClampMagnitude(dir, 1f);

            if (animator != null && animator.enabled)
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
