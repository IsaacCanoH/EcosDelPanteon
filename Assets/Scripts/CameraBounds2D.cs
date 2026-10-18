using UnityEngine;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Camera))]
public class CameraBounds2D : MonoBehaviour
{
    public Vector2 minBounds = new Vector2(-17f, -13f);
    public Vector2 maxBounds = new Vector2(17f, 10f);

    [Min(0f)]
    public float edgeMargin = 0.05f;

    private Camera cameraComponent;
    private float originalSize;

    private void Start()
    {
        cameraComponent = GetComponent<Camera>();
        originalSize = cameraComponent.orthographicSize;

        ApplyBounds();
    }

    private void LateUpdate()
    {
        ApplyBounds();
    }

    private void ApplyBounds()
    {
        if (cameraComponent == null ||
            !cameraComponent.orthographic)
            return;

        Vector2 lower =
            minBounds + Vector2.one * edgeMargin;

        Vector2 upper =
            maxBounds - Vector2.one * edgeMargin;

        float width = upper.x - lower.x;
        float height = upper.y - lower.y;

        if (width <= 0f || height <= 0f)
            return;

        float aspect = Mathf.Max(
            0.01f, cameraComponent.aspect);

        // Ajustar el zoom si el encuadre supera el terreno.
        float maximumSize = Mathf.Min(
            height * 0.5f,
            width / (2f * aspect));

        cameraComponent.orthographicSize = Mathf.Min(
            originalSize, maximumSize);

        float halfHeight =
            cameraComponent.orthographicSize;

        float halfWidth = halfHeight * aspect;

        float minX = lower.x + halfWidth;
        float maxX = upper.x - halfWidth;
        float minY = lower.y + halfHeight;
        float maxY = upper.y - halfHeight;

        Vector3 position = transform.position;

        position.x = minX < maxX
            ? Mathf.Clamp(position.x, minX, maxX)
            : (lower.x + upper.x) * 0.5f;

        position.y = minY < maxY
            ? Mathf.Clamp(position.y, minY, maxY)
            : (lower.y + upper.y) * 0.5f;

        transform.position = position;
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 center = (minBounds + maxBounds) * 0.5f;
        Vector2 size = maxBounds - minBounds;

        Gizmos.color = Color.cyan;

        Gizmos.DrawWireCube(
            new Vector3(center.x, center.y, 0f),
            new Vector3(size.x, size.y, 0f));
    }
}