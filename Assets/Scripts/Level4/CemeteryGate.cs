using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CemeteryGate : MonoBehaviour
{
    [SerializeField] private Vector2 outward = Vector2.right;
    [SerializeField] private float insideDistance = 1.5f;
    [SerializeField] private float outsideDistance = 1.35f;
    [SerializeField] private BoxCollider2D blocker;
    [SerializeField] private SpriteRenderer firstLeaf;
    [SerializeField] private SpriteRenderer secondLeaf;
    [SerializeField] private SpriteRenderer editPreview;
    [SerializeField] private Sprite lateralNorthSource;
    [SerializeField] private Sprite lateralSouthSource;

    private static Sprite[] lateralFirst, lateralSecond, horizontalFirst, horizontalSecond;
    private Sprite[] firstFrames, secondFrames;

    public Vector2 Inside => (Vector2)transform.position - outward * insideDistance;
    public Vector2 Outside => (Vector2)transform.position + outward * outsideDistance;
    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (outward.x != 0f)
        {
            if (lateralFirst == null || lateralFirst[0] == null)
                Level2VictoryManager.GenerateEastGateAnimationFrames(
                    lateralNorthSource, lateralSouthSource, out lateralFirst, out lateralSecond);
            firstFrames = lateralFirst;
            secondFrames = lateralSecond;
        }
        else
        {
            if (horizontalFirst == null || horizontalFirst[0] == null)
                Level2VictoryManager.GenerateNorthGateAnimationFrames(out horizontalFirst, out horizontalSecond);
            firstFrames = horizontalFirst;
            secondFrames = horizontalSecond;
        }

        if (editPreview != null) editPreview.enabled = false;
        firstLeaf.flipX = secondLeaf.flipX = false;
        firstLeaf.enabled = secondLeaf.enabled = true;
        SetFrame(0f);
        blocker.enabled = true;
    }

    internal IEnumerator Animate(bool open)
    {
        if (IsOpen == open) yield break;
        if (!open) blocker.enabled = true;
        const float duration = 0.85f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, t / duration);
            SetFrame(open ? progress : 1f - progress);
            yield return null;
        }

        SetFrame(open ? 1f : 0f);
        IsOpen = open;
        blocker.enabled = !open;
        Physics2D.SyncTransforms();
    }

    private void SetFrame(float progress)
    {
        int index = Mathf.Clamp(Mathf.RoundToInt(progress * (firstFrames.Length - 1)), 0, firstFrames.Length - 1);
        firstLeaf.sprite = firstFrames[index];
        secondLeaf.sprite = secondFrames[index];
    }
}
