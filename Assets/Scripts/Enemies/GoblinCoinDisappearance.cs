using System.Collections;
using UnityEngine;

public class GoblinCoinDisappearance : MonoBehaviour
{
    private GoldCoinCollector collector;
    private GoblinAI goblinAI;
    private Animator animator;
    private Rigidbody2D rb;

    private bool isDisappearing = false;

    private void Awake()
    {
        goblinAI = GetComponent<GoblinAI>();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning("No se encontró al Player.");
            return;
        }

        collector = player.GetComponent<GoldCoinCollector>();

        if (collector == null)
        {
            Debug.LogWarning("El Player no tiene GoldCoinCollector.");
            return;
        }

        collector.OnAllCoinsCollected += Disappear;

        if (collector.collectedCoins >= collector.requiredCoins)
        {
            Disappear();
        }
    }

    private void OnDestroy()
    {
        if (collector != null)
        {
            collector.OnAllCoinsCollected -= Disappear;
        }
    }

    private void Disappear()
    {
        if (isDisappearing)
            return;

        isDisappearing = true;

        // Detener persecución y ataques.
        if (goblinAI != null)
        {
            goblinAI.StopAllCoroutines();
            goblinAI.enabled = false;
        }

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.simulated = false;
        }

        // Reproducir la animación de desaparición.
        animator.SetBool("IsMoving", false);
        animator.SetInteger("ActionState", 3);
        animator.Play("Goblin_Death", 0, 0f);

        StartCoroutine(FinishDisappearance());
    }

    private IEnumerator FinishDisappearance()
    {
        yield return null;

        float duration =
            animator.GetCurrentAnimatorStateInfo(0).length;

        yield return new WaitForSeconds(duration);

        Destroy(gameObject);
    }
}