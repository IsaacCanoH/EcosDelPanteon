using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovePlayer : MonoBehaviour
{
    Rigidbody2D rb;
    float moveSpeed = 5f;
    float moveX, moveY;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        moveY = Input.GetAxisRaw("Vertical");
        moveX = Input.GetAxisRaw("Horizontal");

        rb.velocity = new Vector2(moveX * moveSpeed, moveY * moveSpeed);
    }
}