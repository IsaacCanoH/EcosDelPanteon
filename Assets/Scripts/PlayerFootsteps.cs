using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerFootsteps : MonoBehaviour
{
    public AudioSource fuentePasos;

    private Rigidbody2D rb;
    private Animator animator;
    private PlayerHealth health;

    private Vector2 ultimaPosicion;
    private float ultimoMovimiento = float.NegativeInfinity;
    private float volumenBase;
    private bool iniciado;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        health = GetComponent<PlayerHealth>();
        ultimaPosicion = rb.position;

        if (fuentePasos == null || fuentePasos.clip == null)
        {
            Debug.LogError(
                "Asigna la fuente y el audio de pasos del jugador.",
                this);

            enabled = false;
            return;
        }

        volumenBase = fuentePasos.volume;
        fuentePasos.playOnAwake = false;
        fuentePasos.loop = true;
        fuentePasos.Stop();
        fuentePasos.volume = 0f;
    }

    private void FixedUpdate()
    {
        Vector2 posicion = rb.position;

        if ((posicion - ultimaPosicion).sqrMagnitude > 0.000001f)
            ultimoMovimiento = Time.time;

        ultimaPosicion = posicion;
    }

    private void Update()
    {
        if (Time.timeScale == 0f)
        {
            fuentePasos.Pause();
            return;
        }

        if (!rb.simulated ||
            (health != null && health.currentHealth <= 0))
        {
            fuentePasos.Stop();
            fuentePasos.volume = 0f;
            iniciado = false;
            return;
        }

        bool seMueve =
            animator.GetBool("IsMoving") &&
            Time.time - ultimoMovimiento <= 0.1f;

        float volumenObjetivo = seMueve ? volumenBase : 0f;

        fuentePasos.volume = Mathf.MoveTowards(
            fuentePasos.volume,
            volumenObjetivo,
            volumenBase * Time.deltaTime / 0.06f);

        if (seMueve)
        {
            if (!iniciado)
            {
                fuentePasos.Play();
                iniciado = true;
            }
            else if (!fuentePasos.isPlaying)
            {
                fuentePasos.UnPause();
            }
        }
        else if (fuentePasos.volume <= 0.0001f)
        {
            fuentePasos.Pause();
        }
    }

    private void OnDisable()
    {
        if (fuentePasos != null)
        {
            fuentePasos.Stop();
            fuentePasos.volume = 0f;
        }

        iniciado = false;
    }
}