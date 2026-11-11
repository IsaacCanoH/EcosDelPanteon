using UnityEngine;

public sealed class DollObjectiveUI : MonoBehaviour
{
    [SerializeField] private ParchmentMessageUI parchment;
    [SerializeField] private Sprite dollIcon;
    [SerializeField] private TMPro.TMP_Text accessLabel;
    [SerializeField] private bool showAtStart = true;

    private void Start()
    {
        if (accessLabel != null)
            accessLabel.text = "VER OBJETIVO";

        if (showAtStart) Invoke(nameof(ShowObjective), 1.6f);
    }

    private void Update()
    {
        if (GameControls.CluePressed || Input.GetKeyDown(KeyCode.P))
            ShowObjective();
    }

    private void ShowObjective()
    {
        if (parchment == null || parchment.IsOpen || parchment.ChangedThisFrame || Time.timeScale <= 0f)
            return;

        parchment.Show("OBJETIVO", "Busca los tres muñecos de trapo en el panteón.", dollIcon);
    }
}
