using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class Level4LloronaHazards : MonoBehaviour
{
    private const int DropCount = 84;
    private const float GraveRadius = 1.25f;
    private const float RainDuration = 1.25f;
    private static readonly Color WarningColor = new Color(0.9f, 0.47f, 1f, 0.87f);
    private static Sprite dropSprite;

    private readonly List<Transform> graves = new List<Transform>();
    private readonly List<SpriteRenderer> graveCues = new List<SpriteRenderer>();
    private readonly SpriteRenderer[] drops = new SpriteRenderer[DropCount];
    private readonly Vector3[] trajectories = new Vector3[DropCount];
    private PlayerHealth player;
    private Collider2D playerFeet;
    private GoblinNavigation2D navigation;
    private Camera view;
    private Transform visuals;
    private AudioSource graveAudio;
    private bool rainWarning;
    private bool gravesSuspended;
    private Coroutine rain;

    internal void Begin(PlayerHealth target, GoblinNavigation2D navigator, AudioClip graveSound)
    {
        player = target;
        playerFeet = target.GetComponent<Collider2D>();
        navigation = navigator;
        view = Camera.main;
        visuals = new GameObject("Efectos_Llorona").transform;
        if (graveSound != null)
        {
            graveAudio = visuals.gameObject.AddComponent<AudioSource>();
            graveAudio.playOnAwake = false;
            graveAudio.spatialBlend = 0f;
            graveAudio.clip = graveSound;
        }
        var tombs = GameObject.Find("SCENE/LAYER 1/Props/Tombs");
        if (tombs == null) return;
        foreach (Transform tomb in tombs.transform)
            graves.Add(tomb);
    }

    internal void WarnRain()
    {
        rainWarning = true;
        if (graveAudio != null) graveAudio.Stop();
    }
    internal void CancelRainWarning() => rainWarning = false;
    internal void SuspendGraves(bool suspended)
    {
        gravesSuspended = suspended;
        if (suspended && graveAudio != null) graveAudio.Stop();
    }

    internal void ReleaseRain()
    {
        rainWarning = false;
        player.TakeDarkRainDamage();
        if (view != null && rain == null) rain = StartCoroutine(RainRoutine());
    }

    internal void BeginGraves() => StartCoroutine(GraveRoutine());

    internal void End()
    {
        StopAllCoroutines();
        if (graveAudio != null) graveAudio.Stop();
        if (visuals != null) Destroy(visuals.gameObject);
        visuals = null;
        rain = null;
    }

    private IEnumerator RainRoutine()
    {
        if (drops[0] == null)
        {
            for (int i = 0; i < drops.Length; i++)
                drops[i] = Level4SpiritEffects.CreateVisual("Gota_Oscura", GetDropSprite(), 48, visuals);
        }

        for (int i = 0; i < trajectories.Length; i++)
            trajectories[i] = new Vector3(Random.value * 1.1f - 0.05f, Random.value, Random.Range(0.9f, 1.7f));

        for (float elapsed = 0f; elapsed < RainDuration && !player.IsDead; elapsed += Time.deltaTime)
        {
            float strength = Mathf.Min(1f, elapsed * 7f, (RainDuration - elapsed) * 5f);
            for (int i = 0; i < drops.Length; i++)
            {
                Vector3 t = trajectories[i];
                float y = Mathf.Repeat(t.y - elapsed * t.z, 1.08f) - 0.04f;
                float x = Mathf.Repeat(t.x + y * 0.10f, 1.10f) - 0.05f;
                Vector3 point = view.ViewportToWorldPoint(new Vector3(x, y, -view.transform.position.z));
                point.z = 0f;
                var drop = drops[i];
                drop.enabled = true;
                drop.transform.position = point;
                drop.transform.localScale = Vector3.one * (0.65f + t.z * 0.45f);
                drop.color = new Color(1f, 1f, 1f, strength * (0.50f + t.y * 0.42f));
            }
            yield return null;
        }

        foreach (var drop in drops) if (drop != null) drop.enabled = false;
        rain = null;
    }

    private IEnumerator GraveRoutine()
    {
        yield return new WaitForSeconds(3.8f);
        while (!player.IsDead)
        {
            if (!gravesSuspended && !rainWarning && rain == null && TryChooseGrave(out Vector2 first))
            {
                var secondAvailable = TryChooseGrave(out Vector2 second, first);
                AddGraveCue(first);
                if (secondAvailable) AddGraveCue(second);
                if (graveAudio != null)
                {
                    graveAudio.panStereo = 0.38f * Mathf.Clamp((first.x - playerFeet.bounds.center.x) / 3f, -1f, 1f);
                    graveAudio.volume = 0.46f * AudioManager.EnemyVolume;
                    graveAudio.Play();
                }
                yield return new WaitForSeconds(0.92f);
                if (!player.IsDead && !gravesSuspended && !rainWarning && rain == null)
                {
                    Vector2 current = playerFeet.bounds.center;
                    if (Vector2.Distance(current, first) <= GraveRadius ||
                        (secondAvailable && Vector2.Distance(current, second) <= GraveRadius))
                        player.TakeDamage(player.HealthPerHeart);
                    foreach (var marker in graveCues) marker.color = new Color(0.86f, 0.21f, 0.48f, 0.95f);
                }
                yield return new WaitForSeconds(0.38f);
                foreach (var marker in graveCues) if (marker != null) Destroy(marker.gameObject);
                graveCues.Clear();
            }
            yield return new WaitForSeconds(Random.Range(5.7f, 7.2f));
        }
    }

    private bool TryChooseGrave(out Vector2 result, Vector2? first = null)
    {
        Vector2 from = playerFeet.bounds.center;
        float best = float.PositiveInfinity;
        result = Vector2.zero;
        foreach (var grave in graves)
        {
            if (grave == null || !grave.gameObject.activeInHierarchy) continue;
            Vector2 point = (Vector2)grave.position + Vector2.down * 0.22f;
            float distance = Vector2.Distance(from, point);
            if (distance < 1.35f || distance > 5f ||
                (first.HasValue && Vector2.Distance(first.Value, point) < 2.9f)) continue;
            float score = Mathf.Abs(distance - 2.1f) + Random.Range(0f, 0.65f);
            if (score >= best || !HasSafeExit(from, point, first)) continue;
            best = score;
            result = point;
        }
        return !float.IsPositiveInfinity(best);
    }

    private bool HasSafeExit(Vector2 from, Vector2 grave, Vector2? other)
    {
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4f;
            Vector2 exit = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2.1f;
            if (Vector2.Distance(exit, grave) > GraveRadius + 0.35f &&
                (!other.HasValue || Vector2.Distance(exit, other.Value) > GraveRadius + 0.35f) &&
                navigation.Clear(from, exit))
                return true;
        }
        return false;
    }

    private void AddGraveCue(Vector2 position)
    {
        var marker = Level4SpiritEffects.CreateVisual("Tumba_Embrujada", Level4SpiritEffects.Ring, 4, visuals);
        marker.transform.position = position;
        marker.transform.localScale = Vector3.one * (GraveRadius * 26f / 22.5f);
        marker.color = WarningColor;
        graveCues.Add(marker);
    }

    internal static Sprite GetDropSprite()
    {
        if (dropSprite != null) return dropSprite;
        var tex = new Texture2D(4, 11, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var colors = new Color[44];
        for (int y = 0; y < 11; y++)
        {
            int x = y < 4 ? 2 : y < 8 ? 1 : 0;
            colors[y * 4 + x] = new Color(0.085f, 0.04f, 0.14f, 0.96f);
            if (y < 8) colors[y * 4 + x + 1] = new Color(0.39f, 0.24f, 0.51f, 0.88f);
        }
        tex.SetPixels(colors);
        tex.Apply();
        dropSprite = Sprite.Create(tex, new Rect(0, 0, 4, 11), Vector2.one * 0.5f, 34f);
        return dropSprite;
    }

    private void OnDisable() => End();
}
