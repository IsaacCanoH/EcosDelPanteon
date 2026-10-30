using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(GoblinAI))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
public class GoblinAudio : MonoBehaviour
{
    [Header("Fuentes")]
    public AudioSource voz;
    public AudioSource pasos;

    public AudioSource ataques;

    [Header("Sonidos de ataque")]
    public AudioClip[] sonidosAtaque = new AudioClip[2];

    private float ultimoAtaqueAudio = float.NegativeInfinity;
    private int ultimoSonidoAtaque = -1;

    [Header("Archivos originales")]
    public AudioClip avistamiento;
    public AudioClip persecucion;
    public AudioClip grabacionPasos;

    [Header("Fragmentos de pasos, en segundos")]
    public float[] inicioPasos = { 1.575f, 1.880f, 2.140f };
    public float duracionPaso = 0.18f;

    private GoblinAI ai;
    private Animator animator;
    private Rigidbody2D rb;
    private Transform player;
    private PlayerHealth health;
    private GoldCoinCollector collector;

    private AudioClip[] golpes;
    private Vector2 ultimaPosicion;
    private float ultimoMovimiento = float.NegativeInfinity;
    private float ultimoPaso = float.NegativeInfinity;
    private float siguienteVoz;
    private float volumenVoz;
    private int modoVoz = -1;
    private int ultimoGolpe = -1;
    private bool pausado;
    private bool desvaneciendo;
    private Coroutine transicionVoz;
    private static GoblinAudio vozActiva;
    private static float siguienteTurnoVoz;
    private static float siguientePasoGrupo;
    private float finTurnoVoz;
    private float siguienteVozPropia;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarTurnos()
    {
        vozActiva = null;
        siguienteTurnoVoz = 0f;
        siguientePasoGrupo = 0f;
    }

    private bool ReservarVoz(AudioSource fuente, float duracion)
    {
        if (Time.time < siguienteVozPropia || player == null || fuente == null ||
            Vector2.Distance(transform.position, player.position) >= fuente.maxDistance)
            return false;
        if (vozActiva != this &&
            ((vozActiva != null && Time.time < vozActiva.finTurnoVoz) ||
             Time.time < siguienteTurnoVoz))
            return false;
        vozActiva = this;
        finTurnoVoz = Time.time + duracion + 0.05f;
        siguienteVozPropia = Time.time + Mathf.Max(4f, duracion + 0.6f);
        siguienteTurnoVoz = finTurnoVoz + 0.25f;
        return true;
    }

    private void LiberarVoz()
    {
        if (vozActiva != this) return;
        vozActiva = null;
        siguienteTurnoVoz = Time.time + 0.25f;
        finTurnoVoz = 0f;
    }

    private void Awake()
    {
        ai = GetComponent<GoblinAI>();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        ultimaPosicion = rb.position;

        GameObject target = GameObject.FindGameObjectWithTag("Player");

        if (target != null)
        {
            player = target.transform;
            health = target.GetComponent<PlayerHealth>();
            collector = target.GetComponent<GoldCoinCollector>();
        }

        if (voz == null || pasos == null || grabacionPasos == null)
        {
            Debug.LogError(
                "Faltan referencias en GoblinAudio.", this);
            enabled = false;
            return;
        }

        volumenVoz = voz.volume;
        voz.playOnAwake = false;
        pasos.playOnAwake = false;
        voz.loop = false;
        pasos.loop = false;

        golpes = new AudioClip[inicioPasos.Length];

        for (int i = 0; i < golpes.Length; i++)
            golpes[i] = CrearPaso(inicioPasos[i], i);
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
        bool pausaActual = Time.timeScale == 0f;

        if (pausaActual != pausado)
        {
            pausado = pausaActual;

            if (pausado)
            {
                voz.Pause();
                pasos.Pause();

                if (ataques != null)
                    ataques.Pause();
            }
            else
            {
                voz.UnPause();
                pasos.UnPause();

                if (ataques != null)
                    ataques.UnPause();
            }
        }

        if (pausado)
            return;

        if (!PuedeSonar())
        {
            Silenciar();
            return;
        }

        if (vozActiva == this && Time.time >= finTurnoVoz &&
            !voz.isPlaying && (ataques == null || !ataques.isPlaying))
            LiberarVoz();

        float velocidad = ai.CurrentMoveSpeed;
        bool rapido = velocidad > ai.moveSpeed + 0.01f;

        animator.SetFloat(
            "RunSpeed",
            velocidad / Mathf.Max(0.01f, ai.moveSpeed));

        bool persigue = ai.IsAlerted;

        int nuevoModo = persigue ? 1 : 0;

        if (nuevoModo != modoVoz)
        {
            modoVoz = nuevoModo;

            if (transicionVoz != null)
                StopCoroutine(transicionVoz);

            transicionVoz = null;
            desvaneciendo = false;

            siguienteVoz = Time.time + (persigue
                ? Random.Range(0.15f, 0.6f)
                : Random.Range(2f, 5f));

            if (voz.isPlaying)
                transicionVoz = StartCoroutine(DesvanecerVoz());
            else
                voz.volume = volumenVoz;
        }

        if (desvaneciendo ||
    voz.isPlaying ||
    (ataques != null && ataques.isPlaying) ||
    Time.time < siguienteVoz)
            return;

        bool puedeIniciarVoz = persigue
            ? EstaCorriendo()
            : animator.GetInteger("ActionState") == 0;

        if (!puedeIniciarVoz)
            return;

        AudioClip clip = persigue ? persecucion : avistamiento;

        if (clip == null)
            return;

        float tono = Random.Range(0.97f, 1.03f);
        if (!ReservarVoz(voz, clip.length / tono))
        {
            siguienteVoz = Time.time + Random.Range(0.35f, 0.85f);
            return;
        }
        voz.clip = clip;
        voz.volume = volumenVoz;
        voz.pitch = tono;
        voz.Play();

        float silencio = persigue
            ? (rapido
                ? Random.Range(2f, 4f)
                : Random.Range(3f, 5f))
            : Random.Range(6f, 10f);

        siguienteVoz =
            Time.time + clip.length / voz.pitch + silencio;
    }

    public void Footstep()
    {
        if (!isActiveAndEnabled ||
            Time.timeScale == 0f ||
            !PuedeSonar() ||
            !EstaCorriendo() ||
            Time.time - ultimoPaso < 0.1f ||
            Time.time < siguientePasoGrupo ||
            golpes == null ||
            golpes.Length == 0)
            return;

        int indice = Random.Range(0, golpes.Length);

        if (golpes.Length > 1 && indice == ultimoGolpe)
            indice = (indice + 1) % golpes.Length;

        if (golpes[indice] == null)
            return;

        ultimoGolpe = indice;
        ultimoPaso = Time.time;
        siguientePasoGrupo = Time.time + 0.075f;

        pasos.pitch = Random.Range(0.97f, 1.03f);
        pasos.PlayOneShot(golpes[indice]);
    }

    public void AttackVoice()
    {
        if (!isActiveAndEnabled ||
            Time.timeScale == 0f ||
            !PuedeSonar() ||
            ataques == null ||
            sonidosAtaque == null ||
            sonidosAtaque.Length == 0 ||
            Time.time - ultimoAtaqueAudio < 0.5f)
            return;

        int estado = animator.GetInteger("ActionState");

        if (estado != 1 && estado != 2)
            return;

        int indice = Random.Range(0, sonidosAtaque.Length);

        if (sonidosAtaque.Length > 1 &&
            indice == ultimoSonidoAtaque)
        {
            indice = (indice + 1) % sonidosAtaque.Length;
        }

        AudioClip clip = sonidosAtaque[indice];

        if (clip == null)
            return;

        float tono = Random.Range(0.97f, 1.03f);
        if (!ReservarVoz(ataques, clip.length / tono)) return;
        ultimoAtaqueAudio = Time.time;
        ultimoSonidoAtaque = indice;

        ataques.Stop();
        ataques.pitch = tono;
        ataques.PlayOneShot(clip);

        siguienteVoz = Mathf.Max(
            siguienteVoz,
            Time.time + clip.length / ataques.pitch + 0.5f);

        // Desvanecer la voz anterior al iniciar el ataque.
        if (transicionVoz != null)
            StopCoroutine(transicionVoz);

        transicionVoz = null;
        desvaneciendo = false;

        if (voz.isPlaying)
            transicionVoz = StartCoroutine(DesvanecerVoz());
        else
            voz.volume = volumenVoz;
    }

    private bool EstaCorriendo()
    {
        return animator.GetBool("IsMoving") &&
               animator.GetInteger("ActionState") == 0 &&
               Time.time - ultimoMovimiento <= 0.1f;
    }

    private bool PuedeSonar()
    {
        return ai.isActiveAndEnabled && !ai.IsStunned &&
               rb.simulated &&
               player != null &&
               (health == null || health.currentHealth > 0) &&
               (collector == null ||
                !collector.IsLevelFinished);
    }

    private IEnumerator DesvanecerVoz()
    {
        desvaneciendo = true;
        float inicio = voz.volume;
        float tiempo = 0f;

        while (tiempo < 0.08f)
        {
            tiempo += Time.deltaTime;
            voz.volume = Mathf.Lerp(inicio, 0f, tiempo / 0.08f);
            yield return null;
        }

        voz.Stop();
        voz.volume = volumenVoz;
        if (ataques == null || !ataques.isPlaying) LiberarVoz();
        desvaneciendo = false;
        transicionVoz = null;
    }

    private AudioClip CrearPaso(float inicio, int indice)
    {
        int frecuencia = grabacionPasos.frequency;
        int canales = grabacionPasos.channels;
        int offset = Mathf.RoundToInt(inicio * frecuencia);
        int cantidad = Mathf.RoundToInt(duracionPaso * frecuencia);

        if (cantidad < 2 ||
            offset < 0 ||
            offset + cantidad > grabacionPasos.samples)
            return null;

        float[] original = new float[cantidad * canales];

        if (!grabacionPasos.GetData(original, offset))
        {
            Debug.LogError(
                "Revisa Decompress On Load y Preload Audio Data.",
                this);
            return null;
        }

        float[] mono = new float[cantidad];
        float pico = 0f;

        for (int i = 0; i < cantidad; i++)
        {
            for (int c = 0; c < canales; c++)
                mono[i] += original[i * canales + c] / canales;

            pico = Mathf.Max(pico, Mathf.Abs(mono[i]));
        }

        float ganancia = Mathf.Min(
            3f, 0.6f / Mathf.Max(0.001f, pico));

        int borde = Mathf.Max(
            1, Mathf.RoundToInt(frecuencia * 0.005f));

        for (int i = 0; i < cantidad; i++)
        {
            float entrada = Mathf.Clamp01((float)i / borde);
            float salida = Mathf.Clamp01(
                (float)(cantidad - 1 - i) / borde);

            mono[i] *= ganancia * Mathf.Min(entrada, salida);
        }

        AudioClip resultado = AudioClip.Create(
            "PasoDuende_" + indice,
            cantidad, 1, frecuencia, false);

        resultado.SetData(mono, 0);
        return resultado;
    }

    public void StopForStun()
    {
        Silenciar();
        modoVoz = -1;
        ultimoMovimiento = float.NegativeInfinity;
    }

    private void Silenciar()
    {
        LiberarVoz();
        if (transicionVoz != null)
            StopCoroutine(transicionVoz);

        transicionVoz = null;
        desvaneciendo = false;

        if (voz != null)
        {
            voz.Stop();
            voz.volume = volumenVoz;
        }

        if (pasos != null)
            pasos.Stop();

        if (ataques != null)
            ataques.Stop();
    }

    private void OnDisable()
    {
        Silenciar();
    }

    private void OnDestroy()
    {
        if (golpes == null)
            return;

        foreach (AudioClip golpe in golpes)
            if (golpe != null)
                Destroy(golpe);
    }
}