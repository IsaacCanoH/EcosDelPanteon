using System.Collections;
using UnityEngine;

public sealed class Level4LloronaFinalEffects : MonoBehaviour
{
    private static readonly int PulseProperty = Shader.PropertyToID("_SpectralPulse");
    private static readonly Color PulseColor = new Color(1f, 0.29f, 0.36f, 0.84f);
    private static AudioClip lament;
    private Renderer[] statues;
    private MaterialPropertyBlock[] originalBlocks;
    private AudioSource voice;

    internal void PlayStatuePulse(Vector2 origin, Renderer[] echoes)
    {
        StartCoroutine(StatuePulse(origin, echoes));
    }

    private IEnumerator StatuePulse(Vector2 origin, Renderer[] echoes)
    {
        var wave = Level4SpiritEffects.CreateVisual("Pulso_Rojo_Estatuas", Level4SpiritEffects.Ring, 3, transform);
        wave.transform.position = origin;
        const float travel = 0.58f;
        var trails = new SpriteRenderer[echoes.Length];
        for (int i = 0; i < echoes.Length; i++)
        {
            if (echoes[i] == null) continue;
            trails[i] = Level4SpiritEffects.CreateVisual("Eco_Rojo_Estatua", Level4LloronaHazards.GetDropSprite(), 8, transform);
            trails[i].transform.position = origin;
            trails[i].transform.localScale = Vector3.one * 2.3f;
            trails[i].color = PulseColor;
        }

        for (float elapsed = 0f; elapsed < travel; elapsed += Time.deltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / travel);
            wave.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 3.7f, progress);
            wave.color = new Color(PulseColor.r, PulseColor.g, PulseColor.b, 0.85f * (1f - progress));
            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] == null || echoes[i] == null) continue;
                Vector2 destination = echoes[i].bounds.center;
                trails[i].transform.position = Vector2.Lerp(origin, destination, Mathf.SmoothStep(0f, 1f, progress)) +
                    Vector2.up * (Mathf.Sin(progress * Mathf.PI) * 0.9f);
            }
            yield return null;
        }

        Destroy(wave.gameObject);
        foreach (var trail in trails) if (trail != null) Destroy(trail.gameObject);
        statues = echoes;
        originalBlocks = new MaterialPropertyBlock[statues.Length];
        for (int i = 0; i < statues.Length; i++)
        {
            if (statues[i] == null) continue;
            originalBlocks[i] = new MaterialPropertyBlock();
            statues[i].GetPropertyBlock(originalBlocks[i]);
        }

        var block = new MaterialPropertyBlock();
        const float glow = 0.78f;
        for (float elapsed = 0f; elapsed < glow; elapsed += Time.deltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / glow);
            float intensity = 0.88f * Mathf.Sin(progress * Mathf.PI);
            foreach (var statue in statues)
            {
                if (statue == null) continue;
                statue.GetPropertyBlock(block);
                block.SetFloat(PulseProperty, intensity);
                statue.SetPropertyBlock(block);
            }
            yield return null;
        }

        RestoreStatues();
    }

    internal IEnumerator Dissolve(SpriteRenderer body, Color tint)
    {
        const float duration = 0.58f;
        var droplets = new SpriteRenderer[16];
        var offsets = new Vector2[droplets.Length];
        var center = (Vector2)transform.position + Vector2.up * 0.77f;
        for (int i = 0; i < droplets.Length; i++)
        {
            offsets[i] = new Vector2(Random.Range(-0.54f, 0.54f), Random.Range(-0.48f, 0.63f));
            var drop = Level4SpiritEffects.CreateVisual("Gota_Desaparicion_Llorona", Level4LloronaHazards.GetDropSprite(), 5, transform);
            drop.transform.position = center + offsets[i];
            droplets[i] = drop;
        }

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            body.color = new Color(tint.r, tint.g, tint.b, tint.a * (1f - progress));
            for (int i = 0; i < droplets.Length; i++)
            {
                var drop = droplets[i];
                Vector2 spread = new Vector2(offsets[i].x * progress, -progress * (0.36f + i * 0.023f));
                drop.transform.position = center + offsets[i] + spread;
                drop.color = new Color(0.76f, 0.55f, 0.98f, Mathf.Sin(progress * Mathf.PI));
            }
            yield return null;
        }

        foreach (var drop in droplets) Destroy(drop.gameObject);
        body.enabled = false;
        body.color = tint;
    }

    internal IEnumerator WarnReturn(Vector2 position, SpriteRenderer body)
    {
        var silhouette = Level4SpiritEffects.CreateVisual("Silueta_Regreso_Llorona", body.sprite, body.sortingOrder, transform);
        silhouette.sortingLayerID = body.sortingLayerID;
        silhouette.transform.position = position;
        silhouette.transform.localScale = body.transform.localScale;
        silhouette.flipX = body.flipX;
        silhouette.flipY = body.flipY;
        PlayLament();

        const float duration = 0.54f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float progress = elapsed / duration;
            silhouette.color = new Color(0.24f, 0.08f, 0.34f, Mathf.Lerp(0.16f, 0.80f, progress));
            yield return null;
        }

        Destroy(silhouette.gameObject);
    }

    internal void PlayLament()
    {
        if (voice == null)
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
        }

        if (lament == null)
        {
            const int rate = 22050;
            const float length = 1.06f;
            var samples = new float[Mathf.RoundToInt(rate * length)];
            float phase = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float progress = t / length;
                float pitch = Mathf.Lerp(415f, 252f, Mathf.SmoothStep(0f, 1f, progress)) +
                    12f * Mathf.Sin(t * 29f);
                phase += 2f * Mathf.PI * pitch / rate;
                float envelope = Mathf.Sin(progress * Mathf.PI);
                float tone = Mathf.Sin(phase) + 0.47f * Mathf.Sin(phase * 2f + Mathf.Sin(t * 23f)) +
                    0.21f * Mathf.Sin(phase * 3f);
                samples[i] = 0.20f * envelope * envelope * tone;
            }

            lament = AudioClip.Create("Lamento_Espectral_Llorona", samples.Length, 1, rate, false);
            lament.SetData(samples, 0);
        }
        voice.PlayOneShot(lament, 0.64f * AudioManager.EnemyVolume);
    }

    private void RestoreStatues()
    {
        if (statues == null || originalBlocks == null) return;
        for (int i = 0; i < statues.Length; i++)
        {
            if (statues[i] != null && originalBlocks[i] != null)
                statues[i].SetPropertyBlock(originalBlocks[i]);
        }
        statues = null;
        originalBlocks = null;
    }

    private void OnDisable() => RestoreStatues();
}
