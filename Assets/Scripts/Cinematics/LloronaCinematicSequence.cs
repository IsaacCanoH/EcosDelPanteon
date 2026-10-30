using System;
using System.Collections;
using UnityEngine;

public class LloronaCinematicSequence : MonoBehaviour
{
    [Header("Visuales de La Llorona")]
    public Sprite lloronaSprite;
    public float floatAmplitude = 0.14f;
    public float floatFrequency = 2.4f;

    [Header("Audio (Opcional)")]
    public AudioClip lloronaWailSound;

    public void PlayEncounter1Cinematic(Vector3 spawnPosition, Action onComplete)
    {
        StartCoroutine(SequenceRoutine(spawnPosition, onComplete));
    }

    private IEnumerator SequenceRoutine(Vector3 spawnPosition, Action onComplete)
    {
        // 1. Cargar sprite si no se asignó en el Inspector
        if (lloronaSprite == null)
        {
            lloronaSprite = Resources.Load<Sprite>("TX_LaLlorona");
            if (lloronaSprite == null)
            {
#if UNITY_EDITOR
                lloronaSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Cinematics/TX_LaLlorona.png");
#endif
            }
        }

        // 2. Instanciar la aparición fantasmal de La Llorona
        GameObject phantom = new GameObject("Phantom_LaLlorona");
        phantom.transform.position = spawnPosition + Vector3.up * 0.8f;
        SpriteRenderer sr = phantom.AddComponent<SpriteRenderer>();
        sr.sprite = lloronaSprite;
        sr.sortingLayerName = "Layer 2";
        sr.sortingOrder = 750;
        sr.color = new Color(1f, 1f, 1f, 0f);

        if (lloronaWailSound != null)
        {
            AudioSource.PlayClipAtPoint(lloronaWailSound, phantom.transform.position, 0.9f);
        }

        // Bloquear controles del jugador durante la aparición
        var playerMovement1 = FindObjectOfType<Cainos.PixelArtTopDown_Basic.TopDownCharacterController>();
        var playerMovement2 = FindObjectOfType<Cainos.PixelArtTopDown_Basic.Level2CharacterController>();
        var playerWeapons = FindObjectOfType<PlayerWeaponController>();
        if (playerMovement1 != null) playerMovement1.enabled = false;
        if (playerMovement2 != null) playerMovement2.enabled = false;
        if (playerWeapons != null) playerWeapons.enabled = false;

        // 3. Fade-in espectral
        float t = 0f;
        while (t < 0.7f)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / 0.7f);
            sr.color = new Color(1f, 1f, 1f, alpha);
            phantom.transform.position = spawnPosition + Vector3.up * (0.8f + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude);
            yield return null;
        }

        // 4. Mostrar Diálogo de La Llorona en pergamino con pausa completa
        bool dialogueDone = false;
        if (ParchmentMessageUI.Instance != null)
        {
            ParchmentMessageUI.Instance.Show(
                "LA LLORONA",
                "«¿Quién perturba el sueño de mis hijos?...\n¡Mis sirvientes no tendrán piedad!»\n\n[P / ESPACIO] Continuar",
                null,
                () => { dialogueDone = true; },
                true
            );

            while (!dialogueDone)
            {
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(2.0f);
        }

        // 5. Desvanecimiento espectral rápido
        t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (t / 0.5f));
            if (sr != null) sr.color = new Color(0.6f, 0.9f, 1f, alpha);
            if (phantom != null) phantom.transform.localScale = Vector3.one * (1f + t * 0.4f);
            yield return null;
        }

        if (phantom != null) Destroy(phantom);

        // Restaurar controles del jugador
        if (playerMovement1 != null) playerMovement1.enabled = true;
        if (playerMovement2 != null) playerMovement2.enabled = true;
        if (playerWeapons != null) playerWeapons.enabled = true;

        yield return new WaitForSeconds(0.2f);
        onComplete?.Invoke();
    }
}
