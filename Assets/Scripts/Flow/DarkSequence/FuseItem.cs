using UnityEngine;
using UnityEngine.Events;
using Bloodrush.UI;
using Bloodrush.Shared.Audio;

namespace Bloodrush.Flow
{
// ADIM 4 — Yerden alınan sigorta. Alınınca kendini kapatır ve panele haber verir.
// Paneli kendisi bulabilir, böylece sahnede birden fazla sigortayı tek tek bağlamak
// zorunda kalmazsın.
public class FuseItem : ProximityInteractable
{
    [Header("Sigorta")]
    [SerializeField] string displayName = "SİGORTA";
    [Tooltip("Boşsa sahnedeki ilk FusePanel otomatik bulunur.")]
    [SerializeField] FusePanel panel;

    [Header("Ses")]
    [SerializeField] AudioClip pickupClip;
    [SerializeField] [Range(0f,2f)] float pickupVolume = 1f;

    [Tooltip("Alındığı AN tetiklenir — silüet anını (SilhouetteScare.Show) buraya bağla.")]
    public UnityEvent onCollected;

    bool taken;

    protected override string PromptText => taken ? null : $"[E] {displayName} Al";

    protected override void OnInteract()
    {
        if (taken) return;
        taken = true;

        SfxPlayer.PlayAtPoint(pickupClip, transform.position, pickupVolume);
        Notification.Show($"{displayName} ALINDI");

        if (panel == null) panel = FindFirstObjectByType<FusePanel>();
        if (panel != null) panel.AddFuse();
        else Debug.LogWarning("[FuseItem] FusePanel bulunamadı — sigorta sayılmadı.", this);

        onCollected?.Invoke();
        gameObject.SetActive(false);
    }
}
}
