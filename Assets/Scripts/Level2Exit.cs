using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public sealed class Level2Exit : MonoBehaviour
{
    public TMP_Text hint;
    private ToyCollector nearbyPlayer;
    private void OnTriggerEnter2D(Collider2D other)
    {
        var toys = other.GetComponentInParent<ToyCollector>();
        if (toys != null)
        {
            nearbyPlayer = toys;
            if (nearbyPlayer.Count >= ToyCollector.RequiredToys)
            {
                TriggerExit();
            }
        }
    }

    private void TriggerExit()
    {
        if (Level2VictoryManager.Instance != null)
        {
            Level2VictoryManager.Instance.TriggerVictorySequence();
        }
        else
        {
            var vm = gameObject.AddComponent<Level2VictoryManager>();
            vm.TriggerVictorySequence();
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<ToyCollector>() == nearbyPlayer) { nearbyPlayer = null; if (hint != null) hint.text = ""; }
    }
    private void Update()
    {
        if (nearbyPlayer == null || hint == null) return;
        var health = nearbyPlayer.GetComponent<PlayerHealth>();
        if (health != null && health.currentHealth <= 0) { hint.text = ""; return; }
        bool complete = nearbyPlayer.Count == ToyCollector.RequiredToys;
        hint.text = complete ? "Juguetes recuperados · E: salir del panteón" : "La salida espera: recupera los tres juguetes.";
        if (complete && Input.GetKeyDown(KeyCode.E))
        {
            TriggerExit();
        }
    }
}
