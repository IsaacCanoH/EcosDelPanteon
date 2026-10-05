using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ControladorPlayer : MonoBehaviour
{
    private ControlPlayer controlPlayer;
    private Vector2 movimiento;
    private Rigidbody2D rb;
    private Animator animator;

    void Awake()
    {
        controlPlayer = new ControlPlayer();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        movimiento = controlPlayer.Player.Move.ReadValue<Vector2>();
        this.CambioEstado();
        rb.velocity = new Vector2(
            movimiento.x * 5f,
            rb.velocity.y
        );
    }

    private void CambioEstado()
    {
        if (movimiento.x > 0)
        {
            animator.SetInteger("PlayerEstado", 1);
        }
        else if (movimiento.x < 0)
        {
            animator.SetInteger("PlayerEstado", 2);
        }
        else if (movimiento.x == 0 && movimiento.y == 0)
        {
            animator.SetInteger("PlayerEstado", 5);
        }
    }

    private void OnEnable()
    {
        controlPlayer.Enable();
        controlPlayer.Player.Saltar.performed += OnSaltar;
    }

    private void OnDisable()
    {
        controlPlayer.Disable();
        controlPlayer.Player.Saltar.performed -= OnSaltar;
    }

    private void OnSaltar(InputAction.CallbackContext context)
    {
        if (ColisionadorPlayer.enPiso)
        {
            rb.velocity = new Vector2(rb.velocity.x, 10f);
        }
    }
}