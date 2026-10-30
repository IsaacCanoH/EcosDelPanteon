using UnityEngine;

public class Level2ClueSystem : MonoBehaviour
{
    public static Level2ClueSystem Instance { get; private set; }

    private ToyCollector collector;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            collector = player.GetComponent<ToyCollector>();
            if (collector != null)
            {
                collector.Changed += HandleToyCollected;
            }
        }

        // Mostrar pista del primer juguete tras una breve espera al inicio
        Invoke(nameof(ShowInitialClue), 1.6f);
    }

    private void OnDestroy()
    {
        if (collector != null)
        {
            collector.Changed -= HandleToyCollected;
        }
    }

    private void Update()
    {
        // Al presionar P en cualquier momento, alternar la pista activa pausando o reanudando el juego
        if (Input.GetKeyDown(KeyCode.P) || GameControls.CluePressed)
        {
            if (ParchmentMessageUI.Instance != null && ParchmentMessageUI.Instance.IsOpen)
            {
                ParchmentMessageUI.Instance.Close();
            }
            else
            {
                ShowActiveClue();
            }
        }
    }

    private void ShowInitialClue()
    {
        if (collector == null || collector.Count == 0)
        {
            ShowActiveClue();
        }
    }

    private void HandleToyCollected(int count)
    {
        if (count == 1)
        {
            if (ParchmentMessageUI.Instance != null)
            {
                ParchmentMessageUI.Instance.Show(
                    "PISTA: SEGUNDO RECUERDO",
                    "El segundo juguete yace en la explanada noreste del cementerio, custodiado por lápidas oscuras.\n\nAcércate a la zona y mantén [E] para purificarla.\n[P / ESPACIO] Continuar",
                    null,
                    null,
                    true
                );
            }
        }
        else if (count == 2)
        {
            if (ParchmentMessageUI.Instance != null)
            {
                ParchmentMessageUI.Instance.Show(
                    "PISTA: ÚLTIMO RECUERDO",
                    "El último juguete descansa en la gran explanada sur.\n\nPrepárate para una horda de 10 enemigos y el despertar del Niño Espectral...\n[P / ESPACIO] Continuar",
                    null,
                    null,
                    true
                );
            }
        }
        else if (count >= 3)
        {
            if (ParchmentMessageUI.Instance != null)
            {
                ParchmentMessageUI.Instance.Show(
                    "¡PUERTA DESBLOQUEADA!",
                    "Has purificado los 3 recuerdos perdidos.\n\nDirígete a la reja lateral del Acceso Oeste para escapar del panteón.\n[P / ESPACIO] Continuar",
                    null,
                    null,
                    true
                );
            }
        }
    }

    public void ShowActiveClue()
    {
        if (ParchmentMessageUI.Instance == null) return;

        int count = collector != null ? collector.Count : ToyEncounterZone.GlobalEncountersCompleted;

        if (count == 0)
        {
            ParchmentMessageUI.Instance.Show(
                "PISTA: PRIMER RECUERDO",
                "Explora la explanada noroeste del panteón, entre los sauces y las tumbas antiguas.\n\nAcércate y mantén [E] para buscar el juguete.\n(Pulsa [P] para ver u ocultar esta pista con pausa).",
                null,
                null,
                true
            );
        }
        else if (count == 1)
        {
            ParchmentMessageUI.Instance.Show(
                "PISTA: SEGUNDO RECUERDO",
                "El segundo juguete se encuentra en la explanada noreste del cementerio.\n\nAcércate a la zona y mantén [E] para purificarla.\n(Pulsa [P] para ver u ocultar esta pista con pausa).",
                null,
                null,
                true
            );
        }
        else if (count == 2)
        {
            ParchmentMessageUI.Instance.Show(
                "PISTA: ÚLTIMO RECUERDO",
                "El tercer juguete te espera en la explanada sur del panteón.\n\nPrepárate para una gran horda espectral y el enfrentamiento final.\n(Pulsa [P] para ver u ocultar esta pista con pausa).",
                null,
                null,
                true
            );
        }
        else
        {
            ParchmentMessageUI.Instance.Show(
                "¡PUERTA DESBLOQUEADA!",
                "Los 3 recuerdos fueron liberados.\n\nVe a la puerta del Acceso Oeste para completar el nivel.\n(Pulsa [P] para ver u ocultar con pausa).",
                null,
                null,
                true
            );
        }
    }
}

