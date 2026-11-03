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

        if (Level2IntroSequence.Instance == null && FindObjectOfType<Level2IntroSequence>() == null)
        {
            Invoke(nameof(ShowInitialClue), 1.6f);
        }
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
        if (Level2HUD.IntroSequencePlaying) return;
        if (ParchmentMessageUI.Instance != null && ParchmentMessageUI.Instance.ChangedThisFrame) return;

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
        if (ParchmentMessageUI.Instance == null) return;

        if (count == 1)
        {
            ParchmentMessageUI.Instance.Show(
                "PISTA 2 DE 3",
                "En la explanada noreste del panteón, entre las lápidas oscuras, aguarda el segundo juguete. Busca en su altar.",
                null,
                null,
                true
            );
        }
        else if (count == 2)
        {
            ParchmentMessageUI.Instance.Show(
                "PISTA 3 DE 3",
                "En la gran explanada sur descansa el último juguete. Prepárate para la horda y el Niño Espectral.",
                null,
                null,
                true
            );
        }
        else if (count >= 3)
        {
            var weapons = collector != null ? collector.GetComponent<PlayerWeaponController>() : null;
            if (weapons != null && !weapons.hasMachete)
            {
                ParchmentMessageUI.Instance.Show(
                    "FALTA EL MACHETE",
                    "Has recuperado el último juguete. Recoge también el Machete junto al altar para abrir la puerta derecha de salida.",
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
                "PISTA 1 DE 3",
                "En la explanada noroeste, entre los árboles y las tumbas antiguas, descansa el primer juguete. Busca en su altar.",
                null,
                null,
                true
            );
        }
        else if (count == 1)
        {
            ParchmentMessageUI.Instance.Show(
                "PISTA 2 DE 3",
                "En la explanada noreste del panteón, entre las lápidas oscuras, aguarda el segundo juguete. Busca en su altar.",
                null,
                null,
                true
            );
        }
        else if (count == 2)
        {
            ParchmentMessageUI.Instance.Show(
                "PISTA 3 DE 3",
                "En la gran explanada sur descansa el último juguete. Prepárate para la horda y el Niño Espectral.",
                null,
                null,
                true
            );
        }
        else
        {
            ParchmentMessageUI.Instance.Show(
                "SALIDA DEL PANTEÓN",
                "Recoge los 3 juguetes y el Machete en el último altar para que la puerta derecha del panteón se abra.",
                null,
                null,
                true
            );
        }
    }
}
