using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cainos.PixelArtTopDown_Basic
{
    public class Level2CharacterController : MonoBehaviour
    {
        [Min(0.5f)] public float speed = 3.5f;

        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private Rigidbody2D body;
        private PlayerHealth health;
        private PlayerWeaponController weaponController;
        private Vector2 movement;

        private void Awake()
        {
            enabled = true;
            if (speed <= 0.1f) speed = 3.5f;

            weaponController = GetComponent<PlayerWeaponController>();
            if (weaponController == null)
            {
                weaponController = gameObject.AddComponent<PlayerWeaponController>();
            }
        }

        private void Start()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<PlayerHealth>();
            if (speed <= 0.1f) speed = 3.5f;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || (health != null && health.currentHealth <= 0))
            {
                movement = Vector2.zero;
                if (animator != null) animator.SetBool("IsMoving", false);
                return;
            }

            Vector2 dir = Vector2.zero;

            // Soporte de Teclas A/D y Flechas
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                dir.x = -1;
                if (animator != null) animator.SetInteger("Direction", 3);
                if (spriteRenderer != null) spriteRenderer.flipX = true;
            }
            else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                dir.x = 1;
                if (animator != null) animator.SetInteger("Direction", 2);
                if (spriteRenderer != null) spriteRenderer.flipX = false;
            }

            // Soporte de Teclas W/S y Flechas
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                dir.y = 1;
                if (animator != null) animator.SetInteger("Direction", 1);
            }
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                dir.y = -1;
                if (animator != null) animator.SetInteger("Direction", 0);
            }

            // Soporte GameControls (mandos / input system)
            if (dir == Vector2.zero && GameControls.Move != Vector2.zero)
            {
                dir = GameControls.Move;
                if (dir.x < -0.1f)
                {
                    if (animator != null) animator.SetInteger("Direction", 3);
                    if (spriteRenderer != null) spriteRenderer.flipX = true;
                }
                else if (dir.x > 0.1f)
                {
                    if (animator != null) animator.SetInteger("Direction", 2);
                    if (spriteRenderer != null) spriteRenderer.flipX = false;
                }
                else if (dir.y > 0.1f)
                {
                    if (animator != null) animator.SetInteger("Direction", 1);
                }
                else if (dir.y < -0.1f)
                {
                    if (animator != null) animator.SetInteger("Direction", 0);
                }
            }

            dir = Vector2.ClampMagnitude(dir, 1f);
            if (animator != null) animator.SetBool("IsMoving", dir.sqrMagnitude > 0.01f);

            movement = dir;
        }

        private void FixedUpdate()
        {
            if (body != null)
            {
                body.velocity = (speed > 0.1f ? speed : 3.5f) * movement;
            }
        }
    }
}
