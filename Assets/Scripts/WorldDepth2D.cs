using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Ordena un objeto completo por sus pies, garantizando que quede por encima del suelo y del pasto.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SortingGroup))]
public sealed class WorldDepth2D : MonoBehaviour
{
    public float feetOffset;
    public string sortingLayer = "Layer 1";
    private SortingGroup group;

    private void OnEnable() { Refresh(); }
    private void LateUpdate() { Refresh(); }
    public void Refresh()
    {
        if (group == null) group = GetComponent<SortingGroup>();
        if (group == null) return;
        group.sortingLayerName = sortingLayer;
        // Base positiva de 1000 para que ningún objeto con Y positivo quede por debajo del suelo (-200) o del pasto (-10)
        group.sortingOrder = Mathf.Clamp(1000 + Mathf.RoundToInt(-(transform.position.y + feetOffset) * 10f), 0, 5000);
    }
}
