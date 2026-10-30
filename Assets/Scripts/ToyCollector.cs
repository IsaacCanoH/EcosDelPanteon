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

    public bool HasToy(int id) { return id >= 0 && id < RequiredToys && (collectedMask & (1 << id)) != 0; }

    public bool Collect(int id)
    {
        if (id < 0 || id >= RequiredToys || HasToy(id)) return false;
        collectedMask |= 1 << id;
        Count++;
        if (pickupAudio != null && pickupSound != null) pickupAudio.PlayOneShot(pickupSound);

        var weapons = GetComponent<PlayerWeaponController>();
        if (weapons != null)
        {
            if (Count >= 1 && !weapons.hasBigKnife) weapons.UnlockAndEquip(WeaponType.BigKnife);
            if (Count >= 3 && !weapons.hasMachete) weapons.UnlockAndEquip(WeaponType.Machete);
        }

        Changed?.Invoke(Count);
        return true;
    }

    private void Awake()
    {
        Count = 0;
        for (int i = 0; i < RequiredToys; i++) if (HasToy(i)) Count++;
    }
}
