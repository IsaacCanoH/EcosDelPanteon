using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerOcclusionZone : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private SpriteRenderer playerRenderer;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayer(other))
        {
            playerRenderer.enabled = false;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other))
        {
            playerRenderer.enabled = true;
        }
    }

    private bool IsPlayer(Collider2D other)
    {
        return other.transform == player || other.transform.IsChildOf(player);
    }

    private void OnDisable()
    {
        if (playerRenderer != null)
            playerRenderer.enabled = true;
    }
}
