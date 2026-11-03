using System;
using UnityEngine;

public sealed class ToyCollector : MonoBehaviour
{
    public const int RequiredToys = 3;
    [SerializeField] private int collectedMask;
    public AudioSource pickupAudio;
    public AudioClip pickupSound;
    public int Count { get; private set; }
    public event Action<int> Changed;

    public bool HasToy(int id)
    {
        return id >= 0 && id < RequiredToys && (collectedMask & (1 << id)) != 0;
    }

    public bool Collect(int id)
    {
        if (id < 0 || id >= RequiredToys || HasToy(id)) return false;
        collectedMask |= 1 << id;
        Count++;
        if (pickupAudio != null && pickupSound != null) pickupAudio.PlayOneShot(pickupSound);

        // Respaldo: al obtener el primer juguete, asegurar que el Cuchillo esté tirado al lado con su animación de recolección
        if (Count == 1)
        {
            var weapons = GetComponent<PlayerWeaponController>();
            if (weapons != null && !weapons.hasBigKnife && !WeaponPickup.IsWeaponDropActive(WeaponType.BigKnife))
            {
                WeaponPickup.SpawnWeaponDrop(WeaponType.BigKnife, transform.position + new Vector3(0.82f, 0f, 0f));
            }
        }

        Changed?.Invoke(Count);

        if (Level2VictoryManager.Instance != null)
        {
            Level2VictoryManager.Instance.CheckVictoryConditions();
        }

        return true;
    }

    private void Awake()
    {
        Count = 0;
        for (int i = 0; i < RequiredToys; i++)
        {
            if (HasToy(i)) Count++;
        }
    }
}
